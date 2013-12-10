﻿namespace Antilli
{
    partial class MainGui
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
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.mn_File = new System.Windows.Forms.ToolStripMenuItem();
            this.mn_File_Open = new System.Windows.Forms.ToolStripMenuItem();
            this.mn_File_Save = new System.Windows.Forms.ToolStripMenuItem();
            this.mn_sep = new System.Windows.Forms.ToolStripSeparator();
            this.mn_File_Exit = new System.Windows.Forms.ToolStripMenuItem();
            this.mn_View = new System.Windows.Forms.ToolStripMenuItem();
            this.mn_View_Models = new System.Windows.Forms.ToolStripMenuItem();
            this.mn_View_Textures = new System.Windows.Forms.ToolStripMenuItem();
            this.mn_Tools = new System.Windows.Forms.ToolStripMenuItem();
            this.mn_Tools_ExportOBJ = new System.Windows.Forms.ToolStripMenuItem();
            this.ElementHostGUI = new System.Windows.Forms.Integration.ElementHost();
            this.GUI = new Antilli.WPFGui();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mn_File,
            this.mn_View,
            this.mn_Tools});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Padding = new System.Windows.Forms.Padding(6, 2, 0, 0);
            this.menuStrip1.Size = new System.Drawing.Size(859, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // mn_File
            // 
            this.mn_File.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mn_File_Open,
            this.mn_File_Save,
            this.mn_sep,
            this.mn_File_Exit});
            this.mn_File.Name = "mn_File";
            this.mn_File.Size = new System.Drawing.Size(37, 22);
            this.mn_File.Text = "File";
            // 
            // mn_File_Open
            // 
            this.mn_File_Open.Name = "mn_File_Open";
            this.mn_File_Open.Size = new System.Drawing.Size(115, 22);
            this.mn_File_Open.Text = "Open ...";
            // 
            // mn_File_Save
            // 
            this.mn_File_Save.Enabled = false;
            this.mn_File_Save.Name = "mn_File_Save";
            this.mn_File_Save.Size = new System.Drawing.Size(115, 22);
            this.mn_File_Save.Text = "Save";
            // 
            // mn_sep
            // 
            this.mn_sep.Name = "mn_sep";
            this.mn_sep.Size = new System.Drawing.Size(112, 6);
            // 
            // mn_File_Exit
            // 
            this.mn_File_Exit.Name = "mn_File_Exit";
            this.mn_File_Exit.Size = new System.Drawing.Size(115, 22);
            this.mn_File_Exit.Text = "Exit";
            // 
            // mn_View
            // 
            this.mn_View.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mn_View_Models,
            this.mn_View_Textures});
            this.mn_View.Name = "mn_View";
            this.mn_View.Size = new System.Drawing.Size(44, 22);
            this.mn_View.Text = "View";
            // 
            // mn_View_Models
            // 
            this.mn_View_Models.Name = "mn_View_Models";
            this.mn_View_Models.Size = new System.Drawing.Size(118, 22);
            this.mn_View_Models.Text = "Models";
            // 
            // mn_View_Textures
            // 
            this.mn_View_Textures.Name = "mn_View_Textures";
            this.mn_View_Textures.Size = new System.Drawing.Size(118, 22);
            this.mn_View_Textures.Text = "Textures";
            // 
            // mn_Tools
            // 
            this.mn_Tools.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mn_Tools_ExportOBJ});
            this.mn_Tools.Name = "mn_Tools";
            this.mn_Tools.Size = new System.Drawing.Size(48, 22);
            this.mn_Tools.Text = "Tools";
            // 
            // mn_Tools_ExportOBJ
            // 
            this.mn_Tools_ExportOBJ.Name = "mn_Tools_ExportOBJ";
            this.mn_Tools_ExportOBJ.Size = new System.Drawing.Size(147, 22);
            this.mn_Tools_ExportOBJ.Text = "Export to .OBJ";
            // 
            // ElementHostGUI
            // 
            this.ElementHostGUI.AutoSize = true;
            this.ElementHostGUI.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ElementHostGUI.Location = new System.Drawing.Point(0, 24);
            this.ElementHostGUI.Name = "ElementHostGUI";
            this.ElementHostGUI.Size = new System.Drawing.Size(859, 488);
            this.ElementHostGUI.TabIndex = 4;
            this.ElementHostGUI.Text = "elementHost1";
            this.ElementHostGUI.Child = this.GUI;
            // 
            // MainGui
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            this.ClientSize = new System.Drawing.Size(859, 512);
            this.Controls.Add(this.ElementHostGUI);
            this.Controls.Add(this.menuStrip1);
            this.DoubleBuffered = true;
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "MainGui";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Antilli";
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem mn_File;
        private System.Windows.Forms.ToolStripMenuItem mn_File_Open;
        private System.Windows.Forms.ToolStripMenuItem mn_File_Save;
        private System.Windows.Forms.ToolStripSeparator mn_sep;
        private System.Windows.Forms.ToolStripMenuItem mn_File_Exit;
        private System.Windows.Forms.ToolStripMenuItem mn_View;
        private System.Windows.Forms.ToolStripMenuItem mn_View_Models;
        private System.Windows.Forms.ToolStripMenuItem mn_View_Textures;
        private System.Windows.Forms.ToolStripMenuItem mn_Tools;
        private System.Windows.Forms.ToolStripMenuItem mn_Tools_ExportOBJ;
        private System.Windows.Forms.Integration.ElementHost ElementHostGUI;
        private WPFGui GUI;
    }
}

