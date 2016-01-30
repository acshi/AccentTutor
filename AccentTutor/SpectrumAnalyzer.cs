using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AccentTutor {
    public static class SpectrumAnalyzer {
        public struct Peak {
            public int lowIndex;
            public int highIndex;
            public float totalValue;
            public float maxValue;

            public Peak(int lowIndex, int highIndex, float totalValue, float maxValue) {
                this.lowIndex = lowIndex;
                this.highIndex = highIndex;
                this.totalValue = totalValue;
                this.maxValue = maxValue;
            }
        }

        // start index, end index, total power peaks from the vowel features
        public static Peak[] GetTopPeaks(float[] values, int maxPeaks) {
            int MAX_ITERATIONS = 20;

            // low index, high index, value
            var possiblePeaks = new List<Peak>();

            // Do a binary search of threshold values so that we don't get more than maxPeaks peaks.
            // But return the largest number of peaks possible
            float max = values.Max();
            float min = values.Min();
            float threshold = (max + min) / 2;
            float delta = threshold;
            int iterationOn = 0;
            while (iterationOn < MAX_ITERATIONS) {
                iterationOn++;
                //possiblePeaks = values.Zip(Naturals(), (v, i) => Tuple.Create(i, v)).Where(a => a.Item2 > threshold).ToArray();
                possiblePeaks.Clear();
                int peakStartI = -1;
                float peakPower = 0f;
                float maxPeak = 0f;
                for (int i = 0; i < values.Length; i++) {
                    if (values[i] > threshold) {
                        if (peakStartI == -1) {
                            peakStartI = i;
                        }
                        peakPower += values[i];
                        maxPeak = Math.Max(maxPeak, values[i]);
                    } else {
                        if (peakStartI != -1) {
                            possiblePeaks.Add(new Peak(peakStartI, i, peakPower, maxPeak));
                            peakStartI = -1;
                            peakPower = 0f;
                            maxPeak = 0f;
                        }
                    }
                }
                if (possiblePeaks.Count == maxPeaks) {
                    break;
                } else if (possiblePeaks.Count > maxPeaks) {
                    delta /= 2;
                    threshold += delta;
                } else if (possiblePeaks.Count < maxPeaks) {
                    delta /= 2;
                    threshold -= delta;
                }
            }

            return possiblePeaks.OrderByDescending(peak => peak.totalValue).ToArray();
        }

        // Attempts to find the fundamental frequency captured by the fft values.
        public static float IdentifyFundamental(SpectrumAnalyzer.Peak[] topPeaks, out SpectrumAnalyzer.Peak[] fundamentalsPeaks) {
            // Identify fundamental
            float freqPerIndex = (float)AudioIn.SAMPLE_RATE / FftProcessor.SAMPLES_IN_FFT;

            float bestFundamentalFreq = 0;
            fundamentalsPeaks = null;
            float bestScore = 0;
            var orderedPeaks = topPeaks.OrderBy(peak => peak.lowIndex);
            // Consider all the deltas between peaks
            var potentialFundamentals = orderedPeaks.Zip(orderedPeaks.Skip(1), (a, b) => {
                int lowBound = b.lowIndex - a.highIndex; // low bound for index of frequency
                int highBound = b.highIndex - a.lowIndex; // range of frequency above the low bound
                return (lowBound + highBound) / 2f;
            });
            // as well as the peaks themselves
            potentialFundamentals = potentialFundamentals.Concat(orderedPeaks.Select(p => (p.lowIndex + p.highIndex) / 2f));

            // Remove very low frequencies. We are talking about people here!
            potentialFundamentals = potentialFundamentals.Where(a => a * freqPerIndex >= 50f);

            foreach (var fund in potentialFundamentals) {
                // How many of the top peaks are a multiple of this?
                var matchingPeaks = orderedPeaks.Where(p => {
                    int harmonicNum = (int)Math.Round(p.highIndex / fund);
                    float offset = harmonicNum * fund;
                    float tolerance = Math.Max((float)Math.Log(fund) * 4f, p.highIndex / 100f);
                    return harmonicNum > 0 && p.lowIndex - offset <= tolerance && p.highIndex - offset >= -tolerance;
                });
                // Remove duplicate peaks with the same harmonic number (group them by harmonic number then take the highest scoring of each group)
                matchingPeaks = matchingPeaks.GroupBy(p => Math.Round(p.highIndex / fund)).Select(group => group.OrderByDescending(p => p.maxValue).First());

                if (matchingPeaks.Count() > 0) {
                    // This scoring function is the most important part for correctly determining the fundamental
                    float score = matchingPeaks.Count() * matchingPeaks.Sum(p => p.maxValue) * fund;
                    float freq = matchingPeaks.Select(p => (p.highIndex + p.lowIndex) / (float)Math.Round((p.highIndex + p.lowIndex) / (fund * 2f))).Average() / 2f * freqPerIndex;
                    if (score > bestScore) {
                        bestScore = score;
                        bestFundamentalFreq = freq;
                        fundamentalsPeaks = matchingPeaks.ToArray();
                    }
                }
            }
            return bestFundamentalFreq;
        }

    }
}
