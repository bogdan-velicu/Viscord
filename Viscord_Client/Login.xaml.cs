using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static Viscord_Client.MainWindow;

namespace Viscord_Client
{
    public partial class Login : Window
    {
        public static string Response { get; set; }

        public Login()
        {
            InitializeComponent();
            Response = null;
        }

        private void TextBlock_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Register reg = new Register();
            reg.Show();
            this.Hide();
        }

        private async void LoginUser()
        {
            if (username.Text == "" || password.Password == "")
            {
                MessageBox.Show("Username or password is empty");
                return;
            }
            Mouse.OverrideCursor = Cursors.Wait;
            bool con = true;
            if (MainWindow.Connector._connectingSocket == null ||       //connect tcp
                !MainWindow.Connector._connectingSocket.Connected)
                con = await MainWindow.Connector.TryToConnect();
            if (!con)
            {
                Mouse.OverrideCursor = null;
                return;
            }
            string loginData = $"{username.Text}{{$}}{password.Password}";
            MainWindow.Client.SendPacket.Send(loginData, PacketID.Login);
            while (Response == null)
            {
                await Task.Delay(300);
            }
            if (Response.Contains("success"))
            {
                Mouse.OverrideCursor = null;
                string img = Response.Split(new string[] { "{$}" }, StringSplitOptions.None)[1];
                MainWindow.Client.Name = username.Text;
                MainWindow.Client.User = new MainWindow.User(username.Text, true, img);
                MainWindow.Client.Image = img;
                MainWindow.Client.IsInCall = false;
                MainWindow main = new MainWindow();
                main.Show();
                this.Hide();
            }
            else {
                if (Response.Contains("error"))
                {
                    string[] res = Response.Split(new string[] { "{$}" }, StringSplitOptions.None);
                    MessageBox.Show(res[1]);
                    Response = null;
                }
                else
                {
                    MessageBox.Show("Unknown error");
                    Response = null;
                }
            }
            Mouse.OverrideCursor = null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            LoginUser();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            MainWindow.Logout();
        }

        private async void username_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Tab)
            {
                await Task.Delay(10);
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    password.Focus();
                }));
            }
        }

        private void password_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Enter)
            {
                LoginUser();
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(10);
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                username.Focus();
            }));
        }
    }
}
