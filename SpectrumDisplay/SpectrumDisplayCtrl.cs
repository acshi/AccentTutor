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

namespace SpectrumDisplay {
    public partial class SpectrumDisplayCtrl : UserControl {
        AudioIn audioIn = new AudioIn(FftProcessor.SAMPLES_IN_UPDATE);
        FftProcessor fftProcessor = new FftProcessor();

        const float RECORDING_TOLERANCE = 0.002f;
        const int SPECTRUMS_PER_TAKE = 10;

        // Prevent race conditions with updating the UI
        ManualResetEvent safeToDrawUI = new ManualResetEvent(true);
        ManualResetEvent safeToChangeUIVariables = new ManualResetEvent(true);



        //VowelLearner vowelLearner = new VowelLearner();
        int currentVowelI = 0;
        bool isRecording = false;
        bool isAnalyzingFile = false;
        bool isLive = false;

        float[] fftData;
        float[] fft;
        float[] spectrum;
        Peak[] topPeaks;
        Peak[] fundamentalsPeaks;
        float[] harmonicValues;

        Formant[][] lastObservedFormants = new Formant[8][];
        Formant[][] lastAcceptedFormants = new Formant[3][]; // one array of last accepted values for each of f1, f2, and f3.
        //List<Formant> apparentFormants = new List<Formant>();
        Formant[] formants = new Formant[3];

        //List<int> peakIndices;
        VowelMatching[] vowelMatchings;
        //Tuple<string, Tuple<int, int>[], float> bestVowelMatching;

        float[] lastObservedFundamentals = new float[4];
        float[] lastChosenFundamentals = new float[4];
        float fundamentalFrequency = -1;

