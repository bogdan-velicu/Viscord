using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Viscord
{
    public partial class CallWindow : Window
    {
        public bool bAnswer = false;
        private TimeSpan time;
        private DispatcherTimer timer;
        public CallWindow(string img_url, string username)
        {
            InitializeComponent();

            Application.Current.Dispatcher.Invoke(() =>
            {
                var usrBit = new BitmapImage();
                usrBit.BeginInit();
                usrBit.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                usrBit.CacheOption = BitmapCacheOption.OnLoad;
                usrBit.UriSource = new Uri(img_url, UriKind.RelativeOrAbsolute);
                usrBit.EndInit();
                usrImg.ImageSource = usrBit;

                callingText.Text = username + " is calling you";
            });

            time = TimeSpan.FromSeconds(10);

            timer = new DispatcherTimer(new TimeSpan(0, 0, 1), DispatcherPriority.Normal, delegate
            {
                if (time == TimeSpan.Zero)
                {
                    timer.Stop();
                    this.Close();
                }
                time = time.Add(TimeSpan.FromSeconds(-1));
            }, Application.Current.Dispatcher);

            timer.Start();
        }

        private void Answer(object sender, MouseButtonEventArgs e)
        {
            bAnswer = true;
            this.Close();
        }

        private void Decline(object sender, MouseButtonEventArgs e)
        {
            this.Close();
        }

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            if(e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }
    }
}
