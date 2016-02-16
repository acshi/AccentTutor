namespace AccentTutor
{
    partial class AccentTutorFrm
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
            this.spectrumDisplay = new AccentTutor.SpectrumDisplay();
            this.SuspendLayout();
            // 
            // spectrumDisplay
            // 
            this.spectrumDisplay.Dock = System.Windows.Forms.DockStyle.Fill;
            this.spectrumDisplay.Location = new System.Drawing.Point(0, 0);
            this.spectrumDisplay.Name = "spectrumDisplay";
            this.spectrumDisplay.Size = new System.Drawing.Size(560, 294);
            this.spectrumDisplay.TabIndex = 0;
            // 
            // AccentTutorFrm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(560, 294);
            this.Controls.Add(this.spectrumDisplay);
            this.Name = "AccentTutorFrm";
            this.Text = "Accent Tutor";
            this.ResumeLayout(false);

        }

        #endregion

        private AccentTutor.SpectrumDisplay spectrumDisplay;
    }
}

