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

namespace AccentTutor {
    public partial class SpectrumDisplay : UserControl {
        AudioIn audioIn = new AudioIn(FftProcessor.SAMPLES_IN_FFT);
        FftProcessor fftProcessor = new FftProcessor();

        const float RECORDING_TOLERANCE = 0.002f;
        const int SPECTRUMS_PER_TAKE = 10;

        VowelLearner vowelLearner = new VowelLearner();
        int currentVowelI = 0;
        bool isRecording = false;
        bool isAnalyzingFile = false;
        bool isLive = false;

        float[] fftData;
        float[] fft;
        float[] spectrum;
        SpectrumAnalyzer.Peak[] topPeaks;
        SpectrumAnalyzer.Peak[] fundamentalsPeaks;

        float fundamentalFrequency = -1;

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
            audioIn.AudioAvilable += HandleAudioData;
            fftProcessor.FftDataAvilable += HandleFftData;
            updateUI();

            this.SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
        }

        private void updateUI() {
            // Make UI modification thread-safe
            if (InvokeRequired) {
                Invoke((MethodInvoker)updateUI);
                return;
            }

            if (isRecording || isAnalyzingFile || isLive) {
                recordBtn.Enabled = false;
                analyzeFileBtn.Enabled = false;
                saveVowelBtn.Enabled = false;
                if (isLive) {
                    clearVowelBtn.Enabled = false;
                    processBtn.Enabled = false;
                } else {
                    liveBtn.Enabled = false;
                }
            } else {
                completionLbl.Text = "Not Recording";
                recordBtn.Enabled = true;
                analyzeFileBtn.Enabled = true;
                saveVowelBtn.Enabled = true;
                liveBtn.Enabled = true;
                clearVowelBtn.Enabled = true;
                processBtn.Enabled = true;
            }

            string vowel = vowelsToRecord[currentVowelI, 0];

            saveVowelBtn.Text = "Save Vowel '" + vowel + "' " + vowelsToRecord[currentVowelI, 1];
            if (isRecording) {
                int completeness = 100 * vowelLearner.GetSpectrumCount(vowel) / SPECTRUMS_PER_TAKE;
                completionLbl.Text = ((completeness * 10) / 10f) + "% Complete";
            } else if (isAnalyzingFile) {
                completionLbl.Text = "Analyzing File";
            } else if (isLive) {
                completionLbl.Text = "Live View";
            }

            Invalidate();
        }

        private void recordBtn_Click(object sender, EventArgs e) {
            // Only allow either real time or file analysis at a time
            audioIn.Start();
            isRecording = true;
            updateUI();
        }

        private void saveVowelBtn_Click(object sender, EventArgs e) {
            currentVowelI++;
            prepareVisualization();
        }

