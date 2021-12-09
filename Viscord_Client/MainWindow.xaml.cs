using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Viscord;
using NAudio.Wave;
using XamlAnimatedGif;
using Brush = System.Windows.Media.Brush;
using Image = System.Windows.Controls.Image;

namespace Viscord_Client
{
    public partial class MainWindow : Window
    {
        public static List<User> Users = new List<User>();
        public static Decorator border = null;
        public static ScrollViewer scrollViewer = null;
        RoutedCommand newCmd = new RoutedCommand();
        ViscordMessage prevMsg = null;
        public static MainWindow Window;

        public class Client
        {
            public static Socket _socket { get; set; }
            public static ReceivePacket Receive { get; set; }
            public static string Name { get; set; }
            public static string Image { get; set; }
            public static bool IsInCall { get; set; }
            public static string CurrentVoice { get; set; }
            public static SendPacket SendPacket { get; set; }
            public static User User { get; set; }

            public static void SetClient(Socket socket)
            {
                Receive = new ReceivePacket(socket);
                Receive.StartReceiving();
                _socket = socket;
                SendPacket = new SendPacket(socket);
            }
        }

        public class Connector
        {
            public static Socket _connectingSocket;
            public static Socket voiceSocket;

            public static async Task<bool> TryToConnect()
            {
                _connectingSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                try
                {
                    await _connectingSocket.ConnectAsync(new IPEndPoint(Dns.GetHostAddresses("zotrix.ddns.net")[0], 6745));
                }
                catch (Exception ex)
                {
                    if (ex.HResult == -2147467259)
                    {
                        MessageBox.Show("Server connection timed out");
                        return false;
                    }
                }

                Client.SetClient(_connectingSocket);
                return true;
            }

            public static bool TryToConnectVoice(int port)
            {
                try
                {
                    Voice.Connect(port);
                }
                catch { }
                return true;
            }
        }

        public class SendPacket
        {
            private Socket _sendSocked;

            public SendPacket(Socket sendSocket)
            {
                _sendSocked = sendSocket;
            }

            public void Send(byte[] rawData)
            {
                try
                {
                    _sendSocked.Send(rawData);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }

            public void Send(string data)
            {
                try
                {
                    var fullPacket = new List<byte>();
                    var unicodeData = Encoding.Unicode.GetBytes(data);
                    int length = unicodeData.Length;
                    fullPacket.AddRange(BitConverter.GetBytes(length));
                    fullPacket.AddRange(unicodeData);

                    _sendSocked.Send(fullPacket.ToArray());
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
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
                if (Result.Length < 3)
                    return "";
                string res = Result.Substring(0, Result.Length - 3);
                return res;
            }
        }

        public static class ConversationConverter
        {
            public static List<ViscordMessage> FromString(string msgList)
            {
                if (msgList == "")
                    return null;
                List<ViscordMessage> final = new List<ViscordMessage>();
                string[] messages = msgList.Split(new string[] { "{#}" }, StringSplitOptions.None);
                foreach (var msg in messages)
                {
                    string[] components = msg.Split(new string[] { "{$}" }, StringSplitOptions.None);
                    var user = components[0] == Client.Name ? User.FindUser(components[1]) : User.FindUser(components[0]);
                    final.Add(new ViscordMessage(user, components[0], components[3], components[2]));
                }
                return final;
            }
        }

        public class User
        {
            public string Name { get; set; }
            public string ImageUrl { get; set; }
            public System.Windows.Media.Brush StatusColor { get; set; }
            public bool Online { get; set; }
            public List<ViscordMessage> Conversation { get; set; }

            public User(string name, bool online = false, string img = "http://zotrix.ddns.net:6746/avatars/default.png")
            {
                ImageUrl = img;
                Online = online;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusColor = Online ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Gray;
                });
                Name = name;
                Conversation = new List<ViscordMessage>();
            }

            public User()
            {

            }

            public void SetStatus(bool online)
            {
                var cv = new BrushConverter();
                Online = online; 
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusColor = Online ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Gray;
                });
            }

            public static User FindUser(string name)
            {
                int index = Users.FindIndex(x => x.Name == name);
                if (index == -1)
                    return null;
                return Users[index];
            }

            public static void GetAllUsers()
            {
                Client.SendPacket.Send("[getusers]");
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

            public void StartReceiving()
            {
                try
                {
                    _buffer = new byte[4];
                    _receiveSocket.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, ReceiveCallback, null);
                }
                catch { }
            }

