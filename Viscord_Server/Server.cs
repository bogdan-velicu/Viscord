using LiteDB;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Drawing;
using System.Threading.Tasks;
using System.Linq;
using System.Windows;
using static Viscord_Server.Server.ReceivePacket;

namespace Viscord_Server
{
    class Server
    {
        public static List<Client> Clients = new List<Client>();
        public static int voicePort = 23650;
        static void Main(string[] args)
        {
            Lite.InitDatabase();
            Conversations.Messages = new List<ViscordMessage>();

            var users = Lite.GetAllUsers();
            foreach(var user in users)
            {
                Clients.Add(new Client(user.Name, user.Image));
            }

            Listener.StartListening();
            VoiceListener.Channels = new List<VoiceChannel>();
            VoiceListener.StartListening(voicePort);
            while(true)
            {
                var cmd = Console.ReadLine();
                if(cmd.Contains("delete"))
                {
                    string name = cmd.Split(' ')[1];
                    Lite.RemoveUser(name);
                }
                else if(cmd.Contains("add"))
                {
                    Console.WriteLine("[name] [password] [ip] [avatar]");
                    var idk = Console.ReadLine();
                    var stuff = idk.Split(' ');
                    Lite.AddUser(stuff[0], stuff[1], stuff[2], stuff[3]);
                }
                else if(cmd.Contains("disconnect"))
                {
                    string name = cmd.Split(' ')[1];
                    var client = ClientController.GetClient(name);
                    if (client == null)
                    {
                        Console.WriteLine("no user found");
                        continue;
                    }
                    foreach (var cl in Clients)
                    {
                        if (cl != client)
                            cl.Receive.Send($"[userdisconnect]{{$}}{name}");
                    }
                    ClientController.RemoveClient(client.Socket);
                    client.Socket.Dispose();
                }
                if (cmd == "stop")
                    break;
            }
            Lite.Database.Dispose();
        }

        public class User
        {
            public string Name { get; set; }
            public string Password { get; set; }
            public string Ip { get; set; }
            public string Image { get; set; }

            public User(string name, string pass, string ip, string img = "")
            {
                Name = name;
                Password = pass;
                Ip = ip;
                Image = img;
            }
        }

        #region LiteDB
        public static class Lite
        {
            public static LiteDatabase Database { get; set; }

            public static void InitDatabase()
            {
                Database = new LiteDatabase(@"database.db");

                var col = Database.GetCollection<User>("users");

                col.EnsureIndex(x => x.Name);
            }

            public static void AddUser(string name, string pass, string ip, string img = "http://zotrix.ddns.net:6746/avatars/default.png")
            {
                var col = Database.GetCollection<User>("users");
                var user = new User(name, pass, ip, img);
                col.Insert(user);
            }

            public static void RemoveUser(string name)
            {
                var col = Database.GetCollection<User>("users");
                col.DeleteMany(user => user.Name == name);
            }

            public static void UpdateUser(string name, string pass, string ip, string img = "http://zotrix.ddns.net:6746/avatars/default.png")
            {
                var col = Database.GetCollection<User>("users");
                var found = col.FindOne(x => x.Name == name);
                found.Name = name;
                found.Password = pass;
                found.Ip = ip;
                found.Image = img;
                col.Update(found);
            }

            public static void ClearDatabase()
            {
                var col = Database.GetCollection<User>("users");
                col.DeleteAll();
            }

            public static List<User> GetAllUsers()
            {
                var col = Database.GetCollection<User>("users");
                var users = col.FindAll().ToList();
                return users;
            }

            public static User GetUser(string name)
            {
                var col = Database.GetCollection<User>("users");
                var user = col.FindOne(x => x.Name == name);
                return user;
            }

            public static User GetUser(IPAddress ip)
            {
                var col = Database.GetCollection<User>("users");
                string res = ip.ToString();
                var user = col.FindOne(x => x.Ip == res);
                return user;
            }
        }
        #endregion

