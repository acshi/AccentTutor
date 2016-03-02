using System;
using System.Collections.Generic;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using NAudio.Wave;
using System.Threading;
using SpectrumAnalyzer;
using static SpectrumAnalyzer.Analyzer;

namespace AccentTutor {
    public partial class VowelDisplay : UserControl {
        // Prevent race conditions with updating the UI
        ManualResetEvent safeToDrawUI = new ManualResetEvent(true);
        ManualResetEvent safeToChangeUIVariables = new ManualResetEvent(true);

        AudioIn audioIn;
        FftProcessor fftProcessor;

        const float RECORDING_TOLERANCE = 0.002f;
        const int SPECTRUMS_PER_TAKE = 10;

        private string targetVowel = "i";
        public string TargetVowel
        {
            get
            {
                return targetVowel;
            }
            set {
                targetVowel = value;
                updateUI();
            }
        }

        // This will give us entries from 0hz through max freq hz.
        float[] fft = new float[(int)(MAX_FREQ / ((float)AudioIn.SAMPLE_RATE / FftProcessor.FFT_LENGTH))];
        Peak[] topPeaks;
        Peak[] fundamentalsPeaks;
        float[] harmonicValues;

        Formant[][] lastObservedFormants = new Formant[4][];
        Formant[][] lastAcceptedFormants = new Formant[3][]; // one array of last accepted values for each of f1, f2, and f3.
        Formant[] formants;
        VowelMatching vowelMatching;

        float[] lastObservedFundamentals = new float[4];
        float[] lastChosenFundamentals = new float[4];
        float fundamentalFrequency = -1;

        public VowelDisplay() {
            for (int i = 0; i < lastAcceptedFormants.Length; i++) {
                lastAcceptedFormants[i] = new Formant[4];
            }

            InitializeComponent();

            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
        }

        // Separate out from VowelDisplay() so that the visual designer does not choke on these
        public void InitializeAudioAndFft() {
            if (audioIn == null) {
                audioIn = new AudioIn(FftProcessor.SAMPLES_IN_UPDATE);
                fftProcessor = new FftProcessor();

                audioIn.AudioAvilable += HandleAudioData;
                fftProcessor.FftDataAvilable += HandleFftData;
                audioIn.Start();
            }
        }

        // Just pass microphone data on to the fft processor
        private void HandleAudioData(object sender, AudioAvailableHandlerArgs e) {
            fftProcessor.ProcessSamples(e.Samples);
        }

        private void updateUI() {
            // Make UI modification thread-safe
            if (InvokeRequired) {
                Invoke((MethodInvoker)updateUI);
                return;
            }
            Invalidate();
        }

        private void prepareVisualization() {
            safeToDrawUI.Reset();
            safeToChangeUIVariables.WaitOne(); // Block while painting UI

            if (fft != null) {
                // Transform the specturm to reflect how the vocal chords produce both low and high frequencies quieter than middle frequencies
                // First boost low frequencies < 300hz about
                //float deltaFreq = (float)AudioIn.SAMPLE_RATE / FftProcessor.SAMPLES_IN_FFT;
                //spectrum = spectrum.Index().Select(v => v.Value / (1.05f - (float)Math.Exp(-Math.Max(v.Key * deltaFreq - 210, 0) / 150f))).ToArray();
                // Then boost high frequencies > 1000hz about
                //spectrum = spectrum.Index().Select(v => v.Value * (float)Math.Exp((v.Key * deltaFreq - 1000) / 1300)).ToArray();

                topPeaks = GetTopPeaks(fft, 32);
                float newFundamental = IdentifyFundamental(topPeaks, out fundamentalsPeaks);
                fundamentalFrequency = MakeSmoothedFundamental(newFundamental, lastObservedFundamentals, lastChosenFundamentals);

                if (fundamentalFrequency != -1) {
                    harmonicValues = EvaluateHarmonicSeries(fft, fundamentalFrequency);
                    var newFormants = IdentifyApparentFormants(fundamentalFrequency, harmonicValues, 0.8f);
                    var newFormants123 = IdentifyFormants123(newFormants);
                    formants = MakeSmoothedFormants123(newFormants123, lastObservedFormants, lastAcceptedFormants);
                    vowelMatching = MatchVowel(formants, fundamentalFrequency, targetVowel);
                }
            } else {
                fundamentalFrequency = -1;
                topPeaks = null;
            }

            safeToDrawUI.Set();
            updateUI();
        }

