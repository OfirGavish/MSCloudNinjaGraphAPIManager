using System;
using System.Drawing;
using System.Windows.Forms;

namespace MSCloudNinjaGraphAPI
{
    partial class MainForm : Form
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
            this.mainContent = new System.Windows.Forms.Panel();
            this.headerPanel = new System.Windows.Forms.Panel();
            this.SuspendLayout();
            // 
            // mainContent
            // 
            this.mainContent.Location = new System.Drawing.Point(12, 62);
            this.mainContent.Name = "mainContent";
            this.mainContent.Size = new System.Drawing.Size(776, 376);
            this.mainContent.TabIndex = 2;
            // 
            // headerPanel
            // 
            this.headerPanel.Location = new System.Drawing.Point(12, 12);
            this.headerPanel.Name = "headerPanel";
            this.headerPanel.Size = new System.Drawing.Size(776, 50);
            this.headerPanel.TabIndex = 1;
            // 
            // MainForm
            // 
            this.Controls.Add(this.mainContent);
            this.Controls.Add(this.headerPanel);
            this.ResumeLayout(false);

        }

        #endregion

        protected Panel mainContent;
        protected Panel headerPanel;
    }
}
