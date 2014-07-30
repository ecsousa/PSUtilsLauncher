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

        protected ProgressForm()
        {
            InitializeComponent();
        }

        private const int CP_NOCLOSE_BUTTON = 0x200;
        private ManualResetEvent Signal;
        private System.Threading.Tasks.Task WaiterTask;
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
            this()
        {
            this.Title = title;
            this.Signal = new ManualResetEvent(false);
            this.CloseWindow = new CloseWindowDelegate(this.Close);
            this.SetProgress = new SetProgressDelegate(this.SetProgressImpl);

            this.Task = new Task(() =>
            {
                task(InvokeSetProgress);

                this.Signal.Set();
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

            if(this.Task.Wait(1500))
                this.Close();
            else
            {
                this.WaiterTask = new Task(() =>
                {
                    this.Signal.WaitOne();
                    this.Invoke(this.CloseWindow);
                });

                this.WaiterTask.Start();
            }
        }

    }
}
