using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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

namespace Viscord_Client
{
    public partial class Register : Window
    {
        public static string Response { get; set; }

        public Register()
        {
            InitializeComponent();
            Response = null;
        }

        private async void RegisterUser()
        {
            if (pass1.Password != pass2.Password)
            {
                MessageBox.Show("Passwords must match!");
                return;
            }
            if(username.Text.Contains(" "))
            {
                MessageBox.Show("Username can't contain spaces");
                return;
            }
            if (username.Text == "" || pass1.Password == "")
            {
                MessageBox.Show("Username or password is empty");
                return;
            }
            Mouse.OverrideCursor = Cursors.Wait;
            bool con = false;
            if (MainWindow.Connector._connectingSocket == null ||
                !MainWindow.Connector._connectingSocket.Connected)
                con = await MainWindow.Connector.TryToConnect();
            if (!con)
            {
                Mouse.OverrideCursor = null;
                return;
            }
            // if no image added
            string path = "";
            if (usrBitmap.UriSource.OriginalString != "http://zotrix.ddns.net:6746/avatars/default.png")
            {
                path = usrBitmap.UriSource.AbsolutePath.ToString();
            }
            if (path != "" && !File.Exists(path))
            {
                MessageBox.Show("Invalid image file path");
                Mouse.OverrideCursor = null;
                return;
            }
            var img = new Bitmap(path);
            if (img.Width > 1000 || img.Height > 1000)
            {
                MessageBox.Show("Image too large (max: 1000x1000)");
                Mouse.OverrideCursor = null;
                return;
            }
            byte[] img_bytes = null;
            if (path != "")
                img_bytes = File.ReadAllBytes(path);

            string registerData = $"[register]{{$}}{username.Text}{{$}}{pass1.Password}{{$}}{img_bytes}";
            MainWindow.Client.SendPacket.Send(registerData);
            while (Response == null)
            {
                await Task.Delay(300);
            }
            if (Response.Contains("[register_success]"))
            {
                string img_url = Response.Split(new string[] { "{$}" }, StringSplitOptions.None)[1];
                Mouse.OverrideCursor = null;
                MainWindow.Client.Name = username.Text;
                MainWindow.Client.Image = img_url;
                MainWindow.Client.IsInCall = false;
                MainWindow.Client.User = new MainWindow.User(username.Text, true, img_url);
                MainWindow main = new MainWindow();
                main.Show();
                this.Hide();
            }
            else if (Response.Contains("[register_fail]"))
            {
                Mouse.OverrideCursor = null;
                string[] res = Response.Split(new string[] { "{$}" }, StringSplitOptions.None);
                MessageBox.Show(res[1]);
                Response = null;
                return;
            }
            Mouse.OverrideCursor = null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            RegisterUser();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            MainWindow.Logout();
        }

        private void TextBlock_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Login log = new Login();
            this.Hide();
            log.Show();
        }

        private async void username_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Tab)
            {
                await Task.Delay(10);
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    pass1.Focus();
                }));
            }
        }

        private async void pass1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab)
            {
                await Task.Delay(10);
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    pass2.Focus();
                }));
            }
        }

        private void pass2_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                RegisterUser();
            }
        }

        private void Ellipse_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();

                openFileDialog.Title = "Open Image";
                openFileDialog.Filter = "Image File|*.bmp; *.gif; *.jpg; *.jpeg; *.png;";

                if (openFileDialog.ShowDialog() == true)
                {
                    usrBitmap = new BitmapImage();
                    usrBitmap.BeginInit();
                    usrBitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    usrBitmap.CacheOption = BitmapCacheOption.OnLoad;
                    usrBitmap.UriSource = new Uri(openFileDialog.FileName, UriKind.RelativeOrAbsolute);
                    usrBitmap.EndInit();
                    usrImg.ImageSource = usrBitmap;

                    opacityCircle.Opacity = 0;

                    plusText.Opacity = 0;
                }
            }
            else if(e.ChangedButton == MouseButton.Right)
            {
                usrBitmap = new BitmapImage();
                usrBitmap.BeginInit();
                usrBitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                usrBitmap.CacheOption = BitmapCacheOption.OnLoad;
                usrBitmap.UriSource = new Uri("http://zotrix.ddns.net:6746/avatars/default.png", UriKind.RelativeOrAbsolute);
                usrBitmap.EndInit();
                usrImg.ImageSource = usrBitmap;

                opacityCircle.Opacity = 1;

                plusText.Opacity = 1;

                changeImgText.Opacity = 0;
            }
        }

        private void Circle_MouseEnter(object sender, MouseEventArgs e)
        {
            if(usrBitmap.UriSource.OriginalString != "http://zotrix.ddns.net:6746/avatars/default.png")
            {
                opacityCircle.Opacity = 0.5;
                changeImgText.Opacity = 1;
            }
        }

        private void Circle_MouseLeave(object sender, MouseEventArgs e)
        {
            if(opacityCircle.Opacity == 0.5)
            {
                opacityCircle.Opacity = 0;
                changeImgText.Opacity = 0;
            }
        }
    }
}
