using SharpCompress.Archives;
using SharpCompress.Common;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Viscord_Autoupdater
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("    ▄   ▄█    ▄▄▄▄▄   ▄█▄    ████▄ █▄▄▄▄ ██▄   ");
            Console.WriteLine("     █  ██   █     ▀▄ █▀ ▀▄  █   █ █  ▄▀ █  █  ");
            Console.WriteLine("█     █ ██ ▄  ▀▀▀▀▄   █   ▀  █   █ █▀▀▌  █   █ ");
            Console.WriteLine(" █    █ ▐█  ▀▄▄▄▄▀    █▄  ▄▀ ▀████ █  █  █  █  ");
            Console.WriteLine("  █  █   ▐            ▀███▀          █   ███▀  ");
            Console.WriteLine("   █▐                               ▀          ");
            Console.WriteLine("   ▐                                           ");

            Console.WriteLine();

            if(!Directory.Exists("Viscord"))
            {
                Directory.CreateDirectory("Viscord");
                using (WebClient wc = new WebClient())
                {
                    var file = wc.DownloadData("http://zotrix.ddns.net:6746/viscord/viscord.zip");
                    File.WriteAllBytes(@"Viscord/viscord.zip", file);
                    ZipFile.ExtractToDirectory(@"Viscord/viscord.zip", @"Viscord", true);
                    if (MainExeFound())
                    {
                        Console.WriteLine();
                        Console.WriteLine("Opening client...");
                        Thread.Sleep(1000);
                        Process.Start(@"Viscord/Viscord.exe");
                        Process.Start(@"Viscord/Viscord.exe");
                    }
                    return;
                }
            }

            Console.WriteLine("Checking for updates...");

            using (WebClient wc = new WebClient())
            {
                var files = Directory.GetFiles(@"Viscord");
                var file_server = wc.DownloadData("http://zotrix.ddns.net:6746/viscord/viscord.zip");
                string md5_server = getMD5(new MemoryStream(file_server));
                string file_local = "";
                // if old zip is present + old client files
                if(File.Exists(@"Viscord/viscord.zip") && files.Length > 1)
                {
                    file_local = @"Viscord/viscord.zip";
                }
                // else if there's only old client and no zip
                else if(files.Length > 1)
                {
                    ZipFile.CreateFromDirectory(@"Viscord", @"Viscord/viscord.zip");
                    file_local = @"Viscord/viscord.zip";
                }
                // else if zip and old client are missing just fresh install
                else
                {
                    File.WriteAllBytes(@"Viscord/viscord.zip", file_server);
                    ZipFile.ExtractToDirectory(@"Viscord/viscord.zip", @"Viscord");
                    if(MainExeFound())
                        Process.Start(@"Viscord/Viscord.exe");
                    return;
                }
                string md5_client = getMD5(new MemoryStream(File.ReadAllBytes(file_local)));
                if(md5_client != md5_server)
                {
                    File.WriteAllBytes(@"Viscord/viscord.zip", file_server);
                    ZipFile.ExtractToDirectory(@"Viscord/viscord.zip", @"Viscord", true);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine();
                    Console.WriteLine("Updated succesfully!");
                }
                if(MainExeFound())
                {
                    Console.WriteLine();
                    Console.WriteLine("Opening client...");
                    Thread.Sleep(1000);
                    Process.Start(@"Viscord/Viscord.exe");
                }
            }
        }

        private static bool MainExeFound()
        {
            if (!File.Exists(@"Viscord/Viscord.exe"))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine();
                Console.WriteLine("Error missing main executable");
                Console.WriteLine("Try deleting [Viscord] folder and run autoupdater again");
                Console.ReadKey();
                return false;
            }
            return true;
        }

        private static string getMD5(MemoryStream stream)
        {
            using (var md5 = MD5.Create())
            {
                return Encoding.Default.GetString(md5.ComputeHash(stream));
            }
        }
    }
}