        public class Client
        {
            public Socket Socket { get; set; }
            public ReceivePacket Receive { get; set; }
            public string Name { get; set; }
            public string Image { get; set; }
            public bool Online { get; set; }

            public Client(Socket socket)
            {
                Receive = new ReceivePacket(socket);
                Receive.StartReceiving();
                Socket = socket;
            }

            public Client(string name, string image, bool online = false)
            {
                Name = name;
                Image = image;
                Online = online;
            }

            public void SetSocket(ReceivePacket rp)
            {
                Receive = rp;
                Receive.StartReceiving();
                Socket = rp._receiveSocket;
            }

            public void SetOnline(bool online)
            {
                Online = online;
            }

            public void SetName(string name)
            {
                Name = name;
            }

            public void SetImage(string img)
            {
                Image = img;
            }
        }

        static class ClientController
        {
            public static void AddClient(Socket socket)
            {
                Clients.Add(new Client(socket));
            }

            public static void AddClient(Client client)
            {
                Clients.Add(client);
            }

            public static void DisconnectClient(Socket sock)
            {
                foreach(var cl in Clients)
                {
                    if(cl.Socket == sock) // if we found the connected socket, dispose it and clear receivepacket class
                    {
                        cl.Receive = null;
                        cl.Socket.Dispose();
                        cl.SetOnline(false);
                        cl.Socket = null;
                    }
                }
            }

            public static void RemoveClient(Socket socket)
            {
                int index = Clients.FindIndex(x => x.Socket == socket);
                if (index == -1)
                    return;
                Clients.RemoveAt(index);
            }

            public static Client GetClient(Socket socket)
            {
                int index = Clients.FindIndex(x => x.Socket == socket);
                if (index == -1)
                    return null;
                return Clients[index];
            }

            public static Client GetClient(string username)
            {
                int index = Clients.FindIndex(x => x.Name == username);
                if (index == -1)
                    return null;
                return Clients[index];
            }

            public static string GetClientName(Socket socket)
            {
                int index = Clients.FindIndex(x => x.Socket == socket);
                if (index == -1)
                    return "";
                return Clients[index].Name;
            }
        }

        class MessageBuilder
        {
            public string Result { get; set; }

            public MessageBuilder()
            {
                Result = "";
            }

            public void Add(string data)
            {
                Result += data + "{$}";
            }

            public string Message()
            {
                Result = Result.Substring(0, Result.Length - 3);
                return Result;
            }
        }

        public class Participant
        {
            public string Name;
            public IPEndPoint EndPoint;
            public Participant(string name, IPEndPoint endPoint)
            {
                Name = name;
                EndPoint = endPoint;
            }
        }

        public class VoiceChannel
        {
            public Socket Server { get; set; }
            public List<Participant> Participants { get; set; }
            public string Name { get; set; }
            private byte[] Buffer { get; set; }
            
            private EndPoint endPoint;

            public VoiceChannel(int port)
            {
                Participants = new List<Participant>();
                Server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                endPoint = new IPEndPoint(IPAddress.Any, port);
                Server.Bind(endPoint);
                Name = "Voice #" + port;
                StartReceiving();
            }

            private void StartReceiving()
            {
                Buffer = new byte[17640];
                Server.BeginReceiveFrom(Buffer, 0, Buffer.Length, SocketFlags.None, ref endPoint, new AsyncCallback(ReceiveCallback), endPoint);
            }