        private void HandleFftData(object sender, FftDataAvailableHandlerArgs e) {
            float[] rawFft = e.FftData;

            // Normalize and then square for power instead of amplitude
            for (int i = 0; i < fft.Length; i++) {
                float n = rawFft[i] / (float)Math.Sqrt(FftProcessor.FFT_LENGTH);
                fft[i] = n * n;
            }
            // Remove DC component (and close to it)
            fft[0] = fft[1] = fft[2] = 0;

            // Remove noise in the fft with a high-pass filter
            float lastIn = fft[0];
            float lastOut = lastIn;
            float alpha = 0.96f;
            for (int i = 1; i < fft.Length; i++) {
                float inValue = fft[i];
                float outValue = alpha * (lastOut + inValue - lastIn);
                lastIn = inValue;
                lastOut = outValue;
                fft[i] = outValue;
            }

            // Z-score it, put negative values at 0.
            float average = fft.Average();
            float sumSquareDiffs = 0;
            for (int i = 0; i < fft.Length; i++) {
                sumSquareDiffs += (fft[i] - average) * (fft[i] - average);
            }
            float stdDev = (float)Math.Sqrt(sumSquareDiffs / fft.Length);
            for (int i = 0; i < fft.Length; i++) {
                fft[i] = (fft[i] - average) / stdDev; //Math.Max(..., 0)?
            }

            prepareVisualization();
        }

        protected override void OnPaint(PaintEventArgs pe) {
            safeToChangeUIVariables.Reset();
            safeToDrawUI.WaitOne(); // Block while GUI referenced things are referenced

            Graphics graphics = pe.Graphics;

            //float freqPerIndex = (float)AudioIn.SAMPLE_RATE / FftProcessor.SAMPLES_IN_FFT;
            
            Font font = new Font(FontFamily.GenericSerif, 14.0f);

            // Make a formant grid
            // F1 on the horizontal, 0hz to 1400hz
            int f1Lines = 1400 / 200;
            float xPerF1Line = Width / (float)f1Lines;
            for (int i = 0; i < f1Lines; i++) {
                float x = xPerF1Line * i;
                graphics.DrawLine(Pens.Gray, x, 0, x, Height);
            }
            // F2 on the vertical, 500hz to 4000hz
            int f2Lines = 3500 / 500;
            float yPerF2Line = Height / (float)f1Lines;
            for (int i = 0; i < f2Lines; i++) {
                float y = yPerF2Line * i;
                graphics.DrawLine(Pens.DarkGray, 0, y, Width, y);
            }

            float f1Scale = Width / 1400f;
            float f2Scale = Height / 3500f;

            if (fundamentalFrequency != -1) {
                // Draw grid harmonic lines
                /*for (float f = 0; f < 1400; f += fundamentalFrequency) {
                    float x = f * f1Scale;
                    graphics.DrawLine(Pens.Gray, x, 0, x, Height);
                }
                for (float f = 0; f < 4000; f += fundamentalFrequency) {
                    if (f > 500) {
                        float y = (f - 500) * f2Scale;
                        graphics.DrawLine(Pens.Gray, 0, y, Width, y);
                    }
                }*/

                // Draw current pronunciation point at (f1, f2)
                if (vowelMatching.matches != null && vowelMatching.matches.Length >= 2) {
                    float f1 = formants[vowelMatching.matches[0].Item1].freq;
                    float f2 = formants[vowelMatching.matches[1].Item1].freq;

                    float px = f1 * Width / 1400;
                    float py = Height - (f2 - 500) * Height / 3500;
                    graphics.FillEllipse(Brushes.Yellow, px - 5, py - 5, 10, 10);

                    graphics.DrawString("F1: " + f1, font, Brushes.White, 10, 10);
                    graphics.DrawString("F2: " + f2, font, Brushes.White, 10, 30);
                }
            }

            for (int i = 0; i < VowelData.Vowels.Length; i++) {
                // Draw the regions of our formant
                string vowel = VowelData.Vowels[i];

                float lowF1 = VowelData.FormantLows[vowel][0] * f1Scale;
                float highF1 = VowelData.FormantHighs[vowel][0] * f1Scale;
                float lowF2 = (VowelData.FormantLows[vowel][1] - 500) * f2Scale;
                float highF2 = (VowelData.FormantHighs[vowel][1] - 500) * f2Scale;
                float stdDevF1 = VowelData.FormantStdDevs[vowel][0] * f1Scale;
                float stdDevF2 = VowelData.FormantStdDevs[vowel][1] * f2Scale;

                Brush b = (vowel == targetVowel) ? Brushes.White : Brushes.Gray;
                Pen p = (vowel == targetVowel) ? Pens.White : Pens.Gray;

                graphics.DrawString(vowel, font, b, (lowF1 + highF1) / 2f - 7, Height - (lowF2 + highF2) / 2f - 7);
                graphics.DrawPolygon(p, new PointF[] {
                    new PointF(lowF1 - stdDevF1, Height - (lowF2 + stdDevF2)),
                    new PointF(lowF1 - stdDevF1, Height - (lowF2 - stdDevF2)),
                    new PointF(lowF1 + stdDevF1, Height - (lowF2 - stdDevF2)),
                    new PointF(highF1 + stdDevF1, Height - (highF2 - stdDevF2)),
                    new PointF(highF1 + stdDevF1, Height - (highF2 + stdDevF2)),
                    new PointF(highF1 - stdDevF1, Height - (highF2 + stdDevF2)),
                });
            }

            safeToChangeUIVariables.Set();
        }

        private void SpectrumDisplay_Resize(object sender, EventArgs e) {
            Invalidate();
        }
    }
}
