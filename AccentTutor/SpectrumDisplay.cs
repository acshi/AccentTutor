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
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Single;
using static AccentTutor.SpectrumAnalyzer;

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
        float[] harmonicValues;

        List<Formant> apparentFormants = new List<Formant>();
        //List<int> peakIndices;
        VowelMatching[] vowelMatchings;
        //Tuple<string, Tuple<int, int>[], float> bestVowelMatching;

        float fundamentalFrequency = -1;

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

            string vowel = VowelData.Vowels[currentVowelI];

            saveVowelBtn.Text = "Save Vowel '" + vowel + "' " + VowelData.Descriptions[vowel];
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
            vowelLearner.ClearVowel(VowelData.Vowels[currentVowelI]);
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
            if (isLive) {
                spectrum = fft;
            } else {
                string vowel = VowelData.Vowels[currentVowelI];
                spectrum = vowelLearner.GetSpectrum(vowel);
            }

            if (spectrum != null) {
                spectrum = (float[])spectrum.Clone(); // So we can modify it in the high-pass filter

                // Transform the specturm to reflect how the vocal chords produce both low and high frequencies quieter than middle frequencies
                // First boost low frequencies < 300hz about
                float deltaFreq = (float)AudioIn.SAMPLE_RATE / FftProcessor.SAMPLES_IN_FFT;
                spectrum = spectrum.Index().Select(v => v.Value / (1.05f - (float)Math.Exp(-Math.Max(v.Key * deltaFreq - 210, 0) / 150f))).ToArray();
                // Then boost high frequencies > 1000hz about
                spectrum = spectrum.Index().Select(v => v.Value * (float)Math.Exp((v.Key * deltaFreq - 1000) / 1300)).ToArray();

                // Remove DC with a high-pass filter, 
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

                topPeaks = SpectrumAnalyzer.GetTopPeaks(spectrum, 16);
                fundamentalFrequency = SpectrumAnalyzer.IdentifyFundamental(topPeaks, out fundamentalsPeaks);
                if (fundamentalFrequency != -1) {
                    harmonicValues = SpectrumAnalyzer.EvaluateHarmonicSeries(spectrum, fundamentalFrequency);
                }

                apparentFormants = SpectrumAnalyzer.IdentifyApparentFormants(harmonicValues, 0.2f);

                vowelMatchings = SpectrumAnalyzer.IdentifyVowel(apparentFormants, fundamentalFrequency);
                //bestVowelMatching = vowelMatchings.First();
            } else {
                fundamentalFrequency = -1;
                topPeaks = null;
            }

            updateUI();
        }

        private void HandleFftData(object sender, FftDataAvailableHandlerArgs e) {
            fftData = e.FftData;

            float deltaFreq = (float)AudioIn.SAMPLE_RATE / FftProcessor.SAMPLES_IN_FFT;
            // Operate only on frequencies through MAX_FREQ that may be involved in formants
            fft = fftData.Take((int)(SpectrumAnalyzer.MAX_FREQ / deltaFreq + 0.5f)).ToArray();
            // Normalize the fft
            fft = fft.Select(a => (float)(a / Math.Sqrt(FftProcessor.SAMPLES_IN_FFT))).ToArray();
            // Negative values differ only by phase, use positive ones instead
            fft = fft.Select(a => Math.Abs(a)).ToArray();
            // Remove DC component (and close to it)
            fft[0] = fft[1] = fft[2] = 0;

            string vowel = VowelData.Vowels[currentVowelI];
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

            float freqPerIndex = (float)AudioIn.SAMPLE_RATE / FftProcessor.SAMPLES_IN_FFT;

            graphics.ScaleTransform(1920 / 2000f, 1);
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
                foreach (var apparentFormant in apparentFormants) {
                    float apFormantIndex = apparentFormant.freqI;
                    float apFormantValue = apparentFormant.value;
                    float formantFreq = (apFormantIndex + 1) * fundamentalFrequency;
                    float x = formantFreq / freqPerIndex - 21;
                    if (x > 1920) { // Keep final formant text on the screen
                        x = 1920;
                    }
                    float y = height / 2 - apFormantValue * 8f - 20;
                    graphics.DrawString("F:" + formantFreq.ToString("0"), font, Brushes.Black, x, y);

                    if (vowelMatchings != null) {
                        foreach (var vowelMatching in vowelMatchings.OrderBy(m => m.score).Index().Take(8)) {
                            int index = vowelMatching.Key;
                            float c = vowelMatching.Value.c;
                            var formantMatch = vowelMatching.Value.matches.Where(p => apparentFormants[p.Item1].freqI == apFormantIndex);
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
        }

        private void SpectrumDisplay_Resize(object sender, EventArgs e) {
            Invalidate();
        }

        private void processBtn_Click(object sender, EventArgs e) {
            prepareVisualization();
        }

        private static bool HungarianAttemptAssigment(Matrix<float> mat, List<Tuple<int, int>> assignments) {
            int n = mat.RowCount;
            bool assignmentMade = false;
            // by row
            foreach (int r in Enumerable.Range(0, n).Except(assignments.Select(a => a.Item1))) {
                var row = mat.Row(r);
                // Make an assignment if there is at least one zero in a row
                var zeros = row.Index().Where(v => v.Value == 0f);
                // Eliminate any zeros that are crossed out because their column is already assigned
                zeros = zeros.Where(v => assignments.All(a => a.Item2 != v.Key));
                if (zeros.Count() == 1) {
                    assignments.Add(Tuple.Create(r, zeros.First().Key));
                    assignmentMade = true;
                }
            }
            // by column
            foreach (int c in Enumerable.Range(0, n).Except(assignments.Select(a => a.Item2))) {
                var column = mat.Column(c);
                // Make an assignment if there is at least one zero in a row
                var zeros = column.Index().Where(v => v.Value == 0f);
                // Eliminate any zeros that are crossed out because their row is already assigned
                zeros = zeros.Where(v => assignments.All(a => a.Item1 != v.Key));
                if (zeros.Count() == 1) {
                    assignments.Add(Tuple.Create(zeros.First().Key, c));
                    assignmentMade = true;
                }
            }
            return assignmentMade;
        }

        public static Tuple<int, int>[] HungarianAlgorithm(float[,] costMatrix) {
            int nRow = costMatrix.GetLength(0);
            int nCol = costMatrix.GetLength(1);
            int n = Math.Max(nRow, nCol);

            // https://en.wikipedia.org/wiki/Hungarian_algorithm

            // 1. Pad matrix size to be square
            Matrix<float> mat = new DenseMatrix(n);
            for (int i = 0; i < nRow; i++) {
                for (int j = 0; j < nCol; j++) {
                    mat[i, j] = costMatrix[i, j];
                }
            }

            // 2. Use max value as the padding value
            float maxVal = mat.Enumerate().Max();
            if (n > nCol) {
                for (int c = nCol; c < n; c++) {
                    mat.SetColumn(c, CreateVector.Dense(n, maxVal));
                }
            }
            if (n > nRow) {
                for (int r = nRow; r < n; r++) {
                    mat.SetRow(r, CreateVector.Dense(n, maxVal));
                }
            }

            // 3. Subtract min value of each row from that row
            for (int r = 0; r < n; r++) {
                var row = mat.Row(r);
                mat.SetRow(r, row - row.Min());
            }

            // make all assignments possible
            //var certainAssignments = new List<Tuple<int, int>>(); // row, column.
            //HungarianAttemptAssigment(mat, certainAssignments);

            // 4. Subtract min value of each column from that column
            for (int c = 0; c < n; c++) {
                var col = mat.Column(c);
                mat.SetColumn(c, col - col.Min());
            }

            // make all assignments possible
            //HungarianAttemptAssigment(mat, certainAssignments);

            Random rand = new Random();
            int iterations = 0;
            while (iterations++ < 128) {
                if (iterations > 126) {
                    iterations++;
                }

                // 5. Make all the assignments possible, starting with our "certain assignments"
                var assignments = new List<Tuple<int, int>>(); // row, column.
                //assignments.AddRange(certainAssignments);
                bool assignmentMade = true;
                while (assignmentMade) {
                    assignmentMade = false;
                    // First add all the singular zeros possible
                    assignmentMade = HungarianAttemptAssigment(mat, assignments);

                    if (!assignmentMade) {
                        // Then make at most one arbitrary assignment where zeros remain
                        // Some choices may not work, so choose randomly
                        foreach (int r in Enumerable.Range(0, n).Except(assignments.Select(a => a.Item1))) {
                            var row = mat.Row(r);
                            // Make an assignment if there is at least one zero in a row
                            var zeros = row.Index().Where(v => v.Value == 0f);
                            // Eliminate any zeros that are crossed out because their column is already assigned
                            zeros = zeros.Where(v => assignments.All(a => a.Item2 != v.Key));
                            int count = zeros.Count();
                            if (count >= 1) {
                                assignments.Add(Tuple.Create(r, zeros.ElementAt(rand.Next(count)).Key));
                                assignmentMade = true;
                                break;
                            }
                        }
                    }
                }

                // Everything is matched
                if (assignments.Count() == n) {
                    // Remove assignments involving padding
                    assignments.RemoveAll(a => a.Item1 >= nRow || a.Item2 >= nCol);
                    return assignments.ToArray();
                }

                // 6. Find the least number of lines that cover all zeros

                var markedCols = new List<int>();
                var markedRows = new List<int>();

                // Mark all rows having no assigments
                var justMarkedRows = Enumerable.Range(0, n).Where(rowI => assignments.All(a => a.Item1 != rowI));
                markedRows.AddRange(justMarkedRows);
                while (justMarkedRows.Count() > 0) {
                    // Mark all (unmarked) columns having zeros in newly marked rows
                    var justMarkedCols = justMarkedRows.SelectMany(rowI => mat.Row(rowI).Index().Where(v => v.Value == 0f).Select(v => v.Key))
                                                       .Except(markedCols).ToArray();
                    markedCols.AddRange(justMarkedCols);
                    // Mark all (unmarked) rows having assignments in newly marked columns
                    justMarkedRows = justMarkedCols.SelectMany(colI => assignments.Where(a => a.Item2 == colI).Select(a => a.Item1))
                                                   .Except(markedRows).ToArray();
                    markedRows.AddRange(justMarkedRows);
                }

                // 7. "Draw lines" through the marked columns and unmarked rows
                // From the remaining values, find the minimum, subtract it from
                // all the remaining values and add it to elements with lines from both columns and rows
                // (make sure that there are no lazy evaluations that happen after the mutation of the matrix)
                var remainingCells = Enumerable.Range(0, n).Except(markedCols).SelectMany(colI => markedRows.Select(rowI => Tuple.Create(rowI, colI))).ToArray();
                if (remainingCells.Count() > 0) {
                    float smallestVal = remainingCells.Select(c => mat[c.Item1, c.Item2]).Min();
                    remainingCells.ForEach(c => mat[c.Item1, c.Item2] -= smallestVal);
                    Enumerable.Range(0, n).Except(markedRows).ForEach(rowI => markedCols.ForEach(colI => mat[rowI, colI] += smallestVal));
                }
            }
            throw new Exception("Hungarian algorithm failed to find a solution in 128 iterations. It is likely implemented wrong.");
        }
    }
}