            private void ReceiveCallback(IAsyncResult ar)
            {
                var receivedBytes = Server.EndReceiveFrom(ar, ref endPoint);

                bool containsEndPoint = false;
                foreach (var prt in Participants)
                {
                    if (prt.EndPoint.Equals(endPoint))
                    {
                        containsEndPoint = true; 
                        break;
                    }
                }
                if (!containsEndPoint)
                {
                    // find online client that has same tcp endpoint address as udp
                    foreach(var client in Clients)
                    {
                        if (client.Online && (client.Socket.RemoteEndPoint as IPEndPoint).Address.ToString() == (endPoint as IPEndPoint).Address.ToString())
                        {
                            Participants.Add(new Participant(client.Name, endPoint as IPEndPoint));
                            break;
                        }
                    }
                }

                if(Participants.Count > 1)
                {
                    foreach(var part in Participants.ToArray()) // forward data to all connected clients
                    {
                        if(part.EndPoint != endPoint) // besides the one that sent it in the first place
                        {
                            Server.SendTo(Buffer, 0, receivedBytes, SocketFlags.None, part.EndPoint);
                        }
                    }
                }
                StartReceiving();
            }
        }

        public static class VoiceListener
        {
            public static List<VoiceChannel> Channels { get; set; }
            private static int Port { get; set; }

            public static void StartListening(int port)
            {
                try
                {
                    Port = port;
                    Console.WriteLine($"Voice channel created on port: {Port}");
                    Channels.Add(new VoiceChannel(port));

                    //if (voicePort < 23653)
                    //    StartListening(++voicePort);

                }
                catch (Exception ex)
                {
                    throw new Exception("VoiceChannel error: " + ex);
                }
            }
        }

        #region OldVoiceClass
        /*
    public class Voice
    {
        public UdpClient Socket { get; set; }
        public List<int> ForwardTo { get; set; }

        public IPEndPoint endPoint;

        public Voice(UdpClient sock, IPEndPoint end)
        {
            endPoint = end;
            Socket = sock;
            StartReceiving();
        }
        public async void StartReceiving()
        {
            try
            {
                UdpReceiveResult receiveBytes = await Socket.ReceiveAsync();

                if (Socket.Client.Connected && receiveBytes.Buffer.Length > 1)
                {
                    endPoint = receiveBytes.RemoteEndPoint;

                    // forward the voice data to other participants
                    foreach (var clientIndex in ForwardTo)
                    {
                        Clients[clientIndex].Voice.Send(receiveBytes.Buffer);
                    }

                    StartReceiving();
                }
                else
                    Disconnect();
            }
            catch { }
        }
        public void Send(byte[] data)
            {
                try
                {
                    Socket.Send(data, data.Length, endPoint);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            public void Disconnect()
            {
                if (Socket.Client.Connected)
                    Socket.Dispose();
            }
        }
        */
        #endregion

        public class ReceivePacket
        {
            private byte[] _buffer;
            public Socket _receiveSocket;

            public ReceivePacket(Socket receiveSocket)
            {
                _receiveSocket = receiveSocket;
            }

            public void ReceiveLogin()
            {
                try
                {
                    byte[] buffer = new byte[4];
                    _receiveSocket.Receive(buffer, 0, buffer.Length, SocketFlags.None);
                    byte[] data = new byte[BitConverter.ToInt32(buffer)];
                    _receiveSocket.Receive(data, 0, data.Length, SocketFlags.None);
                    string decodedData = Encoding.Unicode.GetString(data);
                    if (decodedData.Contains("[login]"))
                    {
                        string[] rows = decodedData.Split(new string[] { "{$}" }, StringSplitOptions.None);
                        string username = rows[1], password = rows[2],
                            ip = ((IPEndPoint)_receiveSocket.RemoteEndPoint).Address.ToString();

                        MessageBuilder mb = new MessageBuilder();

                        int clientIndex = -1;
                        for (int i = 0; i < Clients.Count; i++)
                        {
                            if (Clients[i].Name == username && Clients[i].Socket == null)
                            {
                                Clients[i].SetSocket(this);
                                clientIndex = i;
                                break;
                            }
                            else if (Clients[i].Name == username && Clients[i].Socket != null) // user already connected
                            {
                                mb.Add("[login_fail]");
                                mb.Add("User already connected");
                                this.Send(mb.Message()); // sends back to origin socket failure
                                throw new Exception();
                            }
                        }

                        // search in db for user
                        var user = Lite.GetUser(username);

                        if (user == null)
                        {
                            mb.Add("[login_fail]");
                            mb.Add("Username not found");
                            Clients[clientIndex].Receive.Send(mb.Message());
                            throw new Exception();
                        }
                        else
                        {
                            if (user.Password != password)
                            {
                                mb.Add("[login_fail]");
                                mb.Add("Incorrect password");
                                Clients[clientIndex].Receive.Send(mb.Message());
                                throw new Exception();
                            }
                            else
                            {
                                mb.Add("[login_success]");
                                mb.Add(user.Image);
                                
                                Clients[clientIndex].SetName(username);
                                Clients[clientIndex].SetOnline(true);
                                Clients[clientIndex].SetImage(user.Image);

                                Clients[clientIndex].Receive.Send(mb.Message());
                            }
                        }

                        Console.WriteLine($"{username} logged in!");

                        foreach (var cl in Clients)
                        {
                            if (cl != Clients[clientIndex] && cl.Receive != null) // send to all online users newuser notification
                                cl.Receive.Send($"[newuser]{{$}}{username}{{$}}{user.Image}");
                        }
                    }
                    else if (decodedData.Contains("[register]"))
                    {

                    }
                }
                catch { }
            }

