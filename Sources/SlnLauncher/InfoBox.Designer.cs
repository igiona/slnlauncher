
namespace SlnLauncher
{
    partial class InfoBox
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this._pictureBox = new System.Windows.Forms.PictureBox();
            this._closeButton = new System.Windows.Forms.Button();
            this._tableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this._messageLabel = new System.Windows.Forms.Label();
            this._invisibleLabel = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this._pictureBox)).BeginInit();
            this._tableLayoutPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // _pictureBox
            // 
            this._pictureBox.Location = new System.Drawing.Point(3, 3);
            this._pictureBox.MinimumSize = new System.Drawing.Size(48, 48);
            this._pictureBox.Name = "_pictureBox";
            this._pictureBox.Size = new System.Drawing.Size(48, 48);
            this._pictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this._pictureBox.TabIndex = 0;
            this._pictureBox.TabStop = false;
            // 
            // _closeButton
            // 
            this._closeButton.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this._tableLayoutPanel.SetColumnSpan(this._closeButton, 2);
            this._closeButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._closeButton.Location = new System.Drawing.Point(3, 57);
            this._closeButton.Name = "_closeButton";
            this._closeButton.Size = new System.Drawing.Size(277, 24);
            this._closeButton.TabIndex = 2;
            this._closeButton.Text = "Close";
            this._closeButton.UseVisualStyleBackColor = true;
            // 
            // _tableLayoutPanel
            // 
            this._tableLayoutPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this._tableLayoutPanel.AutoSize = true;
            this._tableLayoutPanel.ColumnCount = 2;
            this._tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 54F));
            this._tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this._tableLayoutPanel.Controls.Add(this._messageLabel, 1, 0);
            this._tableLayoutPanel.Controls.Add(this._closeButton, 0, 1);
            this._tableLayoutPanel.Controls.Add(this._pictureBox, 0, 0);
            this._tableLayoutPanel.Location = new System.Drawing.Point(2, 13);
            this._tableLayoutPanel.Name = "_tableLayoutPanel";
            this._tableLayoutPanel.RowCount = 2;
            this._tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this._tableLayoutPanel.Size = new System.Drawing.Size(283, 84);
            this._tableLayoutPanel.TabIndex = 4;
            // 
            // _messageLabel
            // 
            this._messageLabel.Location = new System.Drawing.Point(57, 0);
            this._messageLabel.Name = "_messageLabel";
            this._messageLabel.Size = new System.Drawing.Size(83, 20);
            this._messageLabel.TabIndex = 6;
            this._messageLabel.Text = "label1";
            // 
            // _invisibleLabel
            // 
            this._invisibleLabel.AutoSize = true;
            this._invisibleLabel.Location = new System.Drawing.Point(127, -2);
            this._invisibleLabel.MaximumSize = new System.Drawing.Size(400, 0);
            this._invisibleLabel.Name = "_invisibleLabel";
            this._invisibleLabel.Size = new System.Drawing.Size(83, 15);
            this._invisibleLabel.TabIndex = 5;
            this._invisibleLabel.Text = "_invisibleLabel";
            this._invisibleLabel.Visible = false;
            // 
            // InfoBox
            // 
            this.AcceptButton = this._closeButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.CancelButton = this._closeButton;
            this.ClientSize = new System.Drawing.Size(286, 100);
            this.ControlBox = false;
            this.Controls.Add(this._invisibleLabel);
            this.Controls.Add(this._tableLayoutPanel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "InfoBox";
            this.Padding = new System.Windows.Forms.Padding(10);
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Title";
            this.TopMost = true;
            ((System.ComponentModel.ISupportInitialize)(this._pictureBox)).EndInit();
            this._tableLayoutPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox _pictureBox;
        private System.Windows.Forms.Button _closeButton;
        private System.Windows.Forms.TableLayoutPanel _tableLayoutPanel;
        private System.Windows.Forms.Label _invisibleLabel;
        private System.Windows.Forms.Label _messageLabel;
    }
}
