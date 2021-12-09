using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Viscord_Client;

namespace Viscord
{
    public static class Voice
    {
        public static UdpClient UdpClient { get; set; }
        public static int Port { get; set; }
        public static Settings Settings { get; set; }
        public static bool Disconnected { get; set; }

        public static IPEndPoint endPoint;

        public static void Connect(int port)
        {
            try
            {
                Port = port;
                Disconnected = false;
                endPoint = new IPEndPoint(Dns.GetHostAddresses("zotrix.ddns.net")[0], Port);
                UdpClient = new UdpClient();
                UdpClient.Connect(endPoint);
                StartVoice();
            }
            catch { }
        }

        public static void StartVoice()
        {
            MainWindow.wave = new WaveInEvent();

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Settings.inputCombo.SelectedIndex == -1)
                {
                    Settings.inputCombo.SelectedIndex = 0;
                    MainWindow.wave.DeviceNumber = 0;
                }
            });
            
            MainWindow.waveOut = new WaveOutEvent();

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Settings.outputCombo.SelectedIndex == -1)
                {
                    Settings.inputCombo.SelectedIndex = 0;
                    MainWindow.waveOut.DeviceNumber = 0;
                }
            });

            MainWindow.wave.WaveFormat = new WaveFormat(44100, WaveIn.GetCapabilities(0).Channels);
            MainWindow.provider = new BufferedWaveProvider(MainWindow.wave.WaveFormat);
            MainWindow.waveOut.Init(MainWindow.provider);
            MainWindow.wave.DataAvailable += Wave_DataAvailable;
        }

        public static void StartRecording()
        {
            MainWindow.wave.StartRecording();
        }

        private static void Wave_DataAvailable(object sender, WaveInEventArgs e)
        {
            Send(e.Buffer, e.BytesRecorded);
        }

        public static void StartReceiving()
        {
            try
            {
                if (Disconnected)
                    return;
                UdpClient.BeginReceive(new AsyncCallback(ReceiveCallback), null);
            }
            catch { }
        }

        private static void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                byte[] receiveData = UdpClient.EndReceive(ar, ref endPoint);

                if (receiveData.Length > 1)
                {
                    MainWindow.provider.AddSamples(receiveData, 0, receiveData.Length);
                    MainWindow.waveOut.Play();
                    StartReceiving();
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        public static void Disconnect()
        {
            Disconnected = true;
            MainWindow.waveOut.Stop();
            MainWindow.wave.StopRecording();
        }

        public static void Send(byte[] data, int length)
        {
            try
            {
                UdpClient.Send(data, length);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
