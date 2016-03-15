namespace AccentTutor {
    partial class AccentTutorFrm {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.vowelListBox = new System.Windows.Forms.ListBox();
            this.languageListBox = new System.Windows.Forms.ListBox();
            this.vowelDisplay = new AccentTutor.VowelDisplay();
            this.SuspendLayout();
            // 
            // vowelListBox
            // 
            this.vowelListBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.vowelListBox.FormattingEnabled = true;
            this.vowelListBox.ItemHeight = 20;
            this.vowelListBox.Location = new System.Drawing.Point(376, 220);
            this.vowelListBox.Name = "vowelListBox";
            this.vowelListBox.Size = new System.Drawing.Size(95, 204);
            this.vowelListBox.TabIndex = 1;
            this.vowelListBox.SelectedIndexChanged += new System.EventHandler(this.vowelListBox_SelectedIndexChanged);
            // 
            // languageListBox
            // 
            this.languageListBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.languageListBox.FormattingEnabled = true;
            this.languageListBox.ItemHeight = 20;
            this.languageListBox.Location = new System.Drawing.Point(376, 10);
            this.languageListBox.Name = "languageListBox";
            this.languageListBox.Size = new System.Drawing.Size(95, 204);
            this.languageListBox.TabIndex = 2;
            this.languageListBox.SelectedIndexChanged += new System.EventHandler(this.languageListBox_SelectedIndexChanged);
            // 
            // vowelDisplay
            // 
            this.vowelDisplay.BackColor = System.Drawing.Color.Black;
            this.vowelDisplay.Location = new System.Drawing.Point(0, 0);
            this.vowelDisplay.Name = "vowelDisplay";
            this.vowelDisplay.Size = new System.Drawing.Size(370, 503);
            this.vowelDisplay.TabIndex = 0;
            this.vowelDisplay.TargetLanguage = "english";
            this.vowelDisplay.TargetVowel = "i";
            // 
            // AccentTutorFrm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(476, 503);
            this.Controls.Add(this.languageListBox);
            this.Controls.Add(this.vowelListBox);
            this.Controls.Add(this.vowelDisplay);
            this.Name = "AccentTutorFrm";
            this.Text = "Accent Tutor";
            this.Load += new System.EventHandler(this.AccentTutorFrm_Load);
            this.Resize += new System.EventHandler(this.AccentTutorFrm_Resize);
            this.ResumeLayout(false);

        }

        #endregion

        private VowelDisplay vowelDisplay;
        private System.Windows.Forms.ListBox vowelListBox;
        private System.Windows.Forms.ListBox languageListBox;
    }
}

