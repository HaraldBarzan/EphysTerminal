namespace StimulationScreen
{
	partial class SkiaScreen
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
			if (!sk.IsDisposed)
				sk.Dispose();

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
			this.sk = new SkiaSharp.Views.Desktop.SKControl();
			this.SuspendLayout();
			// 
			// sk
			// 
			this.sk.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.sk.Location = new System.Drawing.Point(0, 0);
			this.sk.Name = "sk";
			this.sk.Size = new System.Drawing.Size(1920, 1080);
			this.sk.TabIndex = 0;
			this.sk.Text = "skControl1";
			// 
			// SkiaScreen
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(1920, 1080);
			this.Controls.Add(this.sk);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
			this.Name = "SkiaScreen";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "SkiaScreen";
			this.ResumeLayout(false);

		}

		#endregion

		private SkiaSharp.Views.Desktop.SKControl sk;
	}
}