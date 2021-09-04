using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SlnLauncher
{
    public partial class InfoBox : Form
    {
        bool _topMost = false;
        public InfoBox(string title, string message, Bitmap icon, bool topMost = true)
        {
            InitializeComponent();
            Text = title;
            _invisibleLabel.Text = message;

            _pictureBox.Image = icon;
            _topMost = topMost;
            Load += InfoBox_Load;
        }

        private void InfoBox_Load(object sender, EventArgs e)
        {
            _messageLabel.Width = _invisibleLabel.Width;
            if (_invisibleLabel.Height < 48)
            {
                _messageLabel.Height = 48;
            }
            else
            {
                _messageLabel.Height = _invisibleLabel.Height;
            }
            _messageLabel.Text = _invisibleLabel.Text;
            TopMost = _topMost;
            TopLevel = TopMost;
            if (TopMost)
            {
                Focus();
                Activate();
            }
        }
    }
}