        public SpectrumDisplayCtrl() {
            for (int i = 0; i < lastAcceptedFormants.Length; i++) {
                lastAcceptedFormants[i] = new Formant[8];
            }

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

            string vowel = VowelData.Vowels[currentVowelI];

            saveVowelBtn.Text = "Save Vowel '" + vowel + "' " + VowelData.Descriptions[vowel];
            if (isRecording) {
                int completeness = 1000;//1000 * vowelLearner.GetSpectrumCount(vowel) / SPECTRUMS_PER_TAKE;
                completionLbl.Text = (completeness / 10f) + "% Complete";
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
            //vowelLearner.ClearVowel(VowelData.Vowels[currentVowelI]);
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
                float[] buffer = new float[FftProcessor.SAMPLES_IN_UPDATE];
                int iteration = 0;
                byte[] sampleBytes = new byte[reader.BlockAlign * buffer.Length];
                while (reader.Read(sampleBytes, 0, sampleBytes.Length) == sampleBytes.Length) {
                    for (int i = 0; i < buffer.Length; i++) {
                        buffer[i] = (short)(sampleBytes[i * reader.BlockAlign] | sampleBytes[i * reader.BlockAlign + 1] << 8);
                    }

                    fftProcessor.ProcessSamples(buffer);

                    // Give it time to animate a little
                    Thread.Sleep(200);
                    //Thread.Sleep(1000 * FftProcessor.SAMPLES_IN_FFT / AudioIn.SAMPLE_RATE);
                    iteration++;
                }
                reader.Close();
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
            safeToDrawUI.Reset();
            safeToChangeUIVariables.WaitOne(); // Block while painting UI

            //if (isLive) {
            spectrum = fft;
            //} else {
            //string vowel = VowelData.Vowels[currentVowelI];
            //spectrum = vowelLearner.GetSpectrum(vowel);
            //}

            if (spectrum != null) {
                spectrum = (float[])spectrum.Clone(); // So we can modify it in the high-pass filter

                // Square it to get power instead of just amplitude
                spectrum = spectrum.Select(v => v * v).ToArray();

                // Remove DC with a high-pass filter
                float lastIn = spectrum[0];
                float lastOut = lastIn;
                float alpha = 0.96f;
                for (int i = 1; i < spectrum.Length; i++) {
                    float inValue = spectrum[i];
                    float outValue = alpha * (lastOut + inValue - lastIn);
                    lastIn = inValue;
                    lastOut = outValue;
                    spectrum[i] = outValue;
                }

                // Z-score it, put negative values at 0.
                float average = spectrum.Average();
                float stdDev = (float)Math.Sqrt(spectrum.Select(num => (num - average) * (num - average)).Average());
                spectrum = spectrum.Select(num => Math.Max((num - average) / stdDev, 0)).ToArray();

                topPeaks = GetTopPeaks(spectrum, 32);
                float newFundamental = IdentifyFundamental(topPeaks, out fundamentalsPeaks);
                fundamentalFrequency = MakeSmoothedFundamental(newFundamental, lastObservedFundamentals, lastChosenFundamentals);

                if (fundamentalFrequency != -1) {
                    harmonicValues = EvaluateHarmonicSeries(spectrum, fundamentalFrequency);
                    var newFormants = IdentifyApparentFormants(fundamentalFrequency, harmonicValues, 0.8f);
                    var newFormants123 = IdentifyFormants123(newFormants);
                    formants = MakeSmoothedFormants123(newFormants123, lastObservedFormants, lastAcceptedFormants);
                    vowelMatchings = IdentifyVowel(formants, fundamentalFrequency);
                }
            } else {
                fundamentalFrequency = -1;
                topPeaks = null;
            }

            safeToDrawUI.Set();
            updateUI();
        }

        private void HandleFftData(object sender, FftDataAvailableHandlerArgs e) {
            fftData = e.FftData;

            float deltaFreq = (float)AudioIn.SAMPLE_RATE / FftProcessor.FFT_LENGTH;
            // Operate only on frequencies through MAX_FREQ that may be involved in formants
            fft = fftData.Take((int)(Analyzer.MAX_FREQ / deltaFreq + 0.5f)).ToArray();
            // Normalize the fft
            fft = fft.Select(a => (float)(a / Math.Sqrt(FftProcessor.FFT_LENGTH))).ToArray();
            // Negative values differ only by phase, use positive ones instead
            fft = fft.Select(a => Math.Abs(a)).ToArray();
            // Remove DC component (and close to it)
            fft[0] = fft[1] = fft[2] = 0;

            //string vowel = VowelData.Vowels[currentVowelI];
            /*if (!isLive) {
                vowelLearner.AddVowelFft(vowel, fft);
            }*/

            // Enough data now.
            /*if (isRecording && vowelLearner.GetSpectrumCount(vowel) > SPECTRUMS_PER_TAKE) {
                EndVowelCollection();
            }*/

            prepareVisualization();
        }

        protected override void OnPaint(PaintEventArgs pe) {
            safeToChangeUIVariables.Reset();
            safeToDrawUI.WaitOne(); // Block while GUI referenced things are referenced

            System.Drawing.Graphics graphics = pe.Graphics;

            float width = this.Width;
            float height = this.Height;

            float freqPerIndex = (float)AudioIn.SAMPLE_RATE / FftProcessor.FFT_LENGTH;

            graphics.ScaleTransform(1920 / 2000f, 1);
            // We can do two, but less is hard, so rescale to make it like 2
            if (freqPerIndex < 2) {
                graphics.ScaleTransform(freqPerIndex / 2f, 1);
            }
            Font font = new Font(FontFamily.GenericSerif, 14.0f);

            if (spectrum != null) {
                float deltaX = width / fftData.Length * 2;
                float partsPerX = (fftData.Length / 20) / width;
                for (int x = 0; x < spectrum.Length; x++) {
                    float y = spectrum[x] * 30;
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

            // Label the top peaks with their frequency and rank
            if (topPeaks != null) {
                int n = 0;
                foreach (var peak in topPeaks) {
                    float x = peak.maxIndex - 5.0f;//(peak.lowIndex + peak.highIndex) / 2f - 5.0f;
                    float y = Math.Max(height - 14 - peak.maxValue * 30, 100);
                    graphics.DrawString("" + ++n, font, Brushes.Black, x, y - 2);
                    float freq = peak.maxIndex * freqPerIndex;//(peak.lowIndex + peak.highIndex) / 2f * freqPerIndex;
                    graphics.DrawString("" + freq, font, Brushes.Black, x, y - 18);
                }
            }

            if (fundamentalFrequency != -1) {
                graphics.DrawString("Fundamental: " + fundamentalFrequency.ToString("0.00") + "hz", font, Brushes.Black, width - 200, 10);

                // Plot the harmonics
                for (int i = 0; i < harmonicValues.Length; i++) {
                    float x1 = (i + 1) * fundamentalFrequency / freqPerIndex;
                    float y1 = height / 2 - harmonicValues[i] * 8f;
                    if (i < harmonicValues.Length - 1) {
                        float x2 = (i + 2) * fundamentalFrequency / freqPerIndex;
                        float y2 = height / 2 - harmonicValues[i + 1] * 8f;
                        graphics.DrawLine(Pens.Black, x1, y1, x2, y2);
                    }

                    graphics.DrawString("" + harmonicValues[i].ToString("0.0"), font, Brushes.Black, x1 - 21, height / 2);
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
                    float y = height / 2 - 20;// - apFormantValue * 8f - 20;
                    graphics.DrawString("F" + (i + 1) + ":" + apFormantFreq.ToString("0"), font, Brushes.Black, x, y);

                    if (vowelMatchings != null) {
                        foreach (var vowelMatching in vowelMatchings.OrderBy(m => m.score).Index().Take(8)) {
                            int index = vowelMatching.Key;
                            float c = vowelMatching.Value.c;
                            var formantMatch = vowelMatching.Value.matches.Where(p => formants[p.Item2].freq == apFormantFreq);
                            if (formantMatch.Count() > 0) {
                                float vowelFormantFreq = (1 - c) * VowelData.FormantLows[vowelMatching.Value.vowel][formantMatch.First().Item2] +
                                                         c * VowelData.FormantHighs[vowelMatching.Value.vowel][formantMatch.First().Item2];
                                graphics.DrawString("V" + index + ":" + vowelFormantFreq.ToString("0"), font, Brushes.Black, x, y - 20 - index * 20);
                            }
                        }
                    }
                }

                if (vowelMatchings != null) {
                    foreach (var vowelMatching in vowelMatchings.OrderBy(m => m.score).Index().Take(8)) {
                        int index = vowelMatching.Key;
                        string vowel = vowelMatching.Value.vowel;
                        float score = vowelMatching.Value.score;
                        graphics.DrawString("Vowel " + index + ": /" + vowel + "/" + " score: " + score, font, Brushes.Black, width - 280, 30 + index * 40);
                        graphics.DrawString(VowelData.Descriptions[vowel], font, Brushes.Black, width - 280, 50 + index * 40);
                    }
                }
            }

            safeToChangeUIVariables.Set();
        }

        private void SpectrumDisplay_Resize(object sender, EventArgs e) {
            Invalidate();
        }

        private void processBtn_Click(object sender, EventArgs e) {
            prepareVisualization();
        }
    }
}
