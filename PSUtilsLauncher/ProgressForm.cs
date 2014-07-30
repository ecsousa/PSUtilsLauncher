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
        private string Title;
        private Task Task;
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
            this.Task = new Task(() =>
            {
                task(SetProgress);

                this.Signal.Set();
            });
        }

        private void SetProgress(string action, int progress)
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
                    this.Close();
                });

                this.WaiterTask.Start();
            }
        }
    }
}
