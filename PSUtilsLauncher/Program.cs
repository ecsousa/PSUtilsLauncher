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
using System.Threading;
using System.Text.RegularExpressions;

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
        private string BinariesPath;
        private string MessagesFile;
        private List<string> Messages;
        private bool NetworkExecution;
        private string AppData;

        static void Main(string[] args)
        {
            try
            {

                if(args.Length > 0 && args[0] == "clean")
                {
                    Clean(args);
                    return;
                }

                AppDomain.CurrentDomain.AssemblyResolve += OnResolveAssembly;

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
            this.NetworkExecution = new Uri(this.MyDirectory).IsUnc;
            this.Settings = PSUtilsLauncher.Properties.Settings.Default;
            this.PSUtilsPath = Path.Combine(this.MyDirectory, "PSUtils");
            this.ConEmuPath = Path.Combine(this.MyDirectory, "ConEmu");
            this.ConEmuExecutable = Path.Combine(this.ConEmuPath, (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") ?? "x86").ToLower() == "amd64" ? "ConEmu64.exe" : "ConEmu.exe");
            this.PowerShellPath = Path.Combine(Environment.GetEnvironmentVariable("WINDIR"), @"system32\WindowsPowerShell\v1.0\PowerShell.exe");
            this.FilesToClean = new List<string>();
            this.Messages = new List<string>();
            this.BinariesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PSUtilsLauncher\\Binaries");
            this.AppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PSUtils");
            this.MessagesFile = Path.Combine(this.AppData, "messages.txt");

            Environment.SetEnvironmentVariable("PATH", string.Format("{0};{1}", this.BinariesPath, Environment.GetEnvironmentVariable("PATH")));

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

            using(var client = new WebClient())
            {
                var progess = new ProgressForm("Downloading ConEmu", setProgress =>
                {
                    var completed = new ManualResetEvent(false);

                    client.DownloadProgressChanged += (sender, e) =>
                    {
                        setProgress("Downloading...", e.ProgressPercentage);
                    };

                    client.DownloadFileCompleted += (sender, e) =>
                    {
                        completed.Set();
                    };

                    client.DownloadFileAsync(new Uri(this.Settings.ConEmuDownloadUrl), tempFile);

                    completed.WaitOne();

                }).ShowDialog();
            }

            var sevenZipPath = Path.Combine(this.PSUtilsPath, @"7z\7z.exe");

            if(!Directory.Exists(this.ConEmuPath))
                Directory.CreateDirectory(this.ConEmuPath);

            try
            {
                this.ExecuteProcess(sevenZipPath,
                    string.Format("x \"{0}\"",
                        tempFile
                    ),
                    this.ConEmuPath,
                    false)
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

            if(Directory.Exists(this.PSUtilsPath))
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
                new ProgressForm("Cloning PSUtils repository", setProgress =>
                {
                    Repository.Clone(this.Settings.PSUtilsRepository, this.PSUtilsPath, new CloneOptions
                    {
                        OnTransferProgress = progress =>
                        {
                            setProgress("Transfering repository [1/2]", 100 * progress.ReceivedObjects / progress.TotalObjects);
                            return true;
                        },
                        OnCheckoutProgress = (path, completedSteps, totalSteps) =>
                        {
                            setProgress("Checking out files [2/2]", 100 * completedSteps / totalSteps);
                        }
                    });

                }).ShowDialog();


                this.Messages.Add(string.Format("PSUtils repository has just been cloned to {0}. There is some other nice tools you can install.", this.PSUtilsPath));
                this.Messages.Add(string.Format(" - Install-Vim function will install Vim and gVim (portable) at {0}Vim", this.MyDirectory));
                this.Messages.Add(string.Format(" - Install-Sysinternals function will install Sysinternals tools at {0}sysinternals. PSUtils", this.MyDirectory));
                this.Messages.Add(string.Format(" will create aliases for these tools at startup if whey are present.", this.MyDirectory));
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
            if(!Directory.Exists(this.BinariesPath))
                Directory.CreateDirectory(this.BinariesPath);

            var output = Path.Combine(this.BinariesPath, fileName);

            using(var stream = typeof(Program).Assembly.GetManifestResourceStream(resource))
            using(var gzip = new GZipStream(stream, CompressionMode.Decompress))
            using(var fileStream = new FileStream(output, FileMode.Create, FileAccess.Write))
                gzip.CopyTo(fileStream);

            this.FilesToClean.Add(output);
        }

        private bool UpdateRepository()
        {
            if(!this.NetworkExecution && this.Settings.AutoUpdateModule)
            {
                using(var repo = new Repository(this.PSUtilsPath))
                {
                    var currentCommit = repo.Commits.First();

                    try
                    {
                        new ProgressForm("Pulling PSUtils repository", setProgress =>
                        {
                            repo.Network.Pull(new Signature("PSUtilsLancher", "", DateTimeOffset.Now),
                            new PullOptions
                            {
                                FetchOptions = new FetchOptions {
                                    OnTransferProgress = progress =>
                                    {
                                        setProgress("Transfering repository [1/2]", 100 * progress.ReceivedObjects / progress.TotalObjects);
                                        return true;
                                    },
                                },
                                MergeOptions = new MergeOptions
                                {
                                    FileConflictStrategy = CheckoutFileConflictStrategy.Normal,
                                    OnCheckoutProgress = (path, completedSteps, totalSteps) =>
                                    {
                                        setProgress("Checking out files [2/2]", 100 * completedSteps / totalSteps);
                                    }
                                },
                            });
                        }, 2500).ShowDialog();

                        bool initialMessage = false;

                        foreach(var commit in repo.Commits.TakeWhile(c => c.Id != currentCommit.Id))
                        {
                            if(!initialMessage)
                            {
                                this.Messages.Add("PSUtils module has been updated:");
                                initialMessage = true;
                            }

                            var endlineRegex = new Regex("[\r\n]+$");

                            this.Messages.Add(string.Format(" - {0}: {1}", commit.Id.Sha.Substring(0, 7), endlineRegex.Replace(commit.Message, string.Empty)));
                        }
                    }
                    catch(Exception ex)
                    {
                        this.Messages.Add(string.Format("Error updateing PSUtils repository: {0}", ex.Message));
                        this.Messages.Add(string.Format("Exception Type: {0}", ex.GetType().FullName));
                        this.Messages.Add(string.Format("StackTrace: {0}", ex.StackTrace));
                    }
                }
            }

            return true;
        }

        private void StartWithConEmu()
        {
            var arguments = string.Format("/LoadCfgFile \"{0}\"", Path.Combine(this.PSUtilsPath, "ConEmu.xml"));

            this.WriteMessages();
            this.ExecuteProcess(this.ConEmuExecutable, arguments, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), true);
        }

        private void StartWithPowerShell()
        {
            var arguments = string.Format("-ExecutionPolicy {0} -NoExit -Command \"Import-Module '{1}'\"", this.Settings.ExecutionPolicy, this.PSUtilsPath);

            this.WriteMessages();
            this.ExecuteProcess(this.PowerShellPath, arguments, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), true);

        }

        private void WriteMessages()
        {
            this.AssureAppData();

            if(this.Messages.Count > 0)
            {
                File.AppendAllLines(this.MessagesFile, this.Messages);
            }
        }

        private void AssureAppData()
        {
            if(!Directory.Exists(this.AppData))
                Directory.CreateDirectory(this.AppData);
        }

        private Process ExecuteProcess(string program, string arguments)
        {
            return this.ExecuteProcess(program, arguments, null, false);
        }
        private Process ExecuteProcess(string program, string arguments, string workingFolder, bool bringToFront)
        {
            var psi = new ProcessStartInfo();
            psi.FileName = program;
            psi.Arguments = arguments;
            psi.UseShellExecute = false;
            if(bringToFront)
                psi.WindowStyle = ProcessWindowStyle.Maximized;

            if(!string.IsNullOrWhiteSpace(workingFolder))
                psi.WorkingDirectory = workingFolder;

            return Process.Start(psi);
        }

        private void LaunchCleaner()
        {
            var programName = new Uri(typeof(Program).Assembly.CodeBase).Segments.Last();
            this.ExecuteProcess(
                Process.GetCurrentProcess().MainModule.FileName,
                string.Format("clean {0} {1}",
                    Process.GetCurrentProcess().Id,
                    string.Join(" ", this.FilesToClean.Select(file => string.Format("\"{0}\"", file)))
                )
            );
        }
        private static void Clean(string[] args)
        {
            var parentPid = int.Parse(args[1]);

            try
            {
                Process.GetProcessById(parentPid).WaitForExit();
            }
            catch(ArgumentException)
            { }

            foreach(var file in args.Skip(2))
            {
                try
                {
                    File.Delete(file);
                }
                catch(Exception ex)
                {
                    MessageBox.Show(string.Format("Could not delete file '{0}': {1}", file, ex.Message), "PSUtils Launcher");
                }
            }
        }

    }
}
