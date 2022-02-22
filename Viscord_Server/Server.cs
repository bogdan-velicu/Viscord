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
    public class Server
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
                            cl.Receive.Send(name, PacketID.UserDisconnected);
                    }
                    ClientController.RemoveClient(client.Socket);
                    client.Socket.Dispose();
                }
                if (cmd == "stop")
                    break;
            }
            Lite.Database.Dispose();
        }

        public enum PacketID
        {
            Login,
            LoginResponse,
            Register,
            RegisterResponse,
            VoiceConnected,
            VoiceDisconnected,
            VoiceStatus,
            ConversationData,
            ServerData,
            UsersData,
            UserConnected,
            UserDisconnected,
            Message,
            File,
            FileResponse,
            None
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
            public IPEndPoint UdpEndPoint { get; set; }
            public string Name { get; set; }
            public string Image { get; set; }
            public bool Online { get; set; }

            public Client(Socket socket)
            {
                Receive = new ReceivePacket(socket);
                Receive.StartReceiving();
                Socket = socket;
                UdpEndPoint = null;
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
                Socket = rp._receiveSocket;
            }

            public void StartReceiving()
            {
                Receive.StartReceiving();
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

        public static class ClientController
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

        public class MessageBuilder
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
                if(Result.Length < 3)
                {
                    return "";
                }
                Result = Result.Substring(0, Result.Length - 3);
                return Result;
            }
        }

        public class Participant
        {
            public string Name;
            public IPEndPoint UDPEndPoint;
            public Participant(string name, IPEndPoint endPoint)
            {
                Name = name;
                UDPEndPoint = endPoint;
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

            private bool IsParticipant(Client client)
            {
                foreach(var participant in Participants)
                {
                    if(participant.Name == client.Name)
                        return true;
                }
                return false;
            }

            private void ReceiveCallback(IAsyncResult ar)
            {
                var receivedBytes = Server.EndReceiveFrom(ar, ref endPoint);

                bool containsEndPoint = false;
                foreach (var prt in Participants)
                {
                    if (EndpointMatch(prt.UDPEndPoint, endPoint as IPEndPoint))
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
                        if (client.Online
                            && client.UdpEndPoint != null
                            && EndpointMatch(client.UdpEndPoint, endPoint as IPEndPoint)
                            && !IsParticipant(client))
                        {
                            Participants.Add(new Participant(client.Name, endPoint as IPEndPoint));
                            //Console.WriteLine($"Added new participant: {client.Name} to {Name}");
                            break;
                        }
                    }
                }

                if(Participants.Count > 1)
                {
                    foreach(var part in Participants.ToArray()) // forward data to all connected clients
                    {
                        if(!EndpointMatch(part.UDPEndPoint, endPoint as IPEndPoint)) // besides the one that sent it in the first place
                        {
                            Server.SendTo(Buffer, 0, receivedBytes, SocketFlags.None, part.UDPEndPoint);
                        }
                    }
                }
                StartReceiving();
            }
        }

        public static bool EndpointMatch(IPEndPoint end1, IPEndPoint end2)
        {
            if (end1.Address.ToString() == end2.Address.ToString() && end1.Port == end2.Port)
                return true;

            return false;
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

        public class ReceivePacket
        {
            private byte[] _buffer;
            public Socket _receiveSocket;

            public ReceivePacket(Socket receiveSocket)
            {
                _receiveSocket = receiveSocket;
            }

            public async void ReceiveLogin()
            {
                bool loginSuccesfull = false;

                try
                {
                    while (!loginSuccesfull)
                    {
                        byte[] buffer = new byte[4];
                        _receiveSocket.Receive(buffer, 0, buffer.Length, SocketFlags.None);
                        byte[] data = new byte[BitConverter.ToInt32(buffer)];
                        _receiveSocket.Receive(data, 0, data.Length, SocketFlags.None);
                        string decodedData = Encoding.Unicode.GetString(data.Skip(1).ToArray());

                        if ((PacketID)data[0] == PacketID.Login)
                        {
                            loginSuccesfull = ServerCmds.LoginUser(decodedData, _receiveSocket, this);
                        }
                        else if ((PacketID)data[0] == PacketID.Register)
                        {
                            loginSuccesfull = await ServerCmds.RegisterUser(decodedData, _receiveSocket, this); // needs fix
                        }
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

            private void ReceiveCallback(IAsyncResult AR)
            {
                try
                {
                    if (_receiveSocket.EndReceive(AR) > 1)
                    {
                        _buffer = new byte[BitConverter.ToInt32(_buffer, 0)];
                        _receiveSocket.Receive(_buffer, _buffer.Length, SocketFlags.None);

                        string data = Encoding.Unicode.GetString(_buffer.Skip(1).ToArray());
                        PacketID id = (PacketID)_buffer[0];

                        switch (id) {

                            case PacketID.ConversationData:
                                ServerCmds.RequestConversation(data, _receiveSocket);
                                break;

                            case PacketID.VoiceConnected:
                                ServerCmds.VoiceConnected(data, _receiveSocket);
                                break;

                            case PacketID.VoiceDisconnected:
                                ServerCmds.VoiceDisconnected(data, _receiveSocket);
                                break;

                            case PacketID.ServerData:
                                ServerCmds.ServerData(data, _receiveSocket);
                                break;

                            case PacketID.UsersData:
                                ServerCmds.UsersData(_receiveSocket);
                                break;

                            case PacketID.Message:
                                ServerCmds.Message(data, _receiveSocket);
                                break;

                            case PacketID.File:
                                ServerCmds.File(data, _receiveSocket);
                                break;

                            default:
                                Console.WriteLine("Received other packet: " + id);
                                break;
                        }

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

                        StartReceiving();
                    }
                    else
                    {
                        Disconnect(_receiveSocket);
                    }
                }
                catch(Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(ex);
                    Console.ResetColor();

                    if (_receiveSocket.Connected)
                        StartReceiving();
                    else
                    {
                        _receiveSocket.Shutdown(SocketShutdown.Receive);
                        Disconnect(_receiveSocket);
                    }
                }
            }

            public void Send(string data, PacketID id = PacketID.None)
            {
                try
                {
                    var fullPacket = new List<byte>();
                    var unicodeData = Encoding.Unicode.GetBytes(data);
                    fullPacket.AddRange(BitConverter.GetBytes(unicodeData.Length + 1));
                    fullPacket.Add((byte)id);
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
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"{name} disconnected!");
                Console.ResetColor();
                foreach (var cl in Clients)
                {
                    if (cl != client && cl.Receive != null) // send to all online users userdisconnected notify
                        cl.Receive.Send(name, PacketID.UserDisconnected);
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
