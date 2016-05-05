using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Graphics;
using static SpectrumAnalyzer.VowelData;
using System.Threading;
using static SpectrumAnalyzer.Analyzer;
using SpectrumAnalyzer;
using System.Threading.Tasks;
using Android.Util;

namespace AndroidAccentTutor {
    public class TutorDisplay : View {
        private string targetVowel = "i";
        public string TargetVowel
        {
            get
            {
                return targetVowel;
            }
            set
            {
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

        AudioIn audioIn;
        FftProcessor fftProcessor;

        // This will give us entries from 0hz through max freq hz.
        float[] fft = new float[(int)(MAX_FREQ / ((float)AudioIn.SAMPLE_RATE / FftProcessor.FFT_LENGTH))];
        float[][] lastFfts = new float[8][];
        float[] lastFundamentals = new float[16];

        Formant[] formants;
        //VowelMatching vowelMatching;

        float fundamentalFrequency = -1;
        float[] harmonicValues;

        float stdDev = 0;
        int count = 0;

        int processedAudioFfts = 0;
        
        Tuple<PointF, float>[] lastDrawnPointsRoundness = new Tuple<PointF, float>[8];

        public TutorDisplay(Context context, IAttributeSet attrs)
        : base(context, attrs) {
        }

        // Separate out from VowelDisplay() so that the visual designer does not choke on these
        public void InitializeAudioAndFft() {
            if (audioIn == null) {
                audioIn = new AudioIn(FftProcessor.SAMPLES_IN_UPDATE);
                fftProcessor = new FftProcessor();

                audioIn.AudioAvailable += HandleAudioData;
                fftProcessor.FftDataAvilable += HandleFftData;
                audioIn.Start();
            }
        }

        private void updateUI() {
            // Make UI modification thread-safe
            /*if (InvokeRequired) {
                Invoke((MethodInvoker)updateUI);
                return;
            }*/
            Invalidate();
        }

        // Just pass microphone data on to the fft processor
        private void HandleAudioData(object sender, AudioAvailableHandlerArgs e) {
            fftProcessor.ProcessSamples(e.Samples);
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

            float freqPerIndex = (float)AudioIn.SAMPLE_RATE / FftProcessor.FFT_LENGTH;

            // Normalize and then square for power instead of amplitude
            for (int i = 0; i < newFft.Length; i++) {
                float n = rawFft[i] / (float)Math.Sqrt(FftProcessor.FFT_LENGTH);
                newFft[i] = n * n;
                // Perform some equalization to attenuate low frequencies
                float freq = i * freqPerIndex;
                if (freq < 250) {
                    newFft[i] *= 0.0025f;
                } else if (freq < 400) {
                    newFft[i] *= 0.2f;
                }
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
            stdDev = (float)Math.Sqrt(sumSquareDiffs / newFft.Length);
            for (int i = 0; i < newFft.Length; i++) {
                newFft[i] = (newFft[i] - average) / stdDev;
            }

            // Consider it noise/silence if the stdDev is too low
            Console.WriteLine(stdDev);
            if (true || processedAudioFfts < lastFfts.Length || stdDev > 5e6) {
                processedAudioFfts++;

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

                // Is there any point left we will effect?
                if (lastDrawnPointsRoundness.Any(p => p.Item1.X != -1)) {
                    Array.Copy(lastDrawnPointsRoundness, 1, lastDrawnPointsRoundness, 0, lastDrawnPointsRoundness.Length - 1);
                    lastDrawnPointsRoundness[lastDrawnPointsRoundness.Length - 1].Item1.X = -1; // Set to not draw it

                    updateUI();
                }
            }
        }

        void DrawVowelRegion(Canvas canvas, Vowel v) {
            float f1Scale = Width / 1400f;
            float f2Scale = Height / 3500f;

            float lowF1 = v.formantLows[0] * f1Scale;
            float highF1 = v.formantHighs[0] * f1Scale;
            float lowF2 = (v.formantLows[1] - 500) * f2Scale;
            float highF2 = (v.formantHighs[1] - 500) * f2Scale;
            float stdDevF1 = v.formantStdDevs[0] * f1Scale;
            float stdDevF2 = v.formantStdDevs[1] * f2Scale;

            var vowelBoundaryPoints = new PointF[] {
                new PointF(lowF1 - stdDevF1, Height - (lowF2 + stdDevF2)),
                new PointF(lowF1 - stdDevF1, Height - (lowF2 - stdDevF2)),
                new PointF(lowF1 + stdDevF1, Height - (lowF2 - stdDevF2)),
                new PointF(highF1 + stdDevF1, Height - (highF2 - stdDevF2)),
                new PointF(highF1 + stdDevF1, Height - (highF2 + stdDevF2)),
                new PointF(highF1 - stdDevF1, Height - (highF2 + stdDevF2)),
            };

            var vowelBoundaryPath = new Path();
            vowelBoundaryPath.MoveTo(vowelBoundaryPoints[0].X, vowelBoundaryPoints[0].Y);
            for (var i = 1; i < vowelBoundaryPoints.Length; i++) {
                vowelBoundaryPath.LineTo(vowelBoundaryPoints[i].X, vowelBoundaryPoints[i].Y);
            }
            vowelBoundaryPath.LineTo(vowelBoundaryPoints[0].X, vowelBoundaryPoints[0].Y);

            var paintBrush = new Paint();
            paintBrush.SetStyle(Paint.Style.Fill);

            var paintPen = new Paint {
                Color = (v.vowel == targetVowel) ? Color.Black : Color.Gray,
                StrokeWidth = 2
            };
            paintPen.SetStyle(Paint.Style.Stroke);

            // Amplitude values here are in decibels (all negative). Roundedness will range -0db to -50db, here 0 to 1.
            double roundedness = Roundedness(v.formantAmplitudes[0], v.formantAmplitudes[1], v.formantAmplitudes[2]);

            paintBrush.Color = HsvToColor(roundedness * 180, 0.6, 1.0);
            canvas.DrawPath(vowelBoundaryPath, paintBrush);
            canvas.DrawPath(vowelBoundaryPath, paintPen);

            paintBrush.Color = (v.vowel == targetVowel) ? Color.Black : Color.Gray;
            paintBrush.TextSize = 48;
            canvas.DrawText(v.vowel, (lowF1 + highF1) / 2f - 8, Height - (lowF2 + highF2) / 2f + 18, paintBrush);
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

        private void drawPronunciationDot(Canvas canvas, Tuple<PointF, float> pointRoundedness, byte alpha) {
            float x = pointRoundedness.Item1.X;
            float y = pointRoundedness.Item1.Y;
            float roundedness = pointRoundedness.Item2;

            if (x != -1) {
                Color roundednessColor = HsvToColor(roundedness * 180, 0.6, 1.0);
                roundednessColor.A = alpha;

                float dotRadius = Width / 80f;

                var roundColorBrush = new Paint { Color = roundednessColor };
                roundColorBrush.SetStyle(Paint.Style.Fill);
                canvas.DrawOval(x - dotRadius, y - dotRadius, x + dotRadius, y + dotRadius, roundColorBrush);

                if (alpha == 255) {
                    var blackPen = new Paint { Color = Color.Black, StrokeWidth = 2 };
                    blackPen.SetStyle(Paint.Style.Stroke);
                    canvas.DrawOval(x - dotRadius, y - dotRadius, x + dotRadius, y + dotRadius, blackPen);
                }
            }
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

        protected override void OnDraw(Canvas canvas) {
            base.OnDraw(canvas);

            // Safe with updating ui
            Monitor.Enter(this);

            var grayPen = new Paint {
                Color = Color.Gray,
                StrokeWidth = 2
            };
            grayPen.SetStyle(Paint.Style.Stroke);

            var darkGrayPen = new Paint {
                Color = Color.DarkGray,
                StrokeWidth = 2
            };
            darkGrayPen.SetStyle(Paint.Style.Stroke);

            // Plot Fft
            float xPerIndex = Width / (float)fft.Length;
            if (fft != null) {
                for (int i = 0; i + 1 < fft.Length; i += 2) {
                    float y = Math.Max(fft[i], fft[i + 1]) * 40;
                    canvas.DrawLine(xPerIndex * i, Height, xPerIndex * i, Height - y, grayPen);
                }
            }

            // Plot harmonics
            if (fundamentalFrequency != -1) {
                float freqPerIndex = (float)AudioIn.SAMPLE_RATE / FftProcessor.FFT_LENGTH;
                for (int i = 0; i < harmonicValues.Length; i++) {
                    float x1 = (i + 1) * fundamentalFrequency / freqPerIndex * xPerIndex;
                    float y1 = Height / 2 - harmonicValues[i] * 12f * (float)Math.Sqrt(i + 1);
                    if (i < harmonicValues.Length - 1) {
                        float x2 = (i + 2) * fundamentalFrequency / freqPerIndex * xPerIndex;
                        float y2 = Height / 2 - harmonicValues[i + 1] * 12f * (float)Math.Sqrt(i + 2);
                        canvas.DrawLine(x1, y1, x2, y2, grayPen);
                    }
                    //grayPen.TextSize = 18;
                    //canvas.DrawText("" + (harmonicValues[i] * 10).ToString("0.0"), x1 - 21, Height / 3, grayPen);
                }
            }

            // Make a formant grid
            // F1 on the horizontal, 0hz to 1400hz
            int f1Lines = 1400 / 200;
            float xPerF1Line = Width / (float)f1Lines;
            for (int i = 0; i < f1Lines; i++) {
                float x = xPerF1Line * i;
                canvas.DrawLine(x, 0, x, Height, darkGrayPen);
            }
            // F2 on the vertical, 500hz to 4000hz
            int f2Lines = 3500 / 500;
            float yPerF2Line = Height / (float)f1Lines;
            for (int i = 0; i < f2Lines; i++) {
                float y = yPerF2Line * i;
                canvas.DrawLine(0, y, Width, y, darkGrayPen);
            }

            float f1Scale = Width / 1400f;
            float f2Scale = Height / 3500f;

            Vowel[] vowels = GetVowels(targetLanguage);
            for (int i = 0; i < vowels.Length; i++) {
                // Draw the regions of the formant
                Vowel v = vowels[i];
                DrawVowelRegion(canvas, v);
            }
            // Draw current vowel on top again
            DrawVowelRegion(canvas, GetVowel(targetLanguage, targetVowel));

            var whitePen = new Paint { Color = Color.White, StrokeWidth = 1 };
            whitePen.SetStyle(Paint.Style.Stroke);
            whitePen.TextSize = 48;

            if (fundamentalFrequency != -1) {
                float f0 = fundamentalFrequency;
                var f1 = formants[0];
                var f2 = formants[1];
                var f3 = formants[2];

                for (int i = 0; i < lastDrawnPointsRoundness.Length; i++) {
                    if (lastDrawnPointsRoundness[i] != null) {
                        byte alpha = (byte)(128 * lastDrawnPointsRoundness.Length / (i + 1));
                        drawPronunciationDot(canvas, lastDrawnPointsRoundness[i], alpha);
                    }
                }
                // Always draw the most recent value, even if it has phased out of the last drawn points.
                drawPronunciationDot(canvas, calculatePronunciationDot(), 255);
                
                canvas.DrawText("F0: " + f0, 10, 50, whitePen);
                canvas.DrawText("F1: " + f1.freq, 10, 110, whitePen);
                canvas.DrawText("F2: " + f2.freq, 10, 170, whitePen);
                canvas.DrawText("F3: " + f3.freq, 10, 230, whitePen);
            }

            count++;
            canvas.DrawText("StdDev: " + stdDev, Width / 2 + 10, 50, whitePen);
            canvas.DrawText("Draw count: " + count, Width / 2 + 10, 110, whitePen);

            Monitor.Exit(this);
        }

        /// <summary>
        /// Convert HSV to RGB
        /// h is from 0-360 mod 360
        /// s,v values are 0-1
        /// r,g,b values are 0-255
        /// Based upon http://ilab.usc.edu/wiki/index.php/HSV_And_H2SV_Color_Space#HSV_Transformation_C_.2F_C.2B.2B_Code_2
        /// From: http://www.splinter.com.au/converting-hsv-to-rgb-colour-using-c/
        /// </summary>
        Color HsvToColor(double h, double S, double V) {
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
            return Color.Argb(255, r, g, b);
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