using SpectrumAnalyzer;
using System;
using System.Linq;
using System.Windows.Forms;

namespace AccentTutor {
    public partial class AccentTutorFrm : Form {
        public AccentTutorFrm() {
            InitializeComponent();
            languageListBox.DataSource = new string[] { "english", "mandarin" };
        }

        private void AccentTutorFrm_Load(object sender, EventArgs e) {
            vowelDisplay.InitializeAudioAndFft();
        }

        private void AccentTutorFrm_Resize(object sender, EventArgs e) {
            vowelDisplay.Width = Width - vowelListBox.Width - 12;
            vowelDisplay.Height = Height - 39;
            vowelListBox.Left = vowelDisplay.Width + 6;
            languageListBox.Left = vowelDisplay.Width + 6;
        }

        private void vowelListBox_SelectedIndexChanged(object sender, EventArgs e) {
            vowelDisplay.TargetVowel = (string)vowelListBox.SelectedItem;
        }

        private void languageListBox_SelectedIndexChanged(object sender, EventArgs e) {
            vowelDisplay.TargetLanguage = (string)languageListBox.SelectedItem;
            switch (vowelDisplay.TargetLanguage) {
                case "english":
                    vowelListBox.DataSource = VowelData.EnglishVowels.Select(v => v.vowel).ToList();
                    break;
                case "mandarin":
                    vowelListBox.DataSource = VowelData.MandarinVowels.Select(v => v.vowel).ToList();
                    break;
            }
        }
    }
}
