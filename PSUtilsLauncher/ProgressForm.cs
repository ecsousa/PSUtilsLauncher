using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PSUtilsLauncher
{
    public partial class ProgressForm: Form
    {
        delegate void CloseWindowDelegate();
        delegate void SetProgressDelegate(string title, int progress);

        private string Title;
        private Task Task;
        private CloseWindowDelegate CloseWindow;
        private SetProgressDelegate SetProgress;
        private const int CP_NOCLOSE_BUTTON = 0x200;
        private ManualResetEvent CloseSignal;
        private System.Threading.Tasks.Task WaiterTask;
        private int WaitMilliseconds;
 
        protected ProgressForm()
        {
            InitializeComponent();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams myCp = base.CreateParams;
                myCp.ClassStyle = myCp.ClassStyle | CP_NOCLOSE_BUTTON;
                return myCp;
            }
        }

        public ProgressForm(string title, Action<Action<string, int>> task) :
            this(title, task, 0)
        { }

        public ProgressForm(string title, Action<Action<string, int>> task, int waitMilliseconds) :
            this()
        {
            this.Title = title;
            this.CloseSignal = new ManualResetEvent(false);
            this.CloseWindow = new CloseWindowDelegate(this.Close);
            this.SetProgress = new SetProgressDelegate(this.SetProgressImpl);
            this.WaitMilliseconds = waitMilliseconds;

            this.Task = new Task(() =>
            {
                task(InvokeSetProgress);

                this.CloseSignal.Set();
            });
        }

        private void InvokeSetProgress(string action, int progress)
        {
            this.Invoke(this.SetProgress, action, progress);

        }

        private void SetProgressImpl(string action, int progress)
        {
            this.lblAction.Text = action;
            this.prgProgess.Value = progress;
        }

        private void ProgressForm_Load(object sender, EventArgs e)
        {
            this.Text = this.Title;
            this.lblAction.Text = this.Title;
            this.Task.Start();

            if(this.WaitMilliseconds > 0 && this.Task.Wait(this.WaitMilliseconds))
                this.Close();
            else
            {
                this.WaiterTask = new Task(() =>
                {
                    this.CloseSignal.WaitOne();
                    this.Invoke(this.CloseWindow);
                });

                this.WaiterTask.Start();
            }
        }

    }
}