        private void clearVowelBtn_Click(object sender, EventArgs e) {
            if (isRecording) {
                EndVowelCollection();
            }
            vowelLearner.ClearVowel(vowelsToRecord[currentVowelI, 0]);
            prepareVisualization();
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
                float[] buffer = new float[FftProcessor.SAMPLES_IN_FFT];
                int iteration = 0;
                byte[] sampleBytes = new byte[reader.BlockAlign * buffer.Length];
                while (reader.Read(sampleBytes, 0, sampleBytes.Length) == sampleBytes.Length) {
                    for (int i = 0; i < buffer.Length; i++) {
                        buffer[i] = (short)(sampleBytes[i * reader.BlockAlign] | sampleBytes[i * reader.BlockAlign + 1] << 8);
                    }

                    fftProcessor.ProcessSamples(buffer);

                    // Give it time to animate a little
                    Thread.Sleep(50);
                    //Thread.Sleep(1000 * FftProcessor.SAMPLES_IN_FFT / AudioIn.SAMPLE_RATE);
                    iteration++;
                }
            }
            EndVowelCollection();
        }

        private void analyzeFileBtn_Click(object sender, EventArgs e) {
            // Only allow either real time or file analysis at a time
            if (!isRecording && openFileDialog.ShowDialog() == DialogResult.OK) {
                // Make an array of readers from all files selected
                List<WaveFileReader> readers = new List<WaveFileReader>(openFileDialog.FileNames.Length);
                foreach (string fileName in openFileDialog.FileNames) {
                    WaveFileReader fileReader = new WaveFileReader(fileName);
                    if (fileReader.CanRead && fileReader.WaveFormat.SampleRate == 44100 &&
                        fileReader.WaveFormat.BitsPerSample == 16 && fileReader.WaveFormat.Encoding == WaveFormatEncoding.Pcm) {
                        readers.Add(fileReader);
                    } else {
                        MessageBox.Show("Could not open the file " + openFileDialog.FileName + " as WAV file. Use only 44.1khz 16-bit PCM WAV.");
                    }
                }
                if (readers.Count > 0) {
                    analyzeFileBtn.Enabled = false;
                    recordBtn.Enabled = false;
                    // Start a thread to read the flie piece by piece and shuffle it to be analyzed
                    // without blocking the ui in the mean time
                    Thread readingThread = new Thread(() => WavReadingThreadStart(readers.ToArray()));
                    readingThread.Start();
                    isAnalyzingFile = true;
                    updateUI();
                }
            }
        }

        private void EndVowelCollection() {
            if (isRecording) {
                audioIn.Stop();
            }
            isRecording = false;
            isAnalyzingFile = false;
            updateUI();
        }

        // Just pass microphone data on to the fft processor
        private void HandleAudioData(object sender, AudioAvailableHandlerArgs e) {
            fftProcessor.ProcessSamples(e.Samples);
        }

        private void prepareVisualization() {
            if (isLive) {
                spectrum = fft;
            } else {
                string vowel = vowelsToRecord[currentVowelI, 0];
                spectrum = vowelLearner.GetSpectrum(vowel);
            }

            if (spectrum != null) {
                // Z-score it
                float average = spectrum.Average();
                float stdDev = (float)Math.Sqrt(spectrum.Select(num => (num - average) * (num - average)).Average());
                spectrum = spectrum.Select(num => Math.Max((num - average) / stdDev, 0)).ToArray();

                topPeaks = SpectrumAnalyzer.GetTopPeaks(spectrum, 16);
                fundamentalFrequency = SpectrumAnalyzer.IdentifyFundamental(topPeaks, out fundamentalsPeaks);
            } else {
                fundamentalFrequency = -1;
                topPeaks = null;
            }

            updateUI();
        }

        private void HandleFftData(object sender, FftDataAvailableHandlerArgs e) {
            fftData = e.FftData;

            float deltaFreq = (float)AudioIn.SAMPLE_RATE / FftProcessor.SAMPLES_IN_FFT;
            // Operate only on frequencies through 4000Hz that may be involved in formants
            fft = fftData.Take((int)(4000f / deltaFreq + 0.5f)).ToArray();
            // Normalize the fft
            fft = fft.Select(a => (float)(a / Math.Sqrt(FftProcessor.SAMPLES_IN_FFT))).ToArray();
            // Negative values differ only by phase, use positive ones instead
            fft = fft.Select(a => Math.Abs(a)).ToArray();
            // Remove DC component (and close to it)
            fft[0] = fft[1] = fft[2] = 0;

            string vowel = vowelsToRecord[currentVowelI, 0];
            if (!isLive) {
                vowelLearner.AddVowelFft(vowel, fft);
            }

            // Enough data now.
            if (isRecording && vowelLearner.GetSpectrumCount(vowel) > SPECTRUMS_PER_TAKE) {
                EndVowelCollection();
            }

            prepareVisualization();
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
                    // Draw the peak centers in different colors
                    if (fundamentalsPeaks != null && fundamentalsPeaks.Any(peak => peak.lowIndex <= x && x <= peak.highIndex)) {
                        graphics.DrawLine(Pens.Magenta, x, height, x, height - y);
                    } else if (topPeaks != null && topPeaks.Any(peak => peak.lowIndex <= x && x <= peak.highIndex)) {
                        graphics.DrawLine(Pens.Red, x, height, x, height - y);
                    } else {
                        graphics.DrawLine(Pens.Black, x, height, x, height - y);
                    }
                }
            }

            Font f = new Font(FontFamily.GenericSerif, 14.0f);
            if (topPeaks != null) {
                float deltaFreq = (float)AudioIn.SAMPLE_RATE / FftProcessor.SAMPLES_IN_FFT;

                int n = 0;
                foreach (var peak in topPeaks) {
                    float x = (peak.lowIndex + peak.highIndex) / 2f - 5.0f;
                    float y = Math.Max(height - 14 - peak.maxValue * 30, 100);
                    graphics.DrawString("" + ++n, f, Brushes.Black, x, y - 2);
                    float freq = (peak.lowIndex + peak.highIndex) / 2f * deltaFreq;
                    graphics.DrawString("" + freq, f, Brushes.Black, x, y - 18);
                }
            }

            if (fundamentalFrequency != -1) {
                graphics.DrawString("Fundamental: " + fundamentalFrequency.ToString("0.00") + "hz", f, Brushes.Black, width - 200, 30);
            }
        }

        private void SpectrumDisplay_Resize(object sender, EventArgs e) {
            Invalidate();
        }

        private void processBtn_Click(object sender, EventArgs e) {
            prepareVisualization();
        }
    }
}
