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
        public static bool Connected { get; set; }
        public static IAsyncResult currentAsyncRes { get; set; }

        public static IPEndPoint endPoint;

        public static void Connect(int port)
        {
            try
            {
                Port = port;
                Connected = true;
                endPoint = new IPEndPoint(Dns.GetHostAddresses("zotrix.ddns.net")[0], Port);
                UdpClient = new UdpClient();
                UdpClient.Connect(endPoint);
                StartVoice();
            }
            catch { }
        }

        public static void StartVoice(int inDevice = 0, int outDevice = 0)
        {
            MainWindow.wave = new WaveInEvent();
            MainWindow.wave.DeviceNumber = inDevice;
            
            MainWindow.waveOut = new WaveOutEvent();
            MainWindow.waveOut.DeviceNumber = outDevice;

            MainWindow.wave.WaveFormat = new WaveFormat(44100, WaveIn.GetCapabilities(0).Channels);
            MainWindow.provider = new BufferedWaveProvider(MainWindow.wave.WaveFormat);
            MainWindow.provider.DiscardOnBufferOverflow = true;
            MainWindow.waveOut.Init(MainWindow.provider);
            MainWindow.wave.DataAvailable += Wave_DataAvailable;
        }

        public static void RefreshAudioDevices(int inDevice, int outDevice)
        {
            MainWindow.waveOut.Stop();
            MainWindow.wave.StopRecording();
            MainWindow.wave.DataAvailable -= Wave_DataAvailable;
            StartVoice(inDevice, outDevice);
            StartRecording();
            StartReceiving();
        }

        public static void StartRecording()
        {
            MainWindow.wave.StartRecording();
        }

        private static void Wave_DataAvailable(object sender, WaveInEventArgs e)
        {
            if(MainWindow.Microphone)
                Send(e.Buffer, e.BytesRecorded);
        }

        public static void StartReceiving()
        {
            try
            {
                if (!Connected)
                    return;
                currentAsyncRes = UdpClient.BeginReceive(new AsyncCallback(ReceiveCallback), null);
            }
            catch { }
        }

        private static void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                if (ar != currentAsyncRes)
                    return;

                byte[] receiveData = UdpClient.EndReceive(ar, ref endPoint);

                if (receiveData.Length > 1)
                {
                    if (MainWindow.Headphones)
                    {
                        MainWindow.provider.AddSamples(receiveData, 0, receiveData.Length);
                        MainWindow.waveOut.Play();
                    }
                }
                StartReceiving();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        public static void Disconnect()
        {
            Connected = false;
            MainWindow.waveOut.Stop();
            MainWindow.wave.StopRecording();
        }

        public static void Send(byte[] data, int length)
        {
            try
            {
                if (!Connected) return;
                UdpClient.Send(data, length);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
