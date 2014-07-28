using LibGit2Sharp;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.IO.Compression;

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
        private List<string> FilesToClean;

        static void Main(string[] args)
        {
            if(args.Length > 0 && args[0] == "clean")
            {
                Clean(args);
                return;
            }

            AppDomain.CurrentDomain.AssemblyResolve += OnResolveAssembly;

            try
            {
                new Program().Run();
            }
            catch(Exception ex)
            {
                MessageBox.Show(string.Format("Error: {0}", ex.Message), "PSUtils Launcher");
            }

        }

        private static Assembly OnResolveAssembly(object sender, ResolveEventArgs args)
        {
            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            AssemblyName assemblyName = new AssemblyName(args.Name);
            string path = assemblyName.Name + ".dll";
            if(assemblyName.CultureInfo.Equals(CultureInfo.InvariantCulture) == false)
            {
                path = String.Format(@"{0}\{1}", assemblyName.CultureInfo, path);
            }
            using(Stream stream = executingAssembly.GetManifestResourceStream(path))
            {
                if(stream == null)
                    return null;

                using(var gzip = new GZipStream(stream, CompressionMode.Decompress))
                using(var memory = new MemoryStream())
                {
                    gzip.CopyTo(memory);

                    byte[] assemblyRawBytes = new byte[memory.Length];
                    memory.Seek(0, SeekOrigin.Begin);
                    memory.Read(assemblyRawBytes, 0, assemblyRawBytes.Length);
                    return Assembly.Load(assemblyRawBytes);
                }

            }
        }

        private void Run()
        {
            this.MyDirectory = AppDomain.CurrentDomain.BaseDirectory;
            this.Settings = PSUtilsLauncher.Properties.Settings.Default;
            this.PSUtilsPath = Path.Combine(this.MyDirectory, "PSUtils");
            this.ConEmuPath = Path.Combine(this.MyDirectory, "ConEmu");
            this.ConEmuExecutable = Path.Combine(this.ConEmuPath, (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") ?? "x86").ToLower() == "amd64" ? "ConEmu64.exe" : "ConEmu.exe");
            this.PowerShellPath = Path.Combine(Environment.GetEnvironmentVariable("WINDIR"), @"system32\WindowsPowerShell\v1.0\PowerShell.exe");
            this.FilesToClean = new List<string>();

            try
            {
                if(!this.EnsureRepository())
                    return;

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
            finally
            {
                if(this.FilesToClean.Count > 0)
                    this.LaunchCleaner();
            }

        }

        private void DownloadConemu()
        {
            var tempFile = Path.GetTempFileName();

            var sevenZipPath = Path.Combine(this.PSUtilsPath, @"7z\7z.exe");

            if(!Directory.Exists(this.ConEmuPath))
                Directory.CreateDirectory(this.ConEmuPath);

            try
            {
                this.ExecuteProcess(this.PowerShellPath,
                    string.Format("-Command Invoke-WebRequest '{0}' -OutFile '{1}'; pushd '{2}'; & '{3}' x '{1}'",
                        this.Settings.ConEmuDownloadUrl,
                        tempFile,
                        this.ConEmuPath,
                        sevenZipPath
                    ))
                    .WaitForExit();

            }
            finally
            {
                if(File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        private bool EnsureRepository()
        {
            this.WriteGitBinary();

            if(!Directory.Exists(this.PSUtilsPath))
            {
                if(MessageBox.Show("PSUtils repository not found. Do you want to download it? (it may take a while)", "PSUtils Launcher", MessageBoxButtons.YesNo) == DialogResult.No)
                    return false;
            }
            else
            {
                if(!Repository.IsValid(this.PSUtilsPath))
                {
                    if(MessageBox.Show("PSUtils repository not valid. Do you want to DELETE current directory and re-download it? (it may take a while)", "PSUtils Launcher", MessageBoxButtons.YesNo) == DialogResult.No)
                        return false;

                    Directory.Delete(this.PSUtilsPath, true);
                }
                else
                {
                    return this.UpdateRepository();
                }
            }

            try
            {
                Repository.Clone(this.Settings.PSUtilsRepository, this.PSUtilsPath);
            }
            catch
            {
                if(Directory.Exists(this.PSUtilsPath))
                    Directory.Delete(this.PSUtilsPath, true);

                throw;
            }

            return true;
        }

        private void WriteGitBinary()
        {
            var prefix = "gitlib:";

            foreach(var resource in typeof(Program).Assembly.GetManifestResourceNames()
                .Where(r => r.StartsWith(prefix)))
            {

                this.WriteBinary(resource, resource.Substring(prefix.Length));
            }

        }

        private void WriteBinary(string resource, string fileName)
        {
            using(var stream = typeof(Program).Assembly.GetManifestResourceStream(resource))
            using(var gzip = new GZipStream(stream, CompressionMode.Decompress))
            using(var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                gzip.CopyTo(fileStream);

            this.FilesToClean.Add(fileName);
        }

        private bool UpdateRepository()
        {
            if(this.Settings.AutoUpdateModule)
            {
                using(var repo = new Repository(this.PSUtilsPath))
                {
                    repo.Network.Pull(new Signature("PSUtilsLancher", "", DateTimeOffset.Now),
                        new PullOptions
                        {
                            FetchOptions = null,
                            MergeOptions = new MergeOptions
                            {
                                FileConflictStrategy = CheckoutFileConflictStrategy.Theirs,
                            }
                        });

                }
            }

            return true;
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

        private void LaunchCleaner()
        {
            var programName = new Uri(typeof(Program).Assembly.CodeBase).Segments.Last();
            this.ExecuteProcess(
                Process.GetCurrentProcess().MainModule.FileName,
                string.Format("clean {0} {1}", Process.GetCurrentProcess().Id, string.Join(" ", this.FilesToClean))
                );
        }
        private static void Clean(string[] args)
        {
            var parentPid = int.Parse(args[1]);

            try
            {
                Process.GetProcessById(parentPid);
            }
            catch(ArgumentException)
            { }


            foreach(var file in args.Skip(2))
                File.Delete(file);
        }

    }
}
