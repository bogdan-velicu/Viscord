using NAudio.Wave;
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
using Viscord_Client;

namespace Viscord
{
    public partial class Settings : Window
    {
        public Settings()
        {
            InitializeComponent();
        }

        private void hoverButtons(object sender, MouseEventArgs e)
        {
            Border border = sender as Border;
            border.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 162, 36, 36));
        }

        private void buttonsMouseDown(object sender, MouseButtonEventArgs e)
        {
            MainWindow.wave = new WaveInEvent();
            int inIndex = inputCombo.SelectedIndex;
            if(inIndex != -1)
                MainWindow.wave.DeviceNumber = inIndex;
            MainWindow.waveOut = new WaveOutEvent();
            int outIndex = inputCombo.SelectedIndex;
            if(outIndex != -1)
                MainWindow.waveOut.DeviceNumber = outIndex;
            this.Visibility = Visibility.Hidden;
        }

        private void leaveButtons(object sender, MouseEventArgs e)
        {
            Border border = sender as Border;
            border.Background = null;
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }
    }
}
