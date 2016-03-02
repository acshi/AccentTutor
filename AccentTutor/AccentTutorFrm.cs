using SpectrumAnalyzer;
using System;
using System.Windows.Forms;

namespace AccentTutor {
    public partial class AccentTutorFrm : Form {
        public AccentTutorFrm() {
            InitializeComponent();
            vowelListBox.DataSource = VowelData.Vowels;
        }

        private void AccentTutorFrm_Load(object sender, EventArgs e) {
            vowelDisplay.InitializeAudioAndFft();
        }

        private void AccentTutorFrm_Resize(object sender, EventArgs e) {
            vowelDisplay.Width = Width - vowelListBox.Width;
            vowelDisplay.Height = Height;
            vowelListBox.Left = vowelDisplay.Width;
        }

        private void vowelListBox_SelectedIndexChanged(object sender, EventArgs e) {
            vowelDisplay.TargetVowel = (string)vowelListBox.SelectedItem;
        }
    }
}