            public void StartReceiving()
            {
                try
                {
                    _buffer = new byte[4];
                    _receiveSocket.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, ReceiveCallback, null);
                }
                catch { }
            }

            private async void ReceiveCallback(IAsyncResult AR)
            {
                try
                {
                    if (_receiveSocket.EndReceive(AR) > 1)
                    {
                        _buffer = new byte[BitConverter.ToInt32(_buffer, 0)];
                        _receiveSocket.Receive(_buffer, _buffer.Length, SocketFlags.None);

                        string data = Encoding.Unicode.GetString(_buffer);

                        #region Register
                        if (data.Contains("[register]"))
                        {
                            string[] rows = data.Split(new string[] { "{$}" }, StringSplitOptions.None);
                            string username = rows[1], password = rows[2],
                                ip = ((IPEndPoint)_receiveSocket.RemoteEndPoint).Address.ToString();
                            byte[] img_bytes = Encoding.Unicode.GetBytes(rows[3]);

                            MessageBuilder mb = new MessageBuilder();

                            Client client = null;
                            while (client == null)
                            {
                                client = ClientController.GetClient(_receiveSocket);
                                await Task.Delay(200);
                            }

                            if (username == "default")
                            {
                                mb.Add("[register_fail]");
                                mb.Add("Can't use that username");
                                client.Receive.Send(mb.Message());
                                throw new Exception();
                            }

                            // search in db for username
                            var user = Lite.GetUser(username);

                            // search in db for matching ip
                            var ip_addr = IPAddress.Parse(ip);
                            var match_ip = Lite.GetUser(ip_addr);

                            if (user != null)
                            {
                                mb.Add("[register_fail]");
                                mb.Add("Username already in use");
                                client.Receive.Send(mb.Message());
                                throw new Exception();
                                //ClientController.RemoveClient(client._socket);
                            }
                            else if (match_ip != null)
                            {
                                mb.Add("[register_fail]");
                                mb.Add("There is already an account with that IP Address");
                                client.Receive.Send(mb.Message());
                                throw new Exception();
                                //ClientController.RemoveClient(client._socket);
                            }
                            else
                            {
                                string img_url = "";
                                if (img_bytes.Length > 0)
                                {
                                    HttpClient httpClient = new HttpClient();
                                    MultipartFormDataContent form = new MultipartFormDataContent();

                                    form.Add(new ByteArrayContent(img_bytes, 0, img_bytes.Length), "avatar", $"{username}.jpg");
                                    var response = await httpClient.PostAsync("http://zotrix.ddns.net:6746/upload-avatar", form);

                                    if (!response.IsSuccessStatusCode)
                                    {
                                        Console.WriteLine($"Error uploading profile image for {username}");
                                        mb.Add("[register_fail]");
                                        mb.Add("Avatar upload failed. Server might be down.");
                                        client.Receive.Send(mb.Message());
                                        return;
                                    }
                                    img_url = await response.Content.ReadAsStringAsync();
                                }

                                mb.Add("[register_success]");
                                mb.Add(img_url);
                                Lite.AddUser(username, password, ip, img_url);
                                client.Receive.Send(mb.Message());

                                client.SetName(username);
                                client.SetOnline(true);
                                client.SetImage(img_url);

                                Console.WriteLine($"{username} logged in!");

                                foreach (var cl in Clients)
                                {
                                    if (cl != client)
                                        cl.Receive.Send($"[newuser]{{$}}{username}{{$}}{img_url}");
                                }
                            }
                        }
                        #endregion

                        #region RequestConversation
                        else if (data.Contains("[requestconv]"))
                        {
                            string[] rows = data.Split(new string[] { "{$}" }, StringSplitOptions.None);
                            string user = rows[1];

                            MessageBuilder mb = new MessageBuilder();
                            mb.Add("[userconv]");

                            var requester = ClientController.GetClient(_receiveSocket);
                            List<ViscordMessage> msgs = new List<ViscordMessage>();
                            if (Conversations.Messages != null && Conversations.Messages.Count > 0)
                            {
                                msgs = Conversations.Messages.FindAll(x => (
                                x.Sender == user && x.Receiver == requester.Name) ||
                                (x.Sender == requester.Name && x.Receiver == user));
                            }

                            if (msgs.Count < 1)
                            {
                                requester.Receive.Send(mb.Message());
                                StartReceiving();
                                return;
                            }
                            var msg_string = ConversationConverter.ToString(msgs);
                            mb.Add(msg_string.Substring(0, msg_string.Length - 3));

                            requester.Receive.Send(mb.Message());
                        }
                        #endregion

                        #region New Voice User
                        else if (data.Contains("[newvoice]"))
                        {
                            string[] rows = data.Split(new string[] { "{$}" }, StringSplitOptions.None);
                            string vChannel = rows[1];
                            int vIndex = VoiceListener.Channels.FindIndex(x => x.Name == vChannel);
                            Client client = null;
                            foreach(var cl in Clients)
                            {
                                if(cl.Online && cl.Socket.RemoteEndPoint == _receiveSocket.RemoteEndPoint)
                                {
                                    client = cl;
                                }
                            }
                            foreach(var channel in VoiceListener.Channels)
                            {
                                foreach(var part in channel.Participants)
                                {
                                    if(part.Name == client.Name)
                                    {
                                        channel.Participants.Remove(part);
                                    }
                                }
                            }
                            foreach(var cl in Clients) // send fact that new client connected to voice
                            {
                                if(cl.Online && cl.Name != client.Name)
                                {
                                    cl.Receive.Send($"[newvoice]{{$}}{vChannel}{{$}}{client.Name}");
                                }
                            }
                            //VoiceListener.Channels[vIndex].Participants.Add(new Participant(client.Name, (IPEndPoint)client.Socket.RemoteEndPoint));
                        }
                        #endregion

                        #region Left Voice
                        else if(data.Contains("[leftvoice]"))
                        {
                            string[] rows = data.Split(new string[] { "{$}" }, StringSplitOptions.None);
                            string vChannel = rows[1];
                            int vIndex = VoiceListener.Channels.FindIndex(x => x.Name == vChannel);
                            Client client = null;
                            foreach(var cl in Clients)
                            {
                                if(cl.Online && cl.Socket.RemoteEndPoint == _receiveSocket.RemoteEndPoint)
                                {
                                    client = cl;
                                }
                            }
                            int index = -1;
                            for(int i = 0; i < VoiceListener.Channels[vIndex].Participants.Count; i++)
                            {
                                // get user that left by remote endpoint
                                if (VoiceListener.Channels[vIndex].Participants[i].EndPoint.Address.ToString() == (_receiveSocket.RemoteEndPoint as IPEndPoint).Address.ToString())
                                    index = i;
                            }
                            VoiceListener.Channels[vIndex].Participants.RemoveAt(index);
                            foreach (var cl in Clients) // send fact that new client connected to voice
                            {
                                // besides to the client that disconnected
                                if (cl.Online && (cl.Socket.RemoteEndPoint as IPEndPoint).Address.ToString() != (_receiveSocket.RemoteEndPoint as IPEndPoint).Address.ToString())
                                {
                                    cl.Receive.Send($"[leftvoice]{{$}}{vChannel}{{$}}{client.Name}");
                                }
                            }
                        }
                        #endregion

                        #region Get Server Data
                        else if (data.Contains("[getserver]"))
                        {
                            string[] rows = data.Split(new string[] { "{$}" }, StringSplitOptions.None);
                            string server = rows[1];
                            StringBuilder sb = new StringBuilder();
                            var requester = ClientController.GetClient(_receiveSocket);

                            // change in future to support multiple servers
                            if (server == "Server")
                            {
                                sb.Append("[serverdata]{$}");
                                foreach(var vChannel in VoiceListener.Channels) // voice channels
                                {
                                    sb.Append(vChannel.Name + "{@}");
                                    foreach(var part in vChannel.Participants)
                                    {
                                        sb.Append(part.Name + "{@}");
                                    }
                                    sb.Append("{#}");
                                }
                                sb.Append("{$}");
                                //sb.Append("Voice #1{#}Voice #2{#}Voice #3{$}"); // voice channels
                                sb.Append(""); // message channels

                                requester.Receive.Send(sb.ToString());
                            }
                        }
                        #endregion

                        #region CallTo
                        /*
                        else if (data.Contains("[callto]"))
                        {
                            string[] rows = data.Split(new string[] { "{$}" }, StringSplitOptions.None);
                            string username = rows[1];

                            var sender = ClientController.GetClient(_receiveSocket);
                            int clientIndex = Clients.FindIndex(x => x == sender);

                            Client receiver = ClientController.GetClient(username);

                            if (receiver == null || receiver.Receive == null)
                            {
                                sender.Receive.Send("[callres]{$}That user is offline");
                                StartReceiving();
                                return;
                            }

                            MessageBuilder mb = new MessageBuilder();
                            mb.Add("[callfrom]");
                            mb.Add(sender.Name);

                            receiver.Receive.Send(mb.Message());
                        }
                        */
                        #endregion

                        #region CallResponse
                        /*else if(data.Contains("[callacceptedfrom]") || data.Contains("[calldeclinedfrom]"))
                        {
                            string[] rows = data.Split(new string[] { "{$}" }, StringSplitOptions.None);
                            bool accepted = data.Contains("[callacceptedfrom]") ? true : false;
                            string receivername = rows[1], sendername = rows[2];

                            var sender = ClientController.GetClient(sendername);
                            int senderIndex = Clients.FindIndex(x => x == sender);

                            Client receiver = ClientController.GetClient(receivername);
                            int receiverIndex = Clients.FindIndex(x => x == receiver);

                            if (accepted)
                            {
                                // connect both clients to udp
                                var participants1 = new List<int>();
                                participants1.Add(senderIndex);
                                int receiverPort = AssignVoice(receiverIndex, participants1);
                                if(receiverPort != 0)
                                {
                                    receiver.Receive.Send($"[callres]{{$}}accepted{{$}}{receiverPort}{{$}}{sender.Name}");
                                }

                                var participants2 = new List<int>();
                                participants2.Add(receiverIndex);
                                int senderPort = AssignVoice(senderIndex, participants2);
                                if(senderPort != 0)
                                {
                                    sender.Receive.Send($"[callres]{{$}}accepted{{$}}{senderPort}{{$}}{receiver.Name}");
                                }
                            }
                            else
                            {
                                receiver.Receive.Send("[callres]{$}declined");
                            }
                        }*/
                        #endregion

                        #region GetUsers
                        else if (data.Contains("[getusers]"))
                        {
                            MessageBuilder mb = new MessageBuilder();
                            mb.Add("[allusers]");
                            foreach (var client in Clients)
                            {
                                mb.Add($"{client.Name}{{#}}{client.Online}{{#}}{client.Image}");
                            }
                            Client sock = ClientController.GetClient(_receiveSocket);
                            sock.Receive.Send(mb.Message());
                        }
                        #endregion

                        #region MessageTo
                        else if (data.Contains("[messageto]"))
                        {
                            string[] rows = data.Split(new string[] { "{$}" }, StringSplitOptions.None);
                            string user = rows[1];
                            Client client = ClientController.GetClient(user);
                            var clientName = ClientController.GetClientName(_receiveSocket);

                            if (client != null && client.Receive != null)
                            {
                                MessageBuilder mb = new MessageBuilder();
                                mb.Add("[messagefrom]");
                                mb.Add(clientName);
                                for (int i = 2; i < rows.Length; i++)
                                {
                                    mb.Add(rows[i]);
                                }

                                client.Receive.Send(mb.Message());
                            }
                            var msg = new ViscordMessage(user, clientName, rows[2], DateTime.Now.ToString("h:mm tt"));
                            Conversations.Messages.Add(msg);
                        }
                        #endregion

                        #region FileTo
                        else if (data.Contains("[fileto]"))
                        {
                            string[] rows = data.Split(new string[] { "{$}" }, StringSplitOptions.None);
                            string bufferLength = rows[3];

                            // Receive the file asap
                            _buffer = new byte[Int32.Parse(bufferLength)];
                            _receiveSocket.Receive(_buffer, _buffer.Length, SocketFlags.None);

                            string username = rows[1], filename = rows[2];

                            var sender = ClientController.GetClient(_receiveSocket);
                            var receiver = ClientController.GetClient(username);

                            MessageBuilder mb = new MessageBuilder();

                            if (_buffer.Length > 100 * 1000000)
                            {
                                mb.Add("[fileupload]");
                                mb.Add("[error]");
                                mb.Add("File is larger than 100mb");
                                sender.Receive.Send(mb.Message());
                                return;
                            }

                            HttpClient httpClient = new HttpClient();
                            MultipartFormDataContent form = new MultipartFormDataContent();

                            form.Add(new ByteArrayContent(_buffer, 0, _buffer.Length), "file", $"{filename}");
                            var response = await httpClient.PostAsync("http://zotrix.ddns.net:6746/upload-file", form);

                            if (!response.IsSuccessStatusCode)
                            {
                                Console.WriteLine($"Error uploading file: {filename}");
                                mb.Add("[fileupload]");
                                mb.Add("[error]");
                                mb.Add("File upload failed. Server might be down.");
                                sender.Receive.Send(mb.Message());
                                return;
                            }
                            var file_url = await response.Content.ReadAsStringAsync();

                            mb.Add("[fileupload]");
                            mb.Add(file_url);

                            sender.Receive.Send(mb.Message());

                            MessageBuilder nb = new MessageBuilder();
                            nb.Add("[filefrom]");
                            nb.Add(sender.Name);
                            nb.Add(filename);
                            nb.Add(file_url);
                            receiver.Receive.Send(nb.Message());

                            var msg = new ViscordMessage(username, sender.Name, file_url, DateTime.Now.ToString("h:mm tt"));
                            Conversations.Messages.Add(msg);
                        }
                        #endregion

                        StartReceiving();
                    }
                    else
                    {
                        Disconnect(_receiveSocket);
                    }
                }
                catch(Exception ex)
                {
                    if (_receiveSocket.Connected)
                        StartReceiving();
                    else
                    {
                        _receiveSocket.Shutdown(SocketShutdown.Receive);
                        Disconnect(_receiveSocket);
                    }
                }
            }

            public void Send(string data)
            {
                try
                {
                    var fullPacket = new List<byte>();
                    var unicodeData = Encoding.Unicode.GetBytes(data);
                    fullPacket.AddRange(BitConverter.GetBytes(unicodeData.Length));
                    fullPacket.AddRange(unicodeData);

                    _receiveSocket.Send(fullPacket.ToArray());
                }
                catch
                {
                    if (!_receiveSocket.Connected)
                        Disconnect(_receiveSocket);
                }
            }

            private void Disconnect(Socket sock)
            {
                Client client = ClientController.GetClient(sock);
                string name = client.Name;
                if (name == null)
                    name = ((IPEndPoint)client.Socket.RemoteEndPoint).Address.ToString();
                Console.WriteLine($"{name} disconnected!");
                foreach (var cl in Clients)
                {
                    if (cl != client && cl.Receive != null) // send to all online users userdisconnected notify
                        cl.Receive.Send($"[userdisconnect]{{$}}{name}");
                }
                ClientController.DisconnectClient(sock);
            }
        }

        public static class ConversationConverter
        {
            public static string ToString(List<ViscordMessage> msgList)
            {
                StringBuilder sb = new StringBuilder();
                foreach(var msg in msgList)
                {
                    MessageBuilder mb = new MessageBuilder();
                    mb.Add(msg.Sender);         // sender
                    mb.Add(msg.Receiver);       // receiver
                    mb.Add(msg.Timestamp);      // timestamp
                    mb.Add(msg.Message);        // message
                    sb.Append(mb.Message() + "{#}");
                }
                return sb.ToString();
            }

