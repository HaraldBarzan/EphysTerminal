
namespace TINS.Ephys.Display
{
	partial class RealTimeSpectrumDisplay
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
			this.ckbUseLog = new System.Windows.Forms.CheckBox();
			this.SuspendLayout();
			// 
			// ckbUseLog
			// 
			this.ckbUseLog.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.ckbUseLog.AutoSize = true;
			this.ckbUseLog.ForeColor = System.Drawing.Color.White;
			this.ckbUseLog.Location = new System.Drawing.Point(405, 240);
			this.ckbUseLog.Name = "ckbUseLog";
			this.ckbUseLog.Size = new System.Drawing.Size(67, 19);
			this.ckbUseLog.TabIndex = 0;
			this.ckbUseLog.Text = "Fisher Z";
			this.ckbUseLog.UseVisualStyleBackColor = true;
			// 
			// RealTimeSpectrumDisplay
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.BackColor = System.Drawing.SystemColors.ControlDarkDark;
			this.ClientSize = new System.Drawing.Size(484, 271);
			this.Controls.Add(this.ckbUseLog);
			this.MinimumSize = new System.Drawing.Size(500, 310);
			this.Name = "RealTimeSpectrumDisplay";
			this.Text = "RealTimeSpectrumDisplay";
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.CheckBox ckbUseLog;
	}
}