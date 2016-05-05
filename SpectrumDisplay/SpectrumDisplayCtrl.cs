using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using AccentTutor;
using NAudio.Wave;
using System.Threading;
using MoreLinq;
using static SpectrumAnalyzer.Analyzer;
using SpectrumAnalyzer;
using static SpectrumAnalyzer.VowelData;
using System.IO;
using System.Diagnostics;

namespace SpectrumDisplay {
    public partial class SpectrumDisplayCtrl : UserControl {
        List<Formant[]> vowelObservations = new List<Formant[]>();

        AudioIn audioIn;
        FftProcessor fftProcessor;

        string language = "english";

        // Prevent race conditions with updating the UI
        //ManualResetEvent safeToDrawUI = new ManualResetEvent(true);
        //ManualResetEvent safeToChangeUIVariables = new ManualResetEvent(true);
        
        //int currentVowelI = 0;
        bool isMeasuring = false;
        bool isAnalyzingFile = false;
        bool isLive = false;
        
        float[] fft;
        float[] harmonicValues = null;

        float[][] lastFfts = new float[8][];
        float[] lastFundamentals = new float[8];

        //Formant[][] lastObservedFormants = new Formant[4][];
        //Formant[][] lastAcceptedFormants = new Formant[3][]; // one array of last accepted values for each of f1, f2, and f3.
        //List<Formant> apparentFormants = new List<Formant>();
        Formant[] formants = new Formant[3];

        //List<int> peakIndices;
        VowelMatching[] vowelMatchings;
        //Tuple<string, Tuple<int, int>[], float> bestVowelMatching;
        
        float fundamentalFrequency = -1;

        public SpectrumDisplayCtrl() {
            //for (int i = 0; i < lastAcceptedFormants.Length; i++) {
            //    lastAcceptedFormants[i] = new Formant[4];
            //}

            InitializeComponent();
            updateUI();

            this.SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
        }

        public void InitializeAudioAndFft() {
            if (audioIn == null) {
                audioIn = new AudioIn(FftProcessor.SAMPLES_IN_UPDATE);
                fftProcessor = new FftProcessor();
                audioIn.AudioAvilable += HandleAudioData;
                fftProcessor.FftDataAvilable += HandleFftData;
            }
        }

        private void updateUI() {
            // Make UI modification thread-safe
            if (InvokeRequired) {
                Invoke((MethodInvoker)updateUI);
                return;
            }

            if (isAnalyzingFile || isLive) {
                analyzeFileBtn.Enabled = false;
                saveVowelBtn.Enabled = false;
                if (isLive) {
                    processBtn.Enabled = false;
                } else {
                    liveBtn.Enabled = false;
                }
            } else {
                analyzeFileBtn.Enabled = true;
                saveVowelBtn.Enabled = true;
                liveBtn.Enabled = true;
                processBtn.Enabled = true;
            }

            measureBtn.Text = isMeasuring ? "Stop Measuring" : "Measure Vowel";
            observationsLbl.Text = vowelObservations.Count + " Observations";

            Invalidate();
        }

        private void measureBtn_Click(object sender, EventArgs e) {
            // Only allow either real time or file analysis at a time
            isMeasuring = !isMeasuring;
            updateUI();
        }

        private void saveVowelBtn_Click(object sender, EventArgs e) {
            saveFileDialog.FileName = openFileDialog.FileName.Substring(0, openFileDialog.FileName.IndexOf("."));
            if (saveFileDialog.ShowDialog() == DialogResult.OK) {
                StringBuilder csv = new StringBuilder("f1,f2,f3,a1,a2,a3\n");
                foreach (var o in vowelObservations) {
                    csv.Append(string.Join(",", o.Select(f => f.freq)) + ",");
                    csv.AppendLine(string.Join(",", o.Select(f => f.value)));
                }
                File.WriteAllText(saveFileDialog.FileName, csv.ToString());
            }
        }

        private void clearVowelBtn_Click(object sender, EventArgs e) {
            vowelObservations.Clear();
            updateUI();
        }

        private void liveBtn_Click(object sender, EventArgs e) {
            isLive = !isLive;
            if (isLive) {
                audioIn.Start();
            } else {
                audioIn.Stop();
            }
            prepareVisualization();
        }

        private void WavReadingThreadStart(WaveFileReader[] readers) {
            foreach (var reader in readers) {
                long samplesN = reader.SampleCount;
                float[] buffer = new float[FftProcessor.SAMPLES_IN_UPDATE];
                int iteration = 0;
                byte[] sampleBytes = new byte[reader.BlockAlign * buffer.Length];
                while (reader.Read(sampleBytes, 0, sampleBytes.Length) == sampleBytes.Length) {
                    for (int i = 0; i < buffer.Length; i++) {
                        buffer[i] = (short)(sampleBytes[i * reader.BlockAlign] | sampleBytes[i * reader.BlockAlign + 1] << 8);
                    }

                    // Only add the samples if they are not silent
                    fftProcessor.ProcessSamples(buffer);

                    // Give it time to animate a little
                    //Thread.Sleep(2000 * FftProcessor.SAMPLES_IN_UPDATE / AudioIn.SAMPLE_RATE);
                    iteration++;
                }
                reader.Close();
            }
            isAnalyzingFile = false;
            updateUI();
        }

        private void analyzeFileBtn_Click(object sender, EventArgs e) {
            // Only allow either real time or file analysis at a time
            if (openFileDialog.ShowDialog() == DialogResult.OK) {
                // Make an array of readers from all files selected
                List<WaveFileReader> readers = new List<WaveFileReader>(openFileDialog.FileNames.Length);
                foreach (string fileName in openFileDialog.FileNames) {
                    WaveFileReader fileReader = new WaveFileReader(fileName);
                    if (fileReader.CanRead && fileReader.WaveFormat.SampleRate == 44100 &&
                        fileReader.WaveFormat.BitsPerSample == 16 && fileReader.WaveFormat.Encoding == WaveFormatEncoding.Pcm) {
                        readers.Add(fileReader);
                    } else {
                        MessageBox.Show("Could not open the file " + openFileDialog.FileName + " as WAV file. Use only 44.1khz 16-bit PCM WAV.");
                        fileReader.Close();
                    }
                }
                if (readers.Count > 0) {
                    analyzeFileBtn.Enabled = false;
                    // Start a thread to read the flie piece by piece and shuffle it to be analyzed
                    // without blocking the ui in the mean time
                    Thread readingThread = new Thread(() => WavReadingThreadStart(readers.ToArray()));
                    readingThread.Start();
                    isAnalyzingFile = true;
                    updateUI();
                }
            }
        }
        
        private void HandleAudioData(object sender, AudioAvailableHandlerArgs e) {
            // Only add the samples if they are not silent
            fftProcessor.ProcessSamples(e.Samples);
        }

        private void prepareVisualization() {
            Monitor.Enter(this);
            //safeToDrawUI.Reset();
            //safeToChangeUIVariables.WaitOne(); // Block while painting UI

            //if (isLive) {
            //} else {
            //string vowel = VowelData.Vowels[currentVowelI];
            //spectrum = vowelLearner.GetSpectrum(vowel);
            //}

            if (fft != null) {
                fft = (float[])fft.Clone(); // So we can modify it in the high-pass filter

                // Square it to get power instead of just amplitude
                fft = fft.Select(v => v * v).ToArray();

                // Remove DC with a high-pass filter
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
                float stdDev = (float)Math.Sqrt(fft.Select(num => (num - average) * (num - average)).Average());
                fft = fft.Select(num => Math.Max((num - average) / stdDev, 0)).ToArray();
                
                // Consider it noise/silence if the stdDev is too low
                Debug.WriteLine(stdDev);
                if (stdDev > 1e7) {
                    /*topPeaks = GetTopPeaks(spectrum, 32);
                    float newFundamental = IdentifyFundamental(spectrum);//IdentifyFundamental(topPeaks, out fundamentalsPeaks);
                    fundamentalFrequency = MakeSmoothedFundamental(newFundamental, lastObservedFundamentals);//, lastChosenFundamentals);

                    if (fundamentalFrequency != -1) {
                        harmonicValues = EvaluateHarmonicSeries(spectrum, fundamentalFrequency);
                        var newFormants = IdentifyApparentFormants(fundamentalFrequency, harmonicValues, 0.8f);
                        var newFormants123 = IdentifyFormants123(newFormants, EnglishVowels);
                        formants = MakeSmoothedFormants123(newFormants123, lastObservedFormants);//, lastAcceptedFormants);
                        vowelMatchings = IdentifyVowel(formants, fundamentalFrequency);

                        if (isMeasuring) {
                            vowelObservations.Add(formants);
                        }
                    }*/

                    // Shift in the new fft
                    Array.Copy(lastFfts, 1, lastFfts, 0, lastFfts.Length - 1);
                    lastFfts[lastFfts.Length - 1] = fft;

                    // Shift in new fundamental
                    Array.Copy(lastFundamentals, 1, lastFundamentals, 0, lastFundamentals.Length - 1);
                    lastFundamentals[lastFundamentals.Length - 1] = IdentifyFundamental(fft);

                    PerformFormantAnalysis(lastFfts, lastFundamentals, out fundamentalFrequency, out harmonicValues, out formants);
                    vowelMatchings = IdentifyVowel(formants, fundamentalFrequency);

                    if (isMeasuring) {
                        vowelObservations.Add(formants);
                    }
                } else {
                    // Shift in null and 0 values to clear out the buffer.
                    Array.Copy(lastFfts, 1, lastFfts, 0, lastFfts.Length - 1);
                    lastFfts[lastFfts.Length - 1] = null;
                    Array.Copy(lastFundamentals, 1, lastFundamentals, 0, lastFundamentals.Length - 1);
                    lastFundamentals[lastFundamentals.Length - 1] = 0;
                }
            } else {
                fundamentalFrequency = -1;
            }

            //safeToDrawUI.Set();
            Monitor.Exit(this);
            updateUI();
        }

        private void HandleFftData(object sender, FftDataAvailableHandlerArgs e) {
            float[] rawFft = e.FftData;

            float deltaFreq = (float)AudioIn.SAMPLE_RATE / FftProcessor.FFT_LENGTH;
            // Operate only on frequencies through MAX_FREQ that may be involved in formants
            float[] newFft = rawFft.Take((int)(Analyzer.MAX_FREQ / deltaFreq + 0.5f)).ToArray();
            // Normalize the fft
            newFft = newFft.Select(a => (float)(a / Math.Sqrt(FftProcessor.FFT_LENGTH))).ToArray();
            // Negative values differ only by phase, use positive ones instead
            newFft = newFft.Select(a => Math.Abs(a)).ToArray();
            // Remove DC component (and close to it)
            newFft[0] = newFft[1] = newFft[2] = 0;

            fft = newFft;
            prepareVisualization();
        }

        protected override void OnPaint(PaintEventArgs pe) {
            Monitor.Enter(this);
            //safeToChangeUIVariables.Reset();
            //safeToDrawUI.WaitOne(); // Block while GUI referenced things are referenced

            Graphics graphics = pe.Graphics;
            
            float freqPerIndex = (float)AudioIn.SAMPLE_RATE / FftProcessor.FFT_LENGTH;

            graphics.ScaleTransform(1920 / 2000f, 1);
            // We can do two, but less is hard, so rescale to make it like 2
            if (freqPerIndex < 2) {
                graphics.ScaleTransform(freqPerIndex / 2f, 1);
            }
            Font font = new Font(FontFamily.GenericSerif, 14.0f);

            if (fft != null) {
                // Paint only every other entry to save time
                for (int x = 0; x + 1 < fft.Length; x += 2) {
                    float y = Math.Max(fft[x], fft[x + 1]) * 30;
                    //graphics.DrawLine(Pens.Black, x, Height, x + 1, Height - y);
                    graphics.FillRectangle(Brushes.Black, x, Height - y, 2, y);
                }
            }

            // Label the top peaks with their frequency and rank
            //if (topPeaks != null) {
            //    int n = 0;
            //    foreach (var peak in topPeaks) {
            //        float x = peak.maxIndex - 5.0f;//(peak.lowIndex + peak.highIndex) / 2f - 5.0f;
            //        float y = Math.Max(height - 14 - peak.maxValue * 30, 100);
            //        graphics.DrawString("" + ++n, font, Brushes.Black, x, y - 2);
            //        float freq = peak.maxIndex * freqPerIndex;//(peak.lowIndex + peak.highIndex) / 2f * freqPerIndex;
            //        graphics.DrawString("" + freq, font, Brushes.Black, x, y - 18);
            //    }
            //}

            if (fundamentalFrequency != -1) {
                graphics.DrawString("Fundamental: " + fundamentalFrequency.ToString("0.00") + "hz", font, Brushes.Black, Width - 200, 10);

                // Plot the harmonics
                for (int i = 0; i < harmonicValues.Length; i++) {
                    float x1 = (i + 1) * fundamentalFrequency / freqPerIndex;
                    float y1 = Height / 2 - harmonicValues[i] * 8f * (float)Math.Sqrt(i + 1);
                    if (i < harmonicValues.Length - 1) {
                        float x2 = (i + 2) * fundamentalFrequency / freqPerIndex;
                        float y2 = Height / 2 - harmonicValues[i + 1] * 8f * (float)Math.Sqrt(i + 2);
                        graphics.DrawLine(Pens.Black, x1, y1, x2, y2);
                    }

                    graphics.DrawString("" + harmonicValues[i].ToString("0.0"), font, Brushes.Black, x1 - 21, Height / 2);
                }

                // Draw peaks and formant frequencies
                for (int i = 0; i < formants.Length; i++) {
                    var apparentFormant = formants[i];
                    float apFormantFreq = apparentFormant.freq;
                    float apFormantValue = apparentFormant.value;
                    float x = apFormantFreq / freqPerIndex - 21;
                    /*if (x > 1920) { // Keep final formant text on the screen
                        x = 1920;
                    }*/
                    float y = Height / 2 - 20;// - apFormantValue * 8f - 20;
                    graphics.DrawString("F" + (i + 1) + ":" + apFormantFreq.ToString("0"), font, Brushes.Black, x, y);

                    if (vowelMatchings != null) {
                        foreach (var vowelMatching in vowelMatchings.OrderBy(m => m.score).Index().Take(4)) {
                            int index = vowelMatching.Key;
                            float c = vowelMatching.Value.c;
                            var formantMatch = vowelMatching.Value.matches.Where(p => formants[p.Item2].freq == apFormantFreq);
                            if (formantMatch.Count() > 0) {
                                Vowel v = GetVowel(language, vowelMatching.Value.vowel);
                                float vowelFormantFreq = (1 - c) * v.formantLows[formantMatch.First().Item2] +
                                                         c * v.formantHighs[formantMatch.First().Item2];
                                graphics.DrawString("V" + index + ":" + vowelFormantFreq.ToString("0"), font, Brushes.Black, x, y - 20 - index * 20);
                            }
                        }
                    }
                }

                if (vowelMatchings != null) {
                    foreach (var vowelMatching in vowelMatchings.OrderBy(m => m.score).Index().Take(4)) {
                        int index = vowelMatching.Key;
                        string vowel = vowelMatching.Value.vowel;
                        Vowel v = GetVowel(language, vowel);
                        float score = vowelMatching.Value.score;
                        graphics.DrawString("Vowel " + index + ": /" + vowel + "/" + " score: " + score, font, Brushes.Black, Width - 280, 30 + index * 40);
                        graphics.DrawString(v.description, font, Brushes.Black, Width - 280, 50 + index * 40);
                    }
                }
            }

            //safeToChangeUIVariables.Set();
            Monitor.Exit(this);
        }

        private void SpectrumDisplay_Resize(object sender, EventArgs e) {
            Invalidate();
        }

        private void processBtn_Click(object sender, EventArgs e) {
            prepareVisualization();
        }
    }
}
