using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace PSUtilsLauncher
{
    class Program
    {
        private string MyDirectory;
        private Properties.Settings Settings;
        private string PSUtilsPath;
        private string ConEmuExecutable;
        private string ConEmuPath;
        private string PowerShellPath;

        static void Main(string[] args)
        {
            new Program().Run();

        }
        private void Run()
        {
            this.MyDirectory = AppDomain.CurrentDomain.BaseDirectory;
            this.Settings = PSUtilsLauncher.Properties.Settings.Default;
            this.PSUtilsPath = Path.Combine(this.MyDirectory, "PSUtils");
            this.ConEmuPath = Path.Combine(this.MyDirectory, "ConEmu");
            this.ConEmuExecutable = Path.Combine(this.ConEmuPath, IntPtr.Size == 8 ? "ConEmu64.exe" : "ConEmu.exe");
            this.PowerShellPath = Path.Combine(Environment.GetEnvironmentVariable("WINDIR"), @"system32\WindowsPowerShell\v1.0\PowerShell.exe");

            this.EnsureRepository();

            if(!File.Exists(this.ConEmuExecutable))
            {
                if(MessageBox.Show("ConEmu not found. Do you want to download int? (otherwise, default Windows console will be used)", "PSUtils Launcher", MessageBoxButtons.YesNo) == DialogResult.No)
                {
                    this.StartWithPowerShell();
                    return;
                }

                this.DownloadConemu();
            }

            this.StartWithConEmu();

        }
        private void DownloadConemu()
        {
            var tempFile = Path.GetTempFileName();

            this.ExecuteProcess(this.PowerShellPath, string.Format("-Command Invoke-WebRequest '{0}' -OutFile '{1}'", this.Settings.ConEmuDownloadUrl, tempFile))
                .WaitForExit();

            try
            {

                using(var sevenZip = new SevenZip.SevenZipExtractor(tempFile))
                {
                    if(!Directory.Exists(this.ConEmuPath))
                        Directory.CreateDirectory(this.ConEmuPath);

                    sevenZip.ExtractArchive(this.ConEmuPath);
                }
            }
            finally
            {
                if(File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        private void EnsureRepository()
        {
            if(!Directory.Exists(this.PSUtilsPath))
            {
                LibGit2Sharp.Repository.Clone(this.Settings.PSUtilsRepository, this.PSUtilsPath);
            }
        }

        private void StartWithConEmu()
        {
            var arguments = string.Format("/LoadCfgFile \"{0}\"", Path.Combine(this.PSUtilsPath, "ConEmu.xml"));

            this.ExecuteProcess(this.ConEmuExecutable, arguments);
       }

        private void StartWithPowerShell()
        {
            
            var arguments = string.Format("-ExecutionPolicy {0} -NoExit -Command \"Import-Module '{1}'\"", this.Settings.ExecutionPolicy, this.PSUtilsPath);

            this.ExecuteProcess(this.PowerShellPath, arguments);

        }

        private Process ExecuteProcess(string program, string arguments)
        {
            var psi = new ProcessStartInfo();
            psi.FileName = program;
            psi.Arguments = arguments;
            psi.UseShellExecute = false;

            return Process.Start(psi);
        }
    }
}