            public static List<ViscordMessage> FromString(string msgList)
            {
                List<ViscordMessage> final = new List<ViscordMessage>();
                string[] messages = msgList.Split(new string[] { "{#}" }, StringSplitOptions.None);
                foreach(var msg in messages)
                {
                    string[] components = msg.Split(new string[] { "{$}" }, StringSplitOptions.None);
                    final.Add(new ViscordMessage(components[1], components[0], components[3], components[2]));
                }
                return final;
            }
        }

        public static class Conversations
        {
            public static List<ViscordMessage> Messages { get; set; }
        }

        public class ViscordMessage
        {
            public string Sender { get; set; }
            public string Message { get; set; }
            public string Timestamp { get; set; }
            public string Receiver { get; set; }

            // adapted for server-side
            public ViscordMessage(string receiver, string sender, string message, string timestamp)
            {
                Receiver = receiver;
                Sender = sender;
                Message = message;
                Timestamp = timestamp;
            }
        }

        public static class Listener
        {
            public static Socket ListenerSocket; //This is the socket that will listen to any incoming connections
            public static short Port = 6745; // on this port we will listen

            public static void StartListening()
            {
                try
                {
                    Console.WriteLine($"Listening started port: {Port} protocol type: {ProtocolType.Tcp}");
                    ListenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    ListenerSocket.Bind(new IPEndPoint(IPAddress.Any, Port));
                    ListenerSocket.Listen(10);
                    ListenerSocket.BeginAccept(AcceptCallback, ListenerSocket);
                }
                catch (Exception ex)
                {
                    throw new Exception("listening error" + ex);
                }
            }

            public static void AcceptCallback(IAsyncResult ar)
            {
                try
                {
                    Socket acceptedSocket = ListenerSocket.EndAccept(ar);
                    Console.WriteLine($"Accepted ip: {acceptedSocket.RemoteEndPoint}");

                    ListenerSocket.BeginAccept(AcceptCallback, ListenerSocket);

                    var rec = new ReceivePacket(acceptedSocket);
                    rec.ReceiveLogin();
                }
                catch (Exception ex)
                {
                    throw new Exception("Base Accept error" + ex);
                }
            }
        }
    }
}
