using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace RockWithBoA
{
    namespace M3u8Downloader
    {
        /// <summary>
        /// Interaction logic for App.xaml
        /// </summary>
        public partial class App : Application
        {
            public string M3u8FileDir { get; set; }

            private void Application_Startup(object sender, StartupEventArgs e)
            {
                if (e.Args.Length < 1)
                {
                    MessageBox.Show("M3u8 File directory is not specified.");
                }
                else
                {
                    DirectoryInfo dirInfo = new DirectoryInfo(e.Args[0]);
                    if (dirInfo.Exists)
                    {
                        this.Properties["M3u8FileDir"] = e.Args[0];
                    }
                    else
                    {
                        MessageBox.Show("M3u8 File directory is not a valid directory. Input the directory manually again.");
                    }
                }
            }
        }
    }
}
