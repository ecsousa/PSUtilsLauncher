using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace PSUtilsLauncher
{
    public class Updater
    {
        public static bool Check()
        {
            var tempPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PSUtils");
            var tempFile = Path.Combine(tempPath, "old-version");

            if(!Directory.Exists(tempPath))
                Directory.CreateDirectory(tempPath);
            else if(File.Exists(tempFile))
            {
                System.Threading.Thread.Sleep(300);
                File.Delete(tempFile);
            }

            using(var client = new WebClient()) 
            {
                string versionString;
                
                try
                {
                    versionString = client.DownloadString(global::PSUtilsLauncher.Properties.Settings.Default.SelfUpdateInfo);
                }
                catch(Exception)
                {
                    return false;
                }

                var serverVersion = new[] { versionString.Split(' ') }
                .Select(arr => new
                {
                    Version = Version.Parse(arr[0]),
                    Url = arr[1]
                })
                .First();

                if(serverVersion.Version > typeof(Updater).Assembly.GetName().Version)
                {
                    var destination = typeof(Updater).Assembly.Location;
                    byte[] data = null;

                    var progess = new ProgressForm("Updating PSUtilsLauncher", setProgress =>
                    {
                        var completed = new ManualResetEvent(false);
                        Exception downloadError = null;

                        client.DownloadProgressChanged += (sender, e) =>
                        {
                            setProgress("Downloading...", e.ProgressPercentage);
                        };

                        client.DownloadDataCompleted += (sender, e) =>
                        {
                            downloadError = e.Error;
                            data = e.Result;
                            completed.Set();
                        };

                        client.DownloadDataAsync(new Uri(serverVersion.Url));

                        completed.WaitOne();

                        if(downloadError != null)
                            throw new Exception(downloadError.Message, downloadError);

                    }).ShowDialog();

                    File.Move(destination, tempFile);
                    File.WriteAllBytes(destination, data);

                    var psi = new ProcessStartInfo()
                    {
                        Arguments = Environment.CommandLine,
                        FileName = destination,
                        UseShellExecute = false,
                        WorkingDirectory = Directory.GetCurrentDirectory()
                    };

                    Process.Start(psi);

                    return true;
                }
                else
                    return false;

            }

        }

    }
}
