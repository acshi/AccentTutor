﻿using System;
using System.Drawing;
using System.Windows.Forms;

namespace AccentTutor
{
    public partial class AccentTutorFrm : Form
    {
        public AccentTutorFrm()
        {
            InitializeComponent();
        }

        private void spectrumDisplay_Load(object sender, EventArgs e) {
            spectrumDisplay.InitializeAudioAndFft();
        }
    }
}
