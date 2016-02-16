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
            this.recordBtn = new System.Windows.Forms.Button();
            this.completionLbl = new System.Windows.Forms.Label();
            this.analyzeFileBtn = new System.Windows.Forms.Button();
            this.openFileDialog = new System.Windows.Forms.OpenFileDialog();
            this.saveVowelBtn = new System.Windows.Forms.Button();
            this.clearVowelBtn = new System.Windows.Forms.Button();
            this.processBtn = new System.Windows.Forms.Button();
            this.liveBtn = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // recordBtn
            // 
            this.recordBtn.Location = new System.Drawing.Point(3, 3);
            this.recordBtn.Name = "recordBtn";
            this.recordBtn.Size = new System.Drawing.Size(92, 23);
            this.recordBtn.TabIndex = 0;
            this.recordBtn.Text = "Record";
            this.recordBtn.UseVisualStyleBackColor = true;
            this.recordBtn.Click += new System.EventHandler(this.recordBtn_Click);
            // 
            // completionLbl
            // 
            this.completionLbl.Location = new System.Drawing.Point(3, 29);
            this.completionLbl.Name = "completionLbl";
            this.completionLbl.Size = new System.Drawing.Size(92, 25);
            this.completionLbl.TabIndex = 1;
            this.completionLbl.Text = "0% Complete";
            this.completionLbl.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // analyzeFileBtn
            // 
            this.analyzeFileBtn.Location = new System.Drawing.Point(101, 3);
            this.analyzeFileBtn.Name = "analyzeFileBtn";
            this.analyzeFileBtn.Size = new System.Drawing.Size(94, 23);
            this.analyzeFileBtn.TabIndex = 2;
            this.analyzeFileBtn.Text = "Analyze File";
            this.analyzeFileBtn.UseVisualStyleBackColor = true;
            this.analyzeFileBtn.Click += new System.EventHandler(this.analyzeFileBtn_Click);
            // 
            // openFileDialog
            // 
            this.openFileDialog.Filter = "WAV files|*.wav";
            this.openFileDialog.Multiselect = true;
            // 
            // saveVowelBtn
            // 
            this.saveVowelBtn.Location = new System.Drawing.Point(201, 3);
            this.saveVowelBtn.Name = "saveVowelBtn";
            this.saveVowelBtn.Size = new System.Drawing.Size(151, 23);
            this.saveVowelBtn.TabIndex = 3;
            this.saveVowelBtn.Text = "Save Vowel";
            this.saveVowelBtn.UseVisualStyleBackColor = true;
            this.saveVowelBtn.Click += new System.EventHandler(this.saveVowelBtn_Click);
            // 
            // clearVowelBtn
            // 
            this.clearVowelBtn.Location = new System.Drawing.Point(201, 30);
            this.clearVowelBtn.Name = "clearVowelBtn";
            this.clearVowelBtn.Size = new System.Drawing.Size(72, 23);
            this.clearVowelBtn.TabIndex = 4;
            this.clearVowelBtn.Text = "Clear Vowel";
            this.clearVowelBtn.UseVisualStyleBackColor = true;
            this.clearVowelBtn.Click += new System.EventHandler(this.clearVowelBtn_Click);
            // 
            // processBtn
            // 
            this.processBtn.Location = new System.Drawing.Point(101, 30);
            this.processBtn.Name = "processBtn";
            this.processBtn.Size = new System.Drawing.Size(94, 23);
            this.processBtn.TabIndex = 5;
            this.processBtn.Text = "Reprocess Data";
            this.processBtn.UseVisualStyleBackColor = true;
            this.processBtn.Click += new System.EventHandler(this.processBtn_Click);
            // 
            // liveBtn
            // 
            this.liveBtn.Location = new System.Drawing.Point(279, 29);
            this.liveBtn.Name = "liveBtn";
            this.liveBtn.Size = new System.Drawing.Size(72, 23);
            this.liveBtn.TabIndex = 6;
            this.liveBtn.Text = "Live On/Off";
            this.liveBtn.UseVisualStyleBackColor = true;
            this.liveBtn.Click += new System.EventHandler(this.liveBtn_Click);
            // 
            // SpectrumDisplay
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.liveBtn);
            this.Controls.Add(this.processBtn);
            this.Controls.Add(this.clearVowelBtn);
            this.Controls.Add(this.saveVowelBtn);
            this.Controls.Add(this.analyzeFileBtn);
            this.Controls.Add(this.completionLbl);
            this.Controls.Add(this.recordBtn);
            this.Name = "SpectrumDisplay";
            this.Size = new System.Drawing.Size(534, 240);
            this.Resize += new System.EventHandler(this.SpectrumDisplay_Resize);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button recordBtn;
        private System.Windows.Forms.Label completionLbl;
        private System.Windows.Forms.Button analyzeFileBtn;
        private System.Windows.Forms.OpenFileDialog openFileDialog;
        private System.Windows.Forms.Button saveVowelBtn;
        private System.Windows.Forms.Button clearVowelBtn;
        private System.Windows.Forms.Button processBtn;
        private System.Windows.Forms.Button liveBtn;
    }
}
