using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static Viscord_Server.Server;

namespace Viscord_Server
{
    public static class ServerCmds
    {
        public static bool LoginUser(string data, Socket socket, ReceivePacket rp)
        {
            string[] rows = data.Split(new string[] { "{$}" }, StringSplitOptions.None);
            string username = rows[0], password = rows[1],
                ip = ((IPEndPoint)socket.RemoteEndPoint).Address.ToString();

            MessageBuilder mb = new MessageBuilder();

            int clientIndex = Clients.FindIndex(x => x.Name == username);
            
            if (Clients[clientIndex].Socket == null)
                Clients[clientIndex].SetSocket(rp);

            else if (Clients[clientIndex].Online) // user already connected
            {
                mb.Add("error");
                mb.Add("User already connected");
                rp.Send(mb.Message(), PacketID.LoginResponse); // sends back to origin socket failure
                return false;
            }

            // search in db for user
            var user = Lite.GetUser(username);

            if (user == null)
            {
                mb.Add("error");
                mb.Add("Username not found");
                Clients[clientIndex].Receive.Send(mb.Message(), PacketID.LoginResponse);
                return false;
            }
            else
            {
                if (user.Password != password)
                {
                    mb.Add("error");
                    mb.Add("Incorrect password");
                    Clients[clientIndex].Receive.Send(mb.Message(), PacketID.LoginResponse);
                    return false;
                }
                else
                {
                    mb.Add("success");
                    mb.Add(user.Image);

                    Clients[clientIndex].SetName(username);
                    Clients[clientIndex].SetOnline(true);
                    Clients[clientIndex].SetImage(user.Image);

                    Clients[clientIndex].Receive.Send(mb.Message(), PacketID.LoginResponse);

                    Clients[clientIndex].StartReceiving();
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"{username} logged in!");
            Console.ResetColor();

            foreach (var cl in Clients)
            {
                if (cl != Clients[clientIndex] && cl.Receive != null) // send to all online users newuser notification
                    cl.Receive.Send($"{username}{{$}}{user.Image}", PacketID.UserConnected);
            }

            return true;
        }
        public static async Task<bool> RegisterUser(string data, Socket socket, ReceivePacket rp)
        {
            string[] rows = data.Split(new string[] { "{$}" }, StringSplitOptions.None);
            string username = rows[0], password = rows[1],
            ip = ((IPEndPoint)socket.RemoteEndPoint).Address.ToString();
            byte[] img_bytes = Encoding.Unicode.GetBytes(rows[2]);

            MessageBuilder mb = new MessageBuilder();

            Client client = null;
            while (client == null)
            {
                client = Server.ClientController.GetClient(socket);
                await Task.Delay(200);
            }

            if (username == "default")
            {
                mb.Add("error: Can't use that username");
                client.Receive.Send(mb.Message(), PacketID.RegisterResponse);
                return false;
            }

            // search in db for username
            var user = Lite.GetUser(username);

            // search in db for matching ip
            var ip_addr = IPAddress.Parse(ip);
            var match_ip = Lite.GetUser(ip_addr);

            if (user != null)
            {
                mb.Add("error: Username already in use");
                client.Receive.Send(mb.Message(), PacketID.RegisterResponse);
                return false;
            }
            else if (match_ip != null)
            {
                mb.Add("error: There is already an account with that IP Address");
                client.Receive.Send(mb.Message(), PacketID.RegisterResponse);
                return false;
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
                        mb.Add("error: Avatar upload failed. Server might be down.");
                        client.Receive.Send(mb.Message(), PacketID.RegisterResponse);
                        return false;
                    }
                    img_url = await response.Content.ReadAsStringAsync();
                }

                mb.Add(img_url);
                Lite.AddUser(username, password, ip, img_url);
                client.Receive.Send(mb.Message(), PacketID.RegisterResponse);

                client.SetName(username);
                client.SetOnline(true);
                client.SetImage(img_url);

                Clients.Add(client);

                Console.WriteLine($"{username} logged in!");

                foreach (var cl in Clients)
                {
                    if (cl != client)
                        cl.Receive.Send($"{username}{{$}}{img_url}", PacketID.UserConnected);
                }

                return true;
            }
        }
        public static void RequestConversation(string data, Socket socket)
        {
            string user = data;

            MessageBuilder mb = new MessageBuilder();

            var requester = ClientController.GetClient(socket);
            List<ViscordMessage> msgs = new List<ViscordMessage>();
            if (Conversations.Messages != null && Conversations.Messages.Count > 0)
            {
                msgs = Conversations.Messages.FindAll(x => (
                x.Sender == user && x.Receiver == requester.Name) ||
                (x.Sender == requester.Name && x.Receiver == user));
            }

            if (msgs.Count < 1)
            {
                requester.Receive.Send(mb.Message(), PacketID.ConversationData);
                return;
            }

            var msg_string = ConversationConverter.ToString(msgs);
            mb.Add(msg_string.Substring(0, msg_string.Length - 3));
            requester.Receive.Send(mb.Message(), PacketID.ConversationData);
        }
        public static void VoiceConnected(string data, Socket socket)
        {
            string[] rows = data.Split(new string[] { "{$}" }, StringSplitOptions.None);
            string vChannel = rows[0], userName = rows[1], port = rows[2];
            int vIndex = VoiceListener.Channels.FindIndex(x => x.Name == vChannel);
            Client client = null;
            for (int i = 0; i < Clients.Count; i++)
            {
                if (Clients[i].Online && Clients[i].Name == userName) /*cl.Socket.RemoteEndPoint == socket.RemoteEndPoint)*/
                {
                    Clients[i].UdpEndPoint = new IPEndPoint((Clients[i].Socket.RemoteEndPoint as IPEndPoint).Address, int.Parse(port));
                    client = Clients[i];
                }
            }
            //foreach (var channel in VoiceListener.Channels)
            //{
            //    foreach (var part in channel.Participants.ToArray())
            //    {
            //        if (part.Name == client.Name)
            //        {
            //            channel.Participants.Remove(part);
            //        }
            //    }
            //}
            foreach (var cl in Clients) // send fact that new client connected to voice
            {
                if (cl.Online && cl.Name != client.Name)
                {
                    cl.Receive.Send($"{vChannel}{{$}}{client.Name}", PacketID.VoiceConnected);
                }
            }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"{client.Name} connected to voice ({vChannel})");
            Console.ResetColor();
        }
        public static void VoiceDisconnected(string data, Socket socket)
        {
            string[] rows = data.Split(new string[] { "{$}" }, StringSplitOptions.None);
            string vChannel = rows[0], userName = rows[1];
            int vIndex = VoiceListener.Channels.FindIndex(x => x.Name == vChannel);
            for (int i = 0; i < VoiceListener.Channels[vIndex].Participants.Count; i++)
            {
                // get user that left by name
                var part = VoiceListener.Channels[vIndex].Participants[i];
                if (part.Name == userName) // could be exploited needs to be changed
                {
                    VoiceListener.Channels[vIndex].Participants.Remove(part);
                    for (int j = 0; j < Clients.Count; j++)
                    {
                        if (Clients[j].Name == userName)
                        {
                            Clients[j].UdpEndPoint = null;
                            break;
                        }
                    }
                    //Console.WriteLine($"Removing participant {part.Name} from {VoiceListener.Channels[vIndex].Name}");
                    break;
                }
            }
            foreach (var cl in Clients) // send fact that client disconnected from voice
            {
                // besides to the client that disconnected
                if (cl.Online && (cl.Name != userName))
                {
                    cl.Receive.Send($"{vChannel}{{$}}{userName}", PacketID.VoiceDisconnected);
                }
            }
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{userName} disconnected from voice ({vChannel})");
            Console.ResetColor();
        }
        public static void ServerData(string data, Socket socket)
        {
            string server = data;
            StringBuilder sb = new StringBuilder();
            var requester = ClientController.GetClient(socket);

            // change in future to support multiple servers
            if (server == "Server")
            {
                foreach (var vChannel in VoiceListener.Channels) // voice channels
                {
                    sb.Append(vChannel.Name + "{@}");
                    foreach (var part in vChannel.Participants)
                    {
                        sb.Append(part.Name + "{@}");
                    }
                    sb.Append("{#}");
                }
                sb.Append("{$}");
                //sb.Append("Voice #1{#}Voice #2{#}Voice #3{$}"); // voice channels
                sb.Append(""); // message channels

                requester.Receive.Send(sb.ToString(), PacketID.ServerData);
            }
        }
        public static void UsersData(Socket socket)
        {
            MessageBuilder mb = new MessageBuilder();
            foreach (var client in Clients)
            {
                mb.Add($"{client.Name}{{#}}{client.Online}{{#}}{client.Image}");
            }
            Client sock = ClientController.GetClient(socket);
            sock.Receive.Send(mb.Message(), PacketID.UsersData);
        }
        public static void Message(string data, Socket socket)
        {
            string[] rows = data.Split(new string[] { "{$}" }, StringSplitOptions.None);
            string user = rows[0];
            Client client = ClientController.GetClient(user);
            var clientName = ClientController.GetClientName(socket);

            if (client != null && client.Receive != null)
            {
                MessageBuilder mb = new MessageBuilder();
                mb.Add(clientName);
                for (int i = 1; i < rows.Length; i++)
                {
                    mb.Add(rows[i]);
                }

                client.Receive.Send(mb.Message(), PacketID.Message);
            }
            var msg = new ViscordMessage(user, clientName, rows[1], DateTime.Now.ToString("h:mm tt"));
            Conversations.Messages.Add(msg);
        }
        public static async void File(string data, Socket socket)
        {
            string[] rows = data.Split(new string[] { "{$}" }, StringSplitOptions.None);
            string bufferLength = rows[2];

            // Receive the file asap
            var buffer = new byte[Int32.Parse(bufferLength)];
            socket.Receive(buffer, buffer.Length, SocketFlags.None);

            string username = rows[0], filename = rows[1];

            var sender = ClientController.GetClient(socket);
            var receiver = ClientController.GetClient(username);

            MessageBuilder mb = new MessageBuilder();

            if (buffer.Length > 100 * 1000000)
            {
                mb.Add("[error]");
                mb.Add("File is larger than 100mb");
                sender.Receive.Send(mb.Message(), PacketID.FileResponse);
                return;
            }

            HttpClient httpClient = new HttpClient();
            MultipartFormDataContent form = new MultipartFormDataContent();

            form.Add(new ByteArrayContent(buffer, 0, buffer.Length), "file", $"{filename}");
            var response = await httpClient.PostAsync("http://zotrix.ddns.net:6746/upload-file", form);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error uploading file: {filename}");
                mb.Add("[error]");
                mb.Add("File upload failed. Server might be down.");
                sender.Receive.Send(mb.Message(), PacketID.FileResponse);
                return;
            }
            var file_url = await response.Content.ReadAsStringAsync();

            sender.Receive.Send(file_url, PacketID.FileResponse);

            if (receiver.Online)
            {
                MessageBuilder nb = new MessageBuilder();
                nb.Add(sender.Name);
                nb.Add(filename);
                nb.Add(file_url);
                receiver.Receive.Send(nb.Message(), PacketID.File);
            }

            var msg = new ViscordMessage(username, sender.Name, file_url, DateTime.Now.ToString("h:mm tt"));
            Conversations.Messages.Add(msg);
        }
    }
}
