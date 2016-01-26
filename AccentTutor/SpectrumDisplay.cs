using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using AccentTutor;

namespace AccentTutor {
    public partial class SpectrumDisplay : UserControl {
        AudioInFft fftIn = new AudioInFft();
        VowelLearner vowelLearner = new VowelLearner();
        int currentVowelI = 0;
        bool isRecording = false;
        // From last added spectrum
        float percentChange = 1f;

        float[] fftData;
        float[] spectrum;
        Tuple<int, int, float>[] topPeaks;

        string[,] vowelsToRecord = { { "i" , "as in bead" },
                                     { "I", "as in bid" },
                                     { "ɛ", "as in bed" },
                                     { "æ", "as in bad" },
                                     { "ɜ", "the 'ir' in bird" },
                                     { "ə", "as the 'a' in about" },
                                     { "ʌ", "as in bud" },
                                     { "u", "as in food" },
                                     { "ʊ", "as in good" },
                                     { "ɔ", "as in born" },
                                     { "ɒ", "as in body" },
                                     { "ɑ", "as in bard" },
                                     { "l", "the 'l' in like by itself" },
                                     { "ɫ", "the ll in full by itself" } };

        public SpectrumDisplay() {
            InitializeComponent();
            fftIn.FftDataAvilable += HandleFftData;
            updateUI();

            this.SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
        }

        private void updateUI() {
            nextVowelBtn.Text = "Record '" + vowelsToRecord[currentVowelI, 0] + "' " + vowelsToRecord[currentVowelI, 1];
            nextVowelBtn.Enabled = !isRecording;
            if (isRecording) {
                float completeness = 1 - Math.Min(0.05f, percentChange - 0.001f) / 0.05f;
                completionLbl.Text = ((int)(completeness * 1000) / 10f) + "% Complete";
            } else {
                completionLbl.Text = "Not Recording";
            }
        }

        private void nextVowelBtn_Click(object sender, EventArgs e) {
            fftIn.Start();
            isRecording = true;
            updateUI();
        }

        private void HandleFftData(object sender, FftDataAvailableHandlerArgs e) {
            fftData = e.FftData;

            float deltaFreq = (float)AudioInFft.SAMPLE_RATE / AudioInFft.SAMPLES_IN_FFT;
            // Operate only on frequencies through 4000Hz that may be involved in formants
            float[] fft = fftData.Take((int)(4000f / deltaFreq + 0.5f)).ToArray();
            // Normalize the fft
            fft = fft.Select(a => (float)(a / Math.Sqrt(AudioInFft.SAMPLES_IN_FFT))).ToArray();
            // Negative values differ only by phase, use positive ones instead
            fft = fft.Select(a => Math.Abs(a)).ToArray();
            // Remove DC component (and close to it)
            fft[0] = fft[1] = fft[2] = 0;

            percentChange = vowelLearner.AddVowelFft(vowelsToRecord[currentVowelI, 0], fft);
            Console.WriteLine(percentChange);
            
            spectrum = vowelLearner.GetAverageSpectrum();
            // Z-score it
            float average = spectrum.Average();
            float stdDev = (float)Math.Sqrt(spectrum.Select(num => (num - average) * (num - average)).Average());
            spectrum = spectrum.Select(num => Math.Max((num - average) / stdDev, 0)).ToArray();

            topPeaks = vowelLearner.GetTopPeaks(vowelsToRecord[currentVowelI, 0], 10);

            // Enough data now.
            if (isRecording && percentChange < 0.001) {
                fftIn.Stop();
                isRecording = false;
                currentVowelI++;
            }

            updateUI();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs pe) {
            System.Drawing.Graphics graphics = pe.Graphics;
            
            float width = this.Width;
            float height = this.Height;

            if (spectrum != null) {
                float deltaX = width / fftData.Length * 2;
                float partsPerX = (fftData.Length / 20) / width;
                for (int x = 0; x < width && x < spectrum.Length; x++) {
                    float y = (float)(spectrum[x] * 30);
                    // Draw the peak center in a different color
                    if (topPeaks.Any(peak => peak.Item1 <= x && x <= peak.Item2)) {
                        graphics.DrawLine(Pens.Red, x, height, x, height - y);
                    } else {
                        graphics.DrawLine(Pens.Black, x, height, x, height - y);
                    }
                }
            }

            if (topPeaks != null) {
                float deltaFreq = (float)AudioInFft.SAMPLE_RATE / AudioInFft.SAMPLES_IN_FFT;

                int n = 0;
                foreach (var peak in topPeaks) {
                    Font f = new Font(FontFamily.GenericSerif, 10.0f);
                    float x = (peak.Item1 + peak.Item2) / 2f - 5.0f;
                    float y = height - 10 - peak.Item3 / (peak.Item2 - peak.Item1);
                    graphics.DrawString("" + ++n, f, Brushes.Black, x, y);
                    float freq = (peak.Item1 + peak.Item2) / 2f * deltaFreq;
                    graphics.DrawString("" + freq, f, Brushes.Black, x, y - 10);
                }
            }
        }

        private void SpectrumDisplay_Resize(object sender, EventArgs e) {
            Invalidate();
        }
    }
}
