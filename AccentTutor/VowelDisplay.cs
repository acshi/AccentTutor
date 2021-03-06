﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using NAudio.Wave;
using System.Threading;
using SpectrumAnalyzer;
using static SpectrumAnalyzer.Analyzer;
using static SpectrumAnalyzer.VowelData;
using System.Diagnostics;

namespace AccentTutor {
    public partial class VowelDisplay : UserControl {
        AudioIn audioIn;
        FftProcessor fftProcessor;

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

        private string targetLanguage = "mandarin";
        public string TargetLanguage
        {
            get
            {
                return targetLanguage;
            }
            set
            {
                targetLanguage = value;
                updateUI();
            }
        }

        // This will give us entries from 0hz through max freq hz.
        float[] fft = new float[(int)(MAX_FREQ / ((float)AudioIn.SAMPLE_RATE / FftProcessor.FFT_LENGTH))];

        float[][] lastFfts = new float[8][];
        float[] lastFundamentals = new float[8];
        
        Formant[] formants;
        //VowelMatching vowelMatching;
       
        float fundamentalFrequency = -1;
        float[] harmonicValues;

        Tuple<PointF, float>[] lastDrawnPointsRoundness = new Tuple<PointF, float>[8];

        public VowelDisplay() {
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

        private Tuple<PointF, float> calculatePronunciationDot() {
            // Calculate pronunciation dot
            var f1 = formants[0];
            var f2 = formants[1];
            var f3 = formants[2];

            float newX = f1.freq * Width / 1400;
            float newY = Height - (f2.freq - 500) * Height / 3500;
            // values are not decibels, so convert first
            float newRoundedness = Roundedness(f1.value, f2.value, f3.value);

            return Tuple.Create(new PointF(newX, newY), newRoundedness);
        }

        private void drawPronunciationDot(Graphics graphics, Tuple<PointF, float> pointRoundedness, byte alpha) {
            float x = pointRoundedness.Item1.X;
            float y = pointRoundedness.Item1.Y;
            float roundedness = pointRoundedness.Item2;

            if (x != -1) {
                Color roundednessColor = HsvToColor(roundedness * 180, 0.6, 1.0, alpha);

                float dotRadius = Width / 80f;

                graphics.FillEllipse(new SolidBrush(roundednessColor), x - dotRadius, y - dotRadius, dotRadius * 2, dotRadius * 2);
                if (alpha == 255) {
                    graphics.DrawEllipse(new Pen(Color.FromArgb(alpha, 0, 0, 0)), x - dotRadius, y - dotRadius, dotRadius * 2, dotRadius * 2);
                }
            }
        }

        private void prepareVisualization() {
            // Safe updating with ui
            Monitor.Enter(this);
            
            PerformFormantAnalysis(lastFfts, lastFundamentals, out fundamentalFrequency, out harmonicValues, out formants);
                
            Array.Copy(lastDrawnPointsRoundness, 1, lastDrawnPointsRoundness, 0, lastDrawnPointsRoundness.Length - 1);
            lastDrawnPointsRoundness[lastDrawnPointsRoundness.Length - 1] = calculatePronunciationDot();

            Monitor.Exit(this);
            updateUI();
        }

        private void HandleFftData(object sender, FftDataAvailableHandlerArgs e) {
            float[] rawFft = e.FftData;
            float[] newFft = new float[fft.Length];

            // Normalize and then square for power instead of amplitude
            for (int i = 0; i < newFft.Length; i++) {
                float n = rawFft[i] / (float)Math.Sqrt(FftProcessor.FFT_LENGTH);
                newFft[i] = n * n;
            }
            // Remove DC component (and close to it)
            newFft[0] = newFft[1] = newFft[2] = 0;

            // Remove noise in the fft with a high-pass filter
            float lastIn = newFft[0];
            float lastOut = lastIn;
            float alpha = 0.96f;
            for (int i = 1; i < newFft.Length; i++) {
                float inValue = newFft[i];
                float outValue = alpha * (lastOut + inValue - lastIn);
                lastIn = inValue;
                lastOut = outValue;
                newFft[i] = outValue;
            }

            // Z-score it, put negative values at 0.
            float average = newFft.Average();
            float sumSquareDiffs = 0;
            for (int i = 0; i < newFft.Length; i++) {
                sumSquareDiffs += (newFft[i] - average) * (newFft[i] - average);
            }
            float stdDev = (float)Math.Sqrt(sumSquareDiffs / newFft.Length);
            for (int i = 0; i < newFft.Length; i++) {
                newFft[i] = (newFft[i] - average) / stdDev;
            }

            // Consider it noise/silence if the stdDev is too low
            Debug.WriteLine(stdDev);
            if (stdDev > 1e7) {
                fft = newFft;

                // Shift in the new fft
                Array.Copy(lastFfts, 1, lastFfts, 0, lastFfts.Length - 1);
                lastFfts[lastFfts.Length - 1] = fft;

                // Shift in new fundamental
                Array.Copy(lastFundamentals, 1, lastFundamentals, 0, lastFundamentals.Length - 1);
                lastFundamentals[lastFundamentals.Length - 1] = IdentifyFundamental(fft);

                prepareVisualization();
            } else {
                // Shift in null and 0 values to clear out the buffer.
                Array.Copy(lastFfts, 1, lastFfts, 0, lastFfts.Length - 1);
                lastFfts[lastFfts.Length - 1] = null;
                Array.Copy(lastFundamentals, 1, lastFundamentals, 0, lastFundamentals.Length - 1);
                lastFundamentals[lastFundamentals.Length - 1] = 0;

                Array.Copy(lastDrawnPointsRoundness, 1, lastDrawnPointsRoundness, 0, lastDrawnPointsRoundness.Length - 1);
                lastDrawnPointsRoundness[lastDrawnPointsRoundness.Length - 1] = Tuple.Create(new PointF(-1, -1), 0f); // Set to not draw it

                updateUI();
            }
        }

        void DrawVowelRegion(Graphics graphics, Font font, Vowel v) {
            float f1Scale = Width / 1400f;
            float f2Scale = Height / 3500f;

            float lowF1 = v.formantLows[0] * f1Scale;
            float highF1 = v.formantHighs[0] * f1Scale;
            float lowF2 = (v.formantLows[1] - 500) * f2Scale;
            float highF2 = (v.formantHighs[1] - 500) * f2Scale;
            float stdDevF1 = v.formantStdDevs[0] * f1Scale;
            float stdDevF2 = v.formantStdDevs[1] * f2Scale;

            Brush b = (v.vowel == targetVowel) ? Brushes.Black : Brushes.Gray;
            Pen p = (v.vowel == targetVowel) ? Pens.White : Pens.Gray;

            var vowelBoundaryPoints = new PointF[] {
                new PointF(lowF1 - stdDevF1, Height - (lowF2 + stdDevF2)),
                new PointF(lowF1 - stdDevF1, Height - (lowF2 - stdDevF2)),
                new PointF(lowF1 + stdDevF1, Height - (lowF2 - stdDevF2)),
                new PointF(highF1 + stdDevF1, Height - (highF2 - stdDevF2)),
                new PointF(highF1 + stdDevF1, Height - (highF2 + stdDevF2)),
                new PointF(highF1 - stdDevF1, Height - (highF2 + stdDevF2)),
            };
            // Amplitude values here are in decibels (all negative). Roundedness will range -0db to -50db, here 0 to 1.
            double roundedness = Roundedness(v.formantAmplitudes[0], v.formantAmplitudes[1], v.formantAmplitudes[2]);
            graphics.FillPolygon(new SolidBrush(HsvToColor(roundedness * 180, 0.6, 1.0, 255)), vowelBoundaryPoints);
            graphics.DrawPolygon(p, vowelBoundaryPoints);

            graphics.DrawString(v.vowel, font, b, (lowF1 + highF1) / 2f - 7, Height - (lowF2 + highF2) / 2f - 7);
        }

        // Roundedness of the vowel by the decibels of the formants, 0 to 1
        float Roundedness(int db1, int db2, int db3) {
            if (db2 > db1) {
                int tmp = db1;
                db1 = db2;
                db2 = tmp;
            }
            if (db3 > db1) {
                int tmp = db1;
                db1 = db3;
                db3 = tmp;
            }
            // Amplitude values here are in decibels (all negative). Roundedness will range -0db to -50db, here 0 to 1.
            return Math.Min(Math.Min(0, (Math.Max(db2, db3) - db1)) / -50.0f, 1.0f);
        }

        // Roundedness of the vowel by the amplitudes of the formants, 0 to 1
        float Roundedness(float f1val, float f2val, float f3val) {
            if (f2val > f1val) {
                float tmp = f1val;
                f1val = f2val;
                f2val = tmp;
            }
            if (f3val > f1val) {
                float tmp = f1val;
                f1val = f3val;
                f3val = tmp;
            }
            // values are not decibels, so convert first
            return Math.Min(Math.Min(0, (float)Math.Log10(Math.Max(f2val, f3val) / f1val) * 20) / -50.0f, 1.0f);
        }

        protected override void OnPaint(PaintEventArgs pe) {
            // Safe with updating ui
            Monitor.Enter(this);

            Graphics graphics = pe.Graphics;
            Font font = new Font(FontFamily.GenericSerif, 14.0f);
            
            // Paint only every other fft entry to save time
            for (int x = 0; x + 1 < fft.Length; x += 2) {
                float y = Math.Max(fft[x], fft[x + 1]) * 30;
                //graphics.DrawLine(Pens.Black, x, Height, x + 1, Height - y);
                graphics.DrawLine(Pens.Gray, x / 2, Height - y, x / 2, Height);
            }

            if (fundamentalFrequency != -1) {
                float freqPerIndex = (float)AudioIn.SAMPLE_RATE / FftProcessor.FFT_LENGTH;
                // Plot the harmonics
                for (int i = 0; i < harmonicValues.Length; i++) {
                    float x1 = (i + 1) * fundamentalFrequency / freqPerIndex / 2;
                    float y1 = Height / 2 - harmonicValues[i] * 8f * (float)Math.Sqrt(i + 1);
                    if (i < harmonicValues.Length - 1) {
                        float x2 = (i + 2) * fundamentalFrequency / freqPerIndex / 2;
                        float y2 = Height / 2 - harmonicValues[i + 1] * 8f * (float)Math.Sqrt(i + 2);
                        graphics.DrawLine(Pens.Gray, x1, y1, x2, y2);
                    }

                    graphics.DrawString("" + harmonicValues[i].ToString("0.0"), font, Brushes.Gray, x1 - 21, Height / 2);
                }
            }

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
            
            Vowel[] vowels = GetVowels(targetLanguage);
            for (int i = 0; i < vowels.Length; i++) {
                // Draw the regions of the formant
                Vowel v = vowels[i];
                DrawVowelRegion(graphics, font, v);
            }
            // Draw current vowel on top again
            DrawVowelRegion(graphics, font, GetVowel(targetLanguage, targetVowel));

            if (fundamentalFrequency != -1) {
                float f0 = fundamentalFrequency;
                var f1 = formants[0];
                var f2 = formants[1];
                var f3 = formants[2];

                for (int i = 0; i < lastDrawnPointsRoundness.Length; i++) {
                    if (lastDrawnPointsRoundness[i] != null) {
                        byte alpha = (byte)(128 * lastDrawnPointsRoundness.Length / (i + 1));
                        drawPronunciationDot(graphics, lastDrawnPointsRoundness[i], alpha);
                    }
                }
                // Always draw the most recent value, even if it has phased out of the last drawn points.
                drawPronunciationDot(graphics, calculatePronunciationDot(), 255);

                graphics.DrawString("F0: " + f0, font, Brushes.White, 10, 10);
                graphics.DrawString("F1: " + f1.freq, font, Brushes.White, 10, 30);
                graphics.DrawString("F2: " + f2.freq, font, Brushes.White, 10, 50);
                graphics.DrawString("F3: " + f3.freq, font, Brushes.White, 10, 70);
            }

            Monitor.Exit(this);
        }

        private void SpectrumDisplay_Resize(object sender, EventArgs e) {
            Invalidate();
        }

        /// <summary>
        /// Convert HSV to RGB
        /// h is from 0-360 mod 360
        /// s,v values are 0-1
        /// r,g,b values are 0-255
        /// Based upon http://ilab.usc.edu/wiki/index.php/HSV_And_H2SV_Color_Space#HSV_Transformation_C_.2F_C.2B.2B_Code_2
        /// From: http://www.splinter.com.au/converting-hsv-to-rgb-colour-using-c/
        /// </summary>
        Color HsvToColor(double h, double S, double V, byte alpha) {
            // ######################################################################
            // T. Nathan Mundhenk
            // mundhenk@usc.edu
            // C/C++ Macro HSV to RGB

            double H = h;
            while (H < 0) { H += 360; };
            while (H >= 360) { H -= 360; };
            double R, G, B;
            if (V <= 0) { R = G = B = 0; } else if (S <= 0) {
                R = G = B = V;
            } else {
                double hf = H / 60.0;
                int i = (int)Math.Floor(hf);
                double f = hf - i;
                double pv = V * (1 - S);
                double qv = V * (1 - S * f);
                double tv = V * (1 - S * (1 - f));
                switch (i) {

                    // Red is the dominant color

                    case 0:
                        R = V;
                        G = tv;
                        B = pv;
                        break;

                    // Green is the dominant color

                    case 1:
                        R = qv;
                        G = V;
                        B = pv;
                        break;
                    case 2:
                        R = pv;
                        G = V;
                        B = tv;
                        break;

                    // Blue is the dominant color

                    case 3:
                        R = pv;
                        G = qv;
                        B = V;
                        break;
                    case 4:
                        R = tv;
                        G = pv;
                        B = V;
                        break;

                    // Red is the dominant color

                    case 5:
                        R = V;
                        G = pv;
                        B = qv;
                        break;

                    // Just in case we overshoot on our math by a little, we put these here. Since its a switch it won't slow us down at all to put these here.

                    case 6:
                        R = V;
                        G = tv;
                        B = pv;
                        break;
                    case -1:
                        R = V;
                        G = pv;
                        B = qv;
                        break;

                    // The color is not defined, we should throw an error.

                    default:
                        //LFATAL("i Value error in Pixel conversion, Value is %d", i);
                        R = G = B = V; // Just pretend its black/white
                        break;
                }
            }
            int r = Clamp((int)(R * 255.0));
            int g = Clamp((int)(G * 255.0));
            int b = Clamp((int)(B * 255.0));
            return Color.FromArgb(alpha, r, g, b);
        }

        /// <summary>
        /// Clamp a value to 0-255
        /// </summary>
        int Clamp(int i) {
            if (i < 0) return 0;
            if (i > 255) return 255;
            return i;
        }
    }
}