            public void ReceiveCallback(IAsyncResult AR)
            {
                try
                {
                    if (_receiveSocket.Connected && _receiveSocket.EndReceive(AR) > 1)
                    {
                        _buffer = new byte[BitConverter.ToInt32(_buffer, 0)];
                        _receiveSocket.Receive(_buffer, _buffer.Length, SocketFlags.None);

                        string data = Encoding.Unicode.GetString(_buffer);

                        if (data.Contains("[messagefrom]"))
                        {
                            string[] rows = data.Split(new string[] { "{$}" }, StringSplitOptions.None);
                            string sender = rows[1], message = rows[2];
                            var user = User.FindUser(sender);
                            if (user == null)
                            {
                                user = new User(sender);
                                Users.Add(user);
                            }
                            user.Conversation.Add(new ViscordMessage(user, sender, message, DateTime.Now.ToString("h:mm tt")));
                            if (Window.ListBoxSelectedItem() == sender)
                                Window.UpdateMessages(sender);
                            Application.Current.Dispatcher.Invoke(() => {
                                if (Window.WindowState == WindowState.Minimized)
                                    Window.ReceiveMessageNotify(sender, message);
                            });
                        }
                        else if (data.Contains("[filefrom]"))
                        {
                            string[] rows = data.Split(new string[] { "{$}" }, StringSplitOptions.None);
                            string sender = rows[1], filename = rows[2], file_url = rows[3];
                            var user = User.FindUser(sender);
                            if (user == null)
                            {
                                user = new User(sender);
                                Users.Add(user);
                            }
                            user.Conversation.Add(new ViscordMessage(user, sender, file_url, DateTime.Now.ToString("h:mm tt")));
                            if (Window.ListBoxSelectedItem() == sender)
                                Window.UpdateMessages(sender);
                        }
                        else if (data.Contains("[login_"))
                        {
                            Login.Response = data;
                        }
                        else if (data.Contains("[register_"))
                        {
                            Register.Response = data;
                        }
                        else if(data.Contains("[userconv]"))
                        {
                            if(data == "[userconv]")
                            {
                                ConversationRequest = "";
                            }
                            else
                                ConversationRequest = data.Substring(13);
                        }

                        #region Old Call Code
                        //else if(data.Contains("[callfrom]"))
                        //{
                        //    StartReceiving();   // start receiving again cuz next bit of code is sync
                        //                        // and blocks upcoming data traffic until something is returned

                        //    string[] rows = data.Split(new string[] { "{$}" }, StringSplitOptions.None);
                        //    string username = rows[1];
                        //    var sender = User.FindUser(username);

                        //    CallWindow call = null;
                        //    Application.Current.Dispatcher.Invoke(() =>
                        //    {
                        //        call = new CallWindow(sender.ImageUrl, sender.Name);
                        //        call.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                        //        call.ShowDialog();
                        //        call.Activate();
                        //    });

                        //    if(call.bAnswer) // if answered
                        //    {
                        //        Client.SendPacket.Send($"[callacceptedfrom]{{$}}{Client.Name}{{$}}{sender.Name}");
                        //    }
                        //    else
                        //    {
                        //        Client.SendPacket.Send($"[calldeclinedfrom]{{$}}{Client.Name}{{$}}{sender.Name}");
                        //    }
                        //    return;
                        //}
                        //else if(data.Contains("[callres]"))
                        //{
                        //    string[] rows = data.Split(new string[] { "{$}" }, StringSplitOptions.None);
                        //    bool accepted = rows[1] == "accepted" ? true : false;
                        //    if (accepted)
                        //    {
                        //        int port = Int32.Parse(rows[2]);
                        //        string username = rows[3];
                        //        Client.VoicePort = port;
                        //        bool result = Connector.TryToConnectVoice(port);
                        //        if (!result)
                        //            MessageBox.Show("Error trying to connect to voice");
                        //        Window.UpdateStatusBar("In a call with " + username);
                        //        Voice.StartVoice();
                        //    }
                        //    else
                        //    {
                        //        MessageBox.Show(rows[1]);
                        //    }
                        //}
                        #endregion

                        else if(data.Contains("[serverdata]"))
                        {
                            Window.FillCurrentServerData(data);
                            Window.ClearUserListBox();
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                Window.AddToUserListBox(Window.CreateServerXAML());
                            });
                        }
                        else if(data.Contains("[newvoice]"))
                        {
                            string[] rows = data.Split(new string[] { "{$}" }, StringSplitOptions.None);
                            string channelName = rows[1];
                            string userName = rows[2];
                            int index = CurrentServer.VoiceChannels.FindIndex(x => x.Name == channelName);
                            if (index == -1)
                                return;

                            CurrentServer.VoiceChannels[index].AddParticipant(userName);

                            if (Window.ListBoxSelectedIndex() == 0)
                                return;

                            Window.ClearUserListBox();
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                Window.AddToUserListBox(Window.CreateServerXAML());
                            });
                        }
                        else if(data.Contains("[leftvoice]"))
                        {
                            string[] rows = data.Split(new string[] { "{$}" }, StringSplitOptions.None);
                            string channelName = rows[1];
                            string userName = rows[2];
                            int index = CurrentServer.VoiceChannels.FindIndex(x => x.Name == channelName);
                            if (index == -1)
                                return;

                            CurrentServer.VoiceChannels[index].RemoveParticipant(userName);

                            if (Window.ListBoxSelectedIndex() == 0)
                                return;

                            Window.ClearUserListBox();
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                Window.AddToUserListBox(Window.CreateServerXAML());
                            });
                        }
                        else if (data.Contains("[allusers]"))
                        {
                            string[] rows = data.Split(new string[] { "{$}" }, StringSplitOptions.None);
                            for (int i = 1; i < rows.Length; i++)
                            {
                                string[] usr = rows[i].Split(new string[] { "{#}" }, StringSplitOptions.None);
                                if (usr[0] != Client.Name)
                                {
                                    var tempUsr = new User(usr[0], bool.Parse(usr[1]), usr[2]);
                                    Users.Add(tempUsr);

                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        Window.AddToUserListBox(Window.CreateUserXAML(tempUsr));
                                    });
                                }
                            }
                        }
                        else if (data.Contains("[newuser]"))
                        {
                            string[] rows = data.Split(new string[] { "{$}" }, StringSplitOptions.None);
                            string username = rows[1], img = rows[2];
                            foreach(var user in Users)
                            {
                                if(user.Name == username)
                                {
                                    user.SetStatus(true);
                                }
                            }
                            Window.RefreshUsersListBox();
                        }
                        else if (data.Contains("[fileupload]"))
                        {
                            string[] rows = data.Split(new string[] { "{$}" }, StringSplitOptions.None);
                            string response = rows[1];
                            FileResponse = response;
                        }
                        else if (data.Contains("[userdisconnect]"))
                        {
                            string[] rows = data.Split(new string[] { "{$}" }, StringSplitOptions.None);
                            string username = rows[1];
                            foreach(var user in Users)
                            {
                                if(user.Name == username)
                                {
                                    user.SetStatus(false);
                                }
                            }
                            Window.RefreshUsersListBox();
                        }

                        StartReceiving();
                    }
                }
                catch (Exception ex)
                {
                    if (_receiveSocket.Connected)
                        StartReceiving();
                }
            }

        }

        public void RefreshUsersListBox()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                int index = usersListBox.SelectedIndex;
                usersListBox.Items.Clear();
                foreach(var user in Users)
                {
                    AddToUserListBox(Window.CreateUserXAML(user));
                }
                usersListBox.SelectedIndex = index;
            });
        }

        public void ReceiveMessageNotify(string username, string msg)
        {
            new ToastContentBuilder()
            .AddArgument("username", username)
            .AddText(username)
            .AddText(msg)
            .Show();
        }

        public MainWindow()
        {
            Window = this;
            InitializeComponent();
        }

        public static void Logout()
        {
            if (Client._socket != null && Client._socket.Connected)
                Client._socket.Disconnect(false);
            Application.Current.Shutdown();
        }

        public class VoiceChannel
        {
            public List<string> Participants { get; set; }
            public string Name { get; set; }

            public VoiceChannel(string name)
            {
                Participants = new List<string>();
                Name = name;
            }

            public void AddParticipant(string name)
            {
                Participants.Add(name);
            }

            public void RemoveParticipant(string name)
            {
                Participants.Remove(name);
            }
        }

        public class TextChannel
        {
            public string Name { get; set; }

            public TextChannel(string name)
            {
                Name = name;
            }
        }

        public static class CurrentServer
        {
            public static List<VoiceChannel> VoiceChannels = new List<VoiceChannel>();
            public static List<TextChannel> TextChannels = new List<TextChannel>();

            public static void Clear()
            {
                VoiceChannels = new List<VoiceChannel>();
                TextChannels = new List<TextChannel>();
            }
        }

        public ListBoxItem[] CreateServerXAML()
        {
            List<ListBoxItem> items = new List<ListBoxItem>();

            // future fix arrange channels in all ways
            int vIndex = 0, uIndex = 0, tIndex = 0;
            foreach(var vChannel in CurrentServer.VoiceChannels)
            {
                ListBoxItem item = new ListBoxItem();
                #region Voice Channel UI

                System.Windows.Shapes.Rectangle rect = new System.Windows.Shapes.Rectangle();
                rect.Width = 20;
                rect.Height = 20;
                rect.HorizontalAlignment = HorizontalAlignment.Left;
                rect.Fill = new ImageBrush(new BitmapImage(new Uri(@"pack://application:,,,/Resources/voice.png")));

                TextBlock text = new TextBlock();
                text.Margin = new Thickness(50, 0, 0, 0);
                text.HorizontalAlignment = HorizontalAlignment.Left;
                text.Text = vChannel.Name;

                Grid grid = new Grid();
                grid.Height = 30;
                grid.Children.Add(rect);
                grid.Children.Add(text);

                ListBoxItem channel = new ListBoxItem();
                channel.Margin = new Thickness(0, 5, 0, 0);
                channel.BorderBrush = System.Windows.Media.Brushes.Gray;
                channel.Content = grid;

                item.Content = grid;
                #endregion
                item.Name = "VChannel" + vIndex++;
                item.Cursor = Cursors.Hand;
                items.Add(item);

                foreach(var participant in vChannel.Participants)
                {
                    var user = new User();
                    if (Client.Name == participant)
                    {
                        user.Name = Client.Name;
                        user.ImageUrl = Client.Image;
                    }
                    else
                        user = Users.Find(x => x.Name == participant);

                    ListBoxItem partItem = new ListBoxItem();
                    #region User UI
                    partItem.Margin = new Thickness(30, 5, 0, 0);

                    Ellipse ellipse = new Ellipse();
                    ellipse.Margin = new Thickness(5, 0, 0, 0);
                    ellipse.Stroke = System.Windows.Media.Brushes.White;
                    ellipse.Width = 25;
                    ellipse.Height = 25;
                    ellipse.HorizontalAlignment = HorizontalAlignment.Left;
                    ellipse.Fill = new ImageBrush(new BitmapImage(new Uri(user.ImageUrl)));

                    TextBlock tb = new TextBlock();
                    tb.FontSize = 15;
                    tb.Text = user.Name;
                    tb.HorizontalAlignment = HorizontalAlignment.Left;
                    tb.VerticalAlignment = VerticalAlignment.Center;
                    tb.Margin = new Thickness(40, 0, 0, 0);

                    Grid grid1 = new Grid();
                    grid1.Height = 30;
                    grid1.Width = 130;
                    grid1.Children.Add(ellipse);
                    grid1.Children.Add(tb);

                    Border border = new Border();
                    border.CornerRadius = new CornerRadius(15);
                    border.Background = (Brush)new BrushConverter().ConvertFrom("#FF292929");
                    border.Child = grid1;

                    partItem.Content = border;
                    #endregion
                    partItem.Focusable = false;
                    partItem.Name = "VoiceUser" + uIndex++;
                    items.Add(partItem);
                }
            }

            foreach(var tChannel in CurrentServer.TextChannels)
            {
                var txtChannel = new ListBoxItem();
                #region Text Channel UI

                System.Windows.Shapes.Rectangle rect = new System.Windows.Shapes.Rectangle();
                rect.Width = 20;
                rect.Height = 20;
                rect.HorizontalAlignment = HorizontalAlignment.Left;
                rect.Fill = new ImageBrush(new BitmapImage(new Uri(@"pack://application:,,,/Resources/text-channel.png")));

                TextBlock text = new TextBlock();
                text.Margin = new Thickness(50, 0, 0, 0);
                text.HorizontalAlignment = HorizontalAlignment.Left;
                text.Text = tChannel.Name;

                Grid grid = new Grid();
                grid.Height = 30;
                grid.Children.Add(rect);
                grid.Children.Add(text);

                ListBoxItem channel = new ListBoxItem();
                channel.Margin = new Thickness(0, 5, 0, 0);
                channel.BorderBrush = System.Windows.Media.Brushes.Gray;
                channel.Content = grid;

                txtChannel.Content = grid;
                #endregion
                txtChannel.Cursor = Cursors.Hand;
                txtChannel.Name = "TextChannel" + tIndex++;
                items.Add(txtChannel);
            }

            return items.ToArray();
        }

        public void FillCurrentServerData(string data)
        {
            CurrentServer.Clear();
            string[] rows = data.Split(new string[] { "{$}" }, StringSplitOptions.None);
            if (rows[1] != "")
            {
                string[] vChannels = rows[1].Split(new string[] { "{#}" }, StringSplitOptions.None);
                for (int i = 0; i < vChannels.Length; i++)
                {
                    string[] users = vChannels[i].Split(new string[] { "{@}" }, StringSplitOptions.None);
                    if (users[0] != "")
                    {
                        var tempVoice = new VoiceChannel(users[0]);
                        for (int k = 1; k < users.Length; k++)
                        {
                            if (users[k] != "")
                            {
                                tempVoice.AddParticipant(users[k]);
                            }
                        }
                        CurrentServer.VoiceChannels.Add(tempVoice);
                    }
                }
            }
            if (rows[2] != "")
            {
                string[] tChannels = rows[2].Split(new string[] { "{#}" }, StringSplitOptions.None);
                for (int j = 1; j < tChannels.Length; j++)
                {
                    CurrentServer.TextChannels.Add(new TextChannel(tChannels[j]));
                }
            }
        }

        public ListBoxItem CreateUserXAML(User user)
        {
            ListBoxItem item = new ListBoxItem();
            Ellipse ellipse = new Ellipse();
            ellipse.Fill = new ImageBrush(new BitmapImage(new Uri(user.ImageUrl)));
            ellipse.Width = 40;
            ellipse.Height = 40;

            Ellipse ellipse1 = new Ellipse();
            ellipse1.Margin = new Thickness(-15, 30, 0, 0);
            ellipse1.Stroke = System.Windows.Media.Brushes.White;
            ellipse1.Width = 10;
            ellipse1.Height = 10;
            ellipse1.Fill = user.StatusColor;

            TextBlock textBlock = new TextBlock();
            textBlock.Text = user.Name;
            textBlock.VerticalAlignment = VerticalAlignment.Center;
            textBlock.Padding = new Thickness(20, 0, 0, 0);

            StackPanel stack = new StackPanel();
            stack.Orientation = Orientation.Horizontal;
            stack.Height = 50;
            stack.Children.Add(ellipse);
            stack.Children.Add(ellipse1);
            stack.Children.Add(textBlock);

            item.Content = stack;
            item.Name = "user" + user.Name;
            return item;
        }

        public void AddToUserListBox(ListBoxItem user)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Window.usersListBox.Items.Add(user);
            });
        }

        public void AddToUserListBox(ListBoxItem[] users)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach(var user in users)
                    Window.usersListBox.Items.Add(user);
            });
        }

        public void ClearUserListBox()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Window.usersListBox.Items.Clear();
            });
        }

        public void RemoveFromUserListBox(User user)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Window.usersListBox.Items.Remove(user);
            });
        }

        public int ListBoxSelectedIndex()
        {
            int res = -1;
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (usersListBox.SelectedIndex != -1)
                    res = usersListBox.SelectedIndex;
            });
            return res;
        }

        public string ListBoxSelectedItem()
        {
            string res = "";
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (usersListBox.SelectedItem != null)
                    res = ((ListBoxItem)usersListBox.SelectedItem).Name.ToString().Substring(4);
            });
            return res;
        }

        public void ConversationClear()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var border = conversationList.Items[0] as Border;
                var grid = border.Child as Grid;
                var tb = grid.Children[0] as TextBlock;
                string tempText = "";
                if (tb != null)
                    tempText = tb.Text;
                conversationList.Items.Clear();
                conversationList.Items.Add(CreateUserStatusBar(tempText));
            });
        }

        public void UpdateStatusBar(string text)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var border = conversationList.Items[0] as Border;
                var grid = border.Child as Grid;
                var tb = grid.Children[0] as TextBlock;
                var img = grid.Children[1] as Image;
                if (tb != null)
                {
                    tb.Text = text;
                    if (text == "")
                    {
                        img.IsEnabled = false;
                        img.Visibility = Visibility.Hidden;
                    }
                    else
                    {
                        img.IsEnabled = true;
                        img.Visibility = Visibility.Visible;
                    }
                }
            });
        }

        public Border CreateUserStatusBar(string text = "")
        {
            BrushConverter bc = new BrushConverter();

            TextBlock tb = new TextBlock();
            tb.Foreground = (Brush)bc.ConvertFromString("#FF9E9E9E");
            tb.VerticalAlignment = VerticalAlignment.Center;
            tb.HorizontalAlignment = HorizontalAlignment.Left;
            tb.Margin = new Thickness(20, 0, 0, 0);
            tb.Text = text;

            Image image = new Image();
            image.Cursor = Cursors.Hand;
            image.Width = 25;
            image.Height = 25;
            image.VerticalAlignment = VerticalAlignment.Center;
            image.HorizontalAlignment = HorizontalAlignment.Right;
            image.Margin = new Thickness(0, 0, 20, 0);
            image.Source = new BitmapImage(new Uri("/Resources/disconnect.png", UriKind.Relative));
            image.MouseDown += Disconnect_MouseDown;
            if(text != "")
            {
                image.IsEnabled = true;
                image.Visibility = Visibility.Visible;
            }
            else
            {
                image.IsEnabled = false;
                image.Visibility = Visibility.Hidden;
            }

            Grid grid = new Grid();
            grid.Children.Add(tb);
            grid.Children.Add(image);

            Border border = new Border();
            border.HorizontalAlignment = HorizontalAlignment.Stretch;
            border.Background = (Brush)bc.ConvertFromString("#FF4F4F4F");
            border.BorderBrush = (Brush)bc.ConvertFromString("#FF303030");
            border.BorderThickness = new Thickness(0, 0, 0, 2);
            border.CornerRadius = new CornerRadius(20, 20, 0, 0);
            border.Height = 35;
            border.Child = grid;

            return border;
        }

        private void Disconnect_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Client.CurrentVoice == null)
                return;

            Client.SendPacket.Send($"[leftvoice]{{$}}{Client.CurrentVoice}");
            int i = CurrentServer.VoiceChannels.FindIndex(x => x.Name == Client.CurrentVoice);
            CurrentServer.VoiceChannels[i].RemoveParticipant(Client.Name);

            Client.CurrentVoice = null;
            UpdateStatusBar("");
            Window.ClearUserListBox();
            Application.Current.Dispatcher.Invoke(() =>  // lastly update UI
            {
                Window.AddToUserListBox(Window.CreateServerXAML());
            });
            Voice.Disconnect();
        }

        public void UpdateUsername()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                usernameLabel.Content = Client.Name;
            });
        }

        public void UpdateUserImage()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var usrBit = new BitmapImage();
                usrBit.BeginInit();
                usrBit.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                usrBit.CacheOption = BitmapCacheOption.OnLoad;
                usrBit.UriSource = new Uri(Client.Image, UriKind.RelativeOrAbsolute);
                usrBit.EndInit();
                usrImg.ImageSource = usrBit;
            });
        }

        public static List<ViscordMessage> MessageList = new List<ViscordMessage>();
        public class ViscordMessage
        {
            public string ImageUrl { get; set; }
            public string OwnerName { get; set; }
            public HorizontalAlignment Alignment { get; set; }
            public string Message { get; set; }
            public string Timestamp { get; set; }
            public User Receiver { get; set; }

            public ViscordMessage(User user, string sender, string message, string timestamp)
            {
                Receiver = user;
                ImageUrl = (sender == user.Name) ? user.ImageUrl : Client.Image;
                OwnerName = sender;
                Message = message;
                Timestamp = timestamp;
                Alignment = (OwnerName == Client.Name) ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            }
        }

        // xaml in C# for fully customized UI Design
        public ListBoxItem listBoxItem(TextBlock textBlock, ViscordMessage msg)
        {
            var converter = new BrushConverter();

            ListBoxItem item = new ListBoxItem();

            item.HorizontalAlignment = msg.Alignment;
            Border border = new Border();
            border.BorderThickness = new Thickness(0, 3, 0, 0);
            border.Background = (Brush)converter.ConvertFromString("#FF1F1F1F");
            border.CornerRadius = new CornerRadius(20, 20, 20, 20);
            StackPanel stack1 = new StackPanel();
            stack1.Orientation = Orientation.Vertical;

            StackPanel stack2 = new StackPanel();
            stack2.Orientation = Orientation.Horizontal;

            Ellipse ellipse = new Ellipse();
            ellipse.VerticalAlignment = VerticalAlignment.Top;
            ellipse.HorizontalAlignment = HorizontalAlignment.Left;
            ellipse.Stroke = (Brush)converter.ConvertFromString("#FF646464");
            ellipse.Width = 45;
            ellipse.Height = 45;
            ellipse.Margin = new Thickness(15, 5, 0, 0);
            ellipse.Fill = new ImageBrush(new BitmapImage(new Uri(msg.ImageUrl)));
            StackPanel stack3 = new StackPanel();
            stack3.Orientation = Orientation.Vertical;

            TextBlock tb1 = new TextBlock();
            tb1.Text = msg.OwnerName;
            tb1.Padding = new Thickness(10, 5, 0, 0);
            tb1.HorizontalAlignment = HorizontalAlignment.Left;
            tb1.VerticalAlignment = VerticalAlignment.Top;
            tb1.Foreground = (Brush)converter.ConvertFromString("#FF787878");

            TextBlock tb2 = new TextBlock();
            tb2.Text = msg.Timestamp;
            tb2.Padding = new Thickness(10, 5, 0, 0);
            tb2.HorizontalAlignment = HorizontalAlignment.Left;
            tb2.VerticalAlignment = VerticalAlignment.Top;
            tb2.Foreground = (Brush)converter.ConvertFromString("#FF555555");

            stack3.Children.Add(tb1);
            stack3.Children.Add(tb2);

            stack2.Children.Add(ellipse);
            stack2.Children.Add(stack3);

            StackPanel stack4 = new StackPanel();
            stack4.Width = double.NaN;
            stack4.Orientation = Orientation.Horizontal;
            textBlock.MinWidth = 350;
            textBlock.Width = double.NaN;
            textBlock.Margin = new Thickness(10, 5, 0, 5);
            textBlock.TextWrapping = TextWrapping.Wrap;
            stack4.Children.Add(textBlock);

            stack1.Children.Add(stack2);
            stack1.Children.Add(stack4);

            border.Child = stack1;
            item.Content = border;
            return item;
        }

        public static string ConversationRequest = null;
        public async Task<List<ViscordMessage>> RequestConversation(string username)
        {
            MessageBuilder mb = new MessageBuilder();
            mb.Add("[requestconv]");
            mb.Add(username);
            Client.SendPacket.Send(mb.Message());
            while (ConversationRequest == null)
            {
                await Task.Delay(200);
            }
            var temp =  ConversationConverter.FromString(ConversationRequest);
            ConversationRequest = null;
            return temp;
        }

        public async void UpdateMessages(string username)
        {
            var user = User.FindUser(username);
            if (user == null)
            {
                user = new User(username);
                Users.Add(user);
            }

            if (user.Conversation.Count < 1) // if conversation clear request previous data if any
            {
                var conv = await Window.RequestConversation(username);
                if (conv == null)
                    return;
                user.Conversation = conv;
            }

            if (conversationList.Items.Count == 1) // if there is just that status bar in there add all conversation
            {
                prevMsg = null;
                foreach (var msg in user.Conversation)
                {
                    if (prevMsg != null)
                    {
                        if (prevMsg.OwnerName == msg.OwnerName && !prevMsg.Message.Contains(".gif"))
                        {
                            var temp = prevMsg.Message + Environment.NewLine + msg.Message;
                            var tmp_msg = new ViscordMessage(user, msg.OwnerName, temp, DateTime.Now.ToString("h:mm tt"));

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                ChangeLastMessage(listBoxItem(textBlockFromMsg(tmp_msg), tmp_msg));
                            });
                            prevMsg = tmp_msg;
                            continue;
                        }
                    }
                    var msg2 = new ViscordMessage(user, msg.OwnerName, msg.Message, DateTime.Now.ToString("h:mm tt"));

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        AddMessage(listBoxItem(textBlockFromMsg(msg2), msg2));
                    });
                    prevMsg = msg2;
                }
            }
            else // if there are already some messages just add newest one (last index one)
            {
                ViscordMessage msg = user.Conversation[user.Conversation.Count - 1];

                if (prevMsg != null)
                {
                    if (prevMsg.OwnerName == msg.OwnerName && !prevMsg.Message.Contains(".gif"))
                    {
                        var temp = prevMsg.Message + Environment.NewLine + msg.Message;
                        var tmp_msg = new ViscordMessage(user, msg.OwnerName, temp, DateTime.Now.ToString("h:mm tt"));

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ChangeLastMessage(listBoxItem(textBlockFromMsg(tmp_msg), tmp_msg));
                        });
                        prevMsg = tmp_msg;
                    }
                    else
                    {
                        var msg2 = new ViscordMessage(user, msg.OwnerName, msg.Message, DateTime.Now.ToString("h:mm tt"));

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            AddMessage(listBoxItem(textBlockFromMsg(msg2), msg2));
                        });
                        prevMsg = msg2;
                    }
                }
                else
                {
                    var msg2 = new ViscordMessage(user, msg.OwnerName, msg.Message, DateTime.Now.ToString("h:mm tt"));

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        AddMessage(listBoxItem(textBlockFromMsg(msg2), msg2));
                    });
                    prevMsg = msg2;
                }
            }
        }

        private void LinkOnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(e.Uri.ToString());
        }

        private TextBlock textBlockFromMsg(ViscordMessage msg)
        {
            List<string> imgExtensions = new List<string> { ".jpg", ".jpeg", ".bmp", ".gif", ".png" };
            TextBlock tb = new TextBlock();
            var links = Regex.Matches(msg.Message, @"(http|ftp|https):\/\/([\w\-_]+(?:(?:\.[\w\-_]+)+))([\w\-\.,@?^=%&amp;:/~\+#]*[\w\-\@?^=%&amp;/~\+#])?");
            if (links.Count > 0) // if we found any links
            {
                int linkCounter = 0;
                Run run = new Run();
                for (int k = 0; k < msg.Message.Length; k++)
                {
                    if (linkCounter < links.Count && k == links[linkCounter].Index)
                    {
                        string linkText = links[linkCounter].Value;

                        tb.Inlines.Add(run); // append already detected text
                        run = new Run();
                        Uri linkUri = new Uri(linkText);
                        var fileInfo = new FileInfo(linkUri.AbsolutePath);

                        if (imgExtensions.Contains(fileInfo.Extension)) // if url is an img display it
                        {
                            Image img = new Image();
                            img.MaxWidth = 330;
                            img.Stretch = Stretch.Fill;
                            img.Cursor = Cursors.Hand;

                            if (fileInfo.Extension == ".gif")
                                AnimationBehavior.SetSourceUri(img, linkUri);
                            else
                            {
                                BitmapImage bit = new BitmapImage(linkUri);
                                bit.CacheOption = BitmapCacheOption.OnLoad;
                                bit.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                                img.Source = bit;
                            }

                            img.Margin = new Thickness(0, 5, 0, 0);
                            tb.Inlines.Add(img);
                        }
                        else // just display the link
                        {
                            Run linkRun = new Run();
                            if (!string.IsNullOrWhiteSpace(fileInfo.Extension)) // if link is file just show filename
                                linkRun.Text = fileInfo.Name;
                            else
                                linkRun.Text = linkText;
                            Hyperlink link = new Hyperlink(linkRun); // add the hyperlink
                            link.NavigateUri = new Uri(linkText);
                            link.RequestNavigate += LinkOnRequestNavigate; // add click handler
                            tb.Inlines.Add(link);
                        }

                        k += (links[linkCounter].Length - 1); // continue with text after
                        linkCounter++;
                    }
                    else
                    {
                        run.Text += msg.Message[k];
                    }
                }
                tb.Inlines.Add(run);
                return tb;
            }
            else
            {
                tb.Text = msg.Message;
                return tb;
            }
        }

        private void ChangeLastMessage(object msg)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (conversationList.Items.Count < 1)
                    return;
                int index = conversationList.Items.Count - 1;
                conversationList.Items[index] = msg;
                if (scrollViewer != null)
                {
                    scrollViewer.ScrollToEnd();
                }
            });
        }

        private void AddMessage(object msg)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                conversationList.Items.Add(msg);
                if (scrollViewer != null)
                {
                    scrollViewer.ScrollToEnd();
                }
            });
        }

        private void SendMessage()
        {
            if (messageBox.Text == "")
            {
                return;
            }

            if (messageBox.Text.Length > 500)
            {
                MessageBox.Show("Message is too long (max 500 characters)");
                return;
            }

            string username = ListBoxSelectedItem();
            if (username == "")
            {
                MessageBox.Show("No user selected");
                return;
            }

            Client.SendPacket.Send($"[messageto]{{$}}{username}{{$}}{messageBox.Text}");

            var user = User.FindUser(username);
            if (user == null)
            {
                user = new User(username);
                Users.Add(user);
            }
            user.Conversation.Add(new ViscordMessage(Client.User, Client.Name, messageBox.Text, DateTime.Now.ToString("h:mm tt")));
            UpdateMessages(username);
            messageBox.Clear();
        }

        private User prevSelectedUser = null;
        private void usersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var addedItems = e.AddedItems;
            if (addedItems.Count > 0)
            {
                var selectedItem = addedItems[0];
                if (selectedItem != null && ((ListBoxItem)selectedItem).Name.Contains("user"))
                {
                    var username = ((ListBoxItem)selectedItem).Name.Substring(4);
                    var user = Users.Find(x => x.Name == username);

                    if (prevSelectedUser != null && prevSelectedUser != user)
                        ConversationClear();
                    UpdateMessages(user.Name);

                    prevSelectedUser = user;
                }
                else if(selectedItem != null && ((ListBoxItem)selectedItem).Name.Contains("VChannel"))
                {
                    // not safe rework code
                    int port = Int32.Parse(((ListBoxItem)selectedItem).Name.Substring(8)) + 23650;
                    if(!Connector.TryToConnectVoice(port))
                    {
                        MessageBox.Show("error connecting to voice");
                    }
                    else
                    {
                        int index = port - 23650;
                        if (index < 0)
                            return;

                        if (Client.CurrentVoice == CurrentServer.VoiceChannels[index].Name) // if we already in that channel
                            return;

                        if (Client.CurrentVoice != null) // if already in a channel leave it
                        {
                            int i = CurrentServer.VoiceChannels.FindIndex(x => x.Name == Client.CurrentVoice);
                            CurrentServer.VoiceChannels[i].RemoveParticipant(Client.Name);
                        }

                        CurrentServer.VoiceChannels[index].AddParticipant(Client.Name);
                        Client.CurrentVoice = CurrentServer.VoiceChannels[index].Name;

                        Client.SendPacket.Send($"[newvoice]{{$}}{Client.CurrentVoice}");

                        Window.ClearUserListBox();
                        Application.Current.Dispatcher.Invoke(() =>  // lastly update UI
                        {
                            Window.AddToUserListBox(Window.CreateServerXAML());
                        });
                        Window.UpdateStatusBar($"In voice: {Client.CurrentVoice}");

                        Voice.StartRecording();
                        Voice.StartReceiving();
                    }
                }
                else if(selectedItem != null && ((ListBoxItem)selectedItem).Name.Contains("TextChannel"))
                {
                    // add functionality for text channels
                    ConversationClear();
                }
            }
            else
            {
                ConversationClear();
            }
        }

        private void Viscord_Closed(object sender, EventArgs e)
        {
            Logout();
        }

        private void Viscord_Loaded(object sender, RoutedEventArgs e)
        {
            User.GetAllUsers();
            UpdateUsername();
            UpdateUserImage();
            AddMessage(CreateUserStatusBar());
            border = VisualTreeHelper.GetChild(conversationList, 0) as Decorator;
            if (border != null)
            {
                scrollViewer = border.Child as ScrollViewer;
            }
            timer.Tick += new EventHandler(timer_Tick);
            timer.Interval = new TimeSpan(0, 0, 1);
            newCmd.InputGestures.Add(new KeyGesture(Key.V, ModifierKeys.Control));
            messageBox.CommandBindings.Add(new CommandBinding(newCmd, PasteEventHandler));
            settings = new Settings();
            LoadAudioDevices();
            Voice.Settings = settings;
            LoadToast();
        }

        private void LoadAudioDevices()
        {
            for (int deviceId = 0; deviceId < WaveIn.DeviceCount; deviceId++)
            {
                var deviceInfo = WaveIn.GetCapabilities(deviceId);
                settings.inputCombo.Items.Add(deviceInfo.ProductName);
            }

            for (int deviceId = 0; deviceId < WaveOut.DeviceCount; deviceId++)
            {
                var deviceInfo = WaveOut.GetCapabilities(deviceId);
                settings.outputCombo.Items.Add(deviceInfo.ProductName);
            }
        }

        int counter = 0;
        DateTime coolDown = new DateTime();
        System.Windows.Threading.DispatcherTimer timer = new System.Windows.Threading.DispatcherTimer();

        private void PasteEventHandler(object e, ExecutedRoutedEventArgs args)
        {
            if (Clipboard.ContainsImage())
            {
                List<string> files = new List<string>();
                BitmapSource image = Clipboard.GetImage();
                JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                byte[] byteArray = null;
                encoder.QualityLevel = 100;
                using (MemoryStream stream = new MemoryStream())
                {
                    encoder.Frames.Add(BitmapFrame.Create(image));
                    encoder.Save(stream);
                    byteArray = stream.ToArray();
                }
                SendFile(files, byteArray);
            }
            else if (Clipboard.ContainsFileDropList())
            {
                var files = Clipboard.GetFileDropList();
                List<string> final = new List<string>();
                foreach (var file in files)
                {
                    final.Add(file);
                }
                SendFile(final);
            }
            else if(Clipboard.ContainsText())
            {
                messageBox.Text += Clipboard.GetText();
            }
        }

        private void LoadToast()
        {
            ToastNotificationManagerCompat.OnActivated += toastArgs =>
            {
                // Obtain the arguments from the notification
                ToastArguments args = ToastArguments.Parse(toastArgs.Argument);
                var user = User.FindUser(args.Get("username"));
                if (user == null)
                    return;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    //this.WindowState = WindowState.Normal;
                    this.Activate();
                });
            };
        }

        private void messageBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (counter == 0)
                {
                    coolDown = DateTime.Now;
                }

                if (counter >= 3)
                {
                    var diff = DateTime.Now.Subtract(coolDown);
                    if (diff.TotalSeconds < 3) // if 3 msgs in less than 3 sec
                    {
                        messageBox.TextAlignment = TextAlignment.Center;
                        messageBox.IsEnabled = false;
                        time = 6;
                        timer.Start();
                    }
                    counter = -1;
                }

                counter++;
                SendMessage();
            }
        }

        private int time = 0;
        private void timer_Tick(object sender, EventArgs e)
        {
            if (time > 1)
            {
                time--;
                messageBox.Text = $"Cooldown: {time}";
            }
            else
            {
                timer.Stop();
                counter = 0;
                messageBox.Clear();
                messageBox.IsEnabled = true;
                messageBox.TextAlignment = TextAlignment.Left;
            }
        }

        private bool mic = true;
        private void micBut_Click(object sender, RoutedEventArgs e)
        {
            mic = !mic;
            string img_name = "";
            if (mic)
            {
                img_name = @"pack://application:,,,/Resources/microphone.png";
            }
            else
            {
                img_name = @"pack://application:,,,/Resources/disabled_mic.png";
            }
            micImg.ImageSource = new BitmapImage(new Uri(img_name, UriKind.RelativeOrAbsolute));
        }

        private bool head = true;
        private void headBut_Click(object sender, RoutedEventArgs e)
        {
            head = !head;
            string img_name = "";
            if (head)
            {
                img_name = @"pack://application:,,,/Resources/headphones.png";
            }
            else
            {
                img_name = @"pack://application:,,,/Resources/headphones_disabled.png";
            }
            headImg.ImageSource = new BitmapImage(new Uri(img_name, UriKind.RelativeOrAbsolute));
        }

        private async void SendFile(List<string> files, byte[] byteArray = null)
        {
            string username = ListBoxSelectedItem();
            FileInfo file = null;
            if (byteArray == null)
            {
                if (files.Count > 1)
                {
                    MessageBox.Show("You can only send 1 file at a time.");
                    return;
                }
                if (!File.Exists(files[0]))
                    return;
                byte[] data = File.ReadAllBytes(files[0]);
                if (data.Length > (100 * 1000000))
                {
                    MessageBox.Show("Max upload size is 100mb");
                    return;
                }
                else if (data.Length < 1)
                {
                    MessageBox.Show("Error. Empty temp file");
                    return;
                }
                if (username == "")
                {
                    MessageBox.Show("No user selected");
                    return;
                }
                file = new FileInfo(files[0]);

                Client.SendPacket.Send($"[fileto]{{$}}{username}{{$}}{file.Name}{{$}}{data.Length}");
                await Task.Delay(30);
                Client.SendPacket.Send(data);
            }
            else
            {
                Client.SendPacket.Send($"[fileto]{{$}}{username}{{$}}temp.jpeg{{$}}{byteArray.Length}");
                await Task.Delay(30);
                Client.SendPacket.Send(byteArray);
            }

            while (FileResponse == null)
            {
                await Task.Delay(200);
            }

            if (FileResponse.Contains("[error]"))
            {
                string err = FileResponse.Split(new string[] { "{$}" }, StringSplitOptions.None)[1];
                MessageBox.Show(err);
                return;
            }

            var user = User.FindUser(username);
            if (user == null)
            {
                user = new User(username);
                Users.Add(user);
            }
            user.Conversation.Add(new ViscordMessage(Client.User, Client.Name, FileResponse, DateTime.Now.ToString("h:mm tt")));
            UpdateMessages(username);
            FileResponse = null;
        }

        private static string FileResponse = null;
        private void conversationList_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                List<string> data = new List<string>();
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                data.AddRange(files);
                SendFile(data);
            }
        }

        private void hoverButtons(object sender, MouseEventArgs e)
        {
            Border border = sender as Border;
            if((border.Child as TextBlock) != null && (border.Child as TextBlock).Text == "X")
            {
                border.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 162, 36, 36));
                return;
            }
            border.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 79, 79, 79));
        }

        private bool maximizedEarlier = false;
        private void buttonsMouseDown(object sender, MouseButtonEventArgs e)
        {
            Border border = sender as Border;
            switch(border.Name)
            {
                case "minimizeButton":
                    this.WindowState = WindowState.Minimized;
                    break;
                case "maximizeButton":
                    if (this.WindowState == WindowState.Normal)
                    {
                        this.MaxHeight = SystemParameters.MaximumWindowTrackHeight;
                        this.WindowState = WindowState.Maximized;
                        maximizedEarlier = true;
                    }
                    else
                    {
                        this.WindowState = WindowState.Normal;
                        this.Top = oldTop;
                        this.Left = oldLeft;
                    }
                    break;
                case "closeButton":
                    this.Close();
                    break;
            }
        }

        private void leaveButtons(object sender, MouseEventArgs e)
        {
            Border border = sender as Border;
            border.Background = null;
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (maximizedEarlier)
            {
                maximizedEarlier = false;
                return;
            }

            if (e.ChangedButton == MouseButton.Left && !maximizedEarlier)
            {
                if (this.WindowState == WindowState.Maximized)
                {
                    var point = PointToScreen(e.MouseDevice.GetPosition(this));

                    Left = point.X - (RestoreBounds.Width * 0.5);
                    Top = point.Y;

                    this.WindowState = WindowState.Normal;
                }
                this.DragMove();
            }
        }

        private double oldTop = 300.0, oldLeft = 300.0;

        private void CallUser(object sender, RoutedEventArgs e)
        {
            var user = ListBoxSelectedItem();
            Client.SendPacket.Send($"[callto]{{$}}{user}");
        }

        private void usersListBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            //if (usersListBox.SelectedItem != null && e.ChangedButton == MouseButton.Right)
            //{
            //    userContextMenu.PlacementTarget = usersListBox.SelectedItem as ListBoxItem;
            //    userContextMenu.IsOpen = true;
            //}
        }

        public Settings settings;
        public static WaveInEvent wave;
        public static WaveOutEvent waveOut;
        public static BufferedWaveProvider provider;

        private void SettingsClick(object sender, RoutedEventArgs e)
        {
            settings.Show();
        }

        private void serverListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var addedItems = e.AddedItems;
            if (addedItems.Count > 0)
            {
                ListBoxItem selectedItem = (ListBoxItem)addedItems[0];
                if (selectedItem.Name == "Friends")
                {
                    ClearUserListBox();
                    foreach (var user in Users)
                    {
                        Window.AddToUserListBox(Window.CreateUserXAML(user));
                    }
                }
                else
                {
                    Client.SendPacket.Send($"[getserver]{{$}}{selectedItem.Name}");
                }
            }
        }

        private void Viscord_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if(this.WindowState == WindowState.Maximized)
            {
                oldTop = this.Top;
                oldLeft = this.Left;
            }
        }
    }
}

