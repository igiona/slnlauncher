using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SlnLauncher
{
    partial class ProgressDialog : Form
    {
        private bool _loaded = false;

        public ProgressDialog(string title, int maxProgressValue)
        {
            InitializeComponent();
            this.Text = title;
            this.progressBar.Value = 0;
            this.progressBar.Maximum = maxProgressValue;
            this.Load += ProgressDialog_Load;
        }

        public bool IsLoaded => _loaded;

        public void IncreaseProgressByValue(int v)
        {
            var action = new Action(() =>
            {
                this.progressBar.Value += v;
                progressBar.Invalidate();
                progressBar.Refresh();
                Refresh();
                Application.DoEvents();
            });

            if (InvokeRequired)
            {
                Invoke(action);
            }
            else
            {
                action();
            }
        }

        public new void Close()
        {
            var action = new Action(() =>
            {
                base.Close();
            });

            if (InvokeRequired)
            {
                Invoke(action);
            }
            else
            {
                action();
            }
        }

        public double Percentage { get { return progressBar.Value / (double)progressBar.Maximum * 100.0; } }
        public void IncrementProgress()
        {
            IncreaseProgressByValue(1);
        }

        private void ProgressDialog_Load(object sender, EventArgs e)
        {
            _loaded = true;
        }
    }
}
