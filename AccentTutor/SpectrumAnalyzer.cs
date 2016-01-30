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
        public static float IdentifyFundamental(float[] values) {

            return 0;
        }
    }
}
