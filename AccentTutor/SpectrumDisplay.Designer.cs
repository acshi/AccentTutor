namespace AccentTutor
{
    partial class SpectrumDisplay
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.nextVowelBtn = new System.Windows.Forms.Button();
            this.completionLbl = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // nextVowelBtn
            // 
            this.nextVowelBtn.Location = new System.Drawing.Point(3, 3);
            this.nextVowelBtn.Name = "nextVowelBtn";
            this.nextVowelBtn.Size = new System.Drawing.Size(144, 23);
            this.nextVowelBtn.TabIndex = 0;
            this.nextVowelBtn.Text = "Record \"a\" as in bard";
            this.nextVowelBtn.UseVisualStyleBackColor = true;
            this.nextVowelBtn.Click += new System.EventHandler(this.nextVowelBtn_Click);
            // 
            // completionLbl
            // 
            this.completionLbl.Location = new System.Drawing.Point(3, 29);
            this.completionLbl.Name = "completionLbl";
            this.completionLbl.Size = new System.Drawing.Size(144, 25);
            this.completionLbl.TabIndex = 1;
            this.completionLbl.Text = "0% Complete";
            this.completionLbl.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // SpectrumDisplay
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.completionLbl);
            this.Controls.Add(this.nextVowelBtn);
            this.Name = "SpectrumDisplay";
            this.Resize += new System.EventHandler(this.SpectrumDisplay_Resize);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button nextVowelBtn;
        private System.Windows.Forms.Label completionLbl;
    }
}
