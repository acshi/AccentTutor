using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AccentTutor {
    public class VowelLearner {
        // tuple of # of ffts added to spectrum and the sum of all those
        Dictionary<string, Tuple<int, float[]>> vowelSpectrums = new Dictionary<string, Tuple<int, float[]>>();

        // adds the positive real fft/fht to the running spectrum of the vowel
        // returns the % rms change caused in the running spectrum
        public float AddVowelFft(string vowel, float[] vowelFFt) {
            Tuple<int, float[]> vowelSpectrum;
            double rmsChange;
            if (vowelSpectrums.TryGetValue(vowel, out vowelSpectrum)) {
                int nFfts = vowelSpectrum.Item1;
                float[] newVowelSpectrum = vowelSpectrum.Item2.Zip(vowelFFt, (a, b) => (a * nFfts + b) / (nFfts + 1)).ToArray();
                rmsChange = Math.Sqrt(vowelSpectrum.Item2.Zip(newVowelSpectrum, (a, b) => (a - b) * (a - b)).Sum());
                vowelSpectrum = Tuple.Create(vowelSpectrum.Item1 + 1, newVowelSpectrum);
            } else {
                vowelSpectrum = Tuple.Create(1, vowelFFt);
                rmsChange = Math.Sqrt(vowelSpectrum.Item2.Select(a => a * a).Sum());
            }
            vowelSpectrums[vowel] = vowelSpectrum;
            return (float)rmsChange / vowelSpectrum.Item2.Sum();
        }
        
        static IEnumerable<float> Zeros() {
            while (true) {
                yield return 0;
            }
        }

        static IEnumerable<int> Naturals() {
            int value = 0;
            while (true) {
                yield return value;
                value++;
            }
        }

        // The average of all vowel spectrums, equally weighted per vowel
        public float[] GetAverageSpectrum() {
            return vowelSpectrums.Aggregate(Zeros(), (running, vowel) => vowel.Value.Item2.Zip(running, (a, b) => a + b))
                                 .Select(a => a / vowelSpectrums.Count).ToArray();
        }

        // The difference between the average spectrum and the specific vowel
        public float[] GetVowelFeatureSpectrum(string vowel) {
            return vowelSpectrums[vowel].Item2.Zip(GetAverageSpectrum(), (a, b) => a - b).ToArray();
        }

        // start index, end index, total power peaks from the vowel features
        public Tuple<int, int, float>[] GetTopPeaks(string vowel, int maxPeaks) {
            var values = vowelSpectrums[vowel].Item2;//GetVowelFeatureSpectrum(vowel);
            int MAX_ITERATIONS = 20;

            // index, value
            var possiblePeaks = new List<Tuple<int, int, float>>();

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
                for (int i = 0; i < values.Length; i++) {
                    if (values[i] > threshold) {
                        if (peakStartI == -1) {
                            peakStartI = i;
                        }
                        peakPower += values[i];
                    } else {
                        if (peakStartI != -1) {
                            possiblePeaks.Add(Tuple.Create<int, int, float>(peakStartI, i, peakPower));
                            peakStartI = -1;
                            peakPower = 0f;
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

            return possiblePeaks.OrderByDescending(peak => peak.Item3).ToArray();
        }

        // start index, end index, total power peaks from the vowel features
        public Tuple<int, int, float>[] GetTopPeaks2(string vowel) {
            var values = vowelSpectrums[vowel].Item2;//GetVowelFeatureSpectrum(vowel);

            // find z scores
            float average = values.Average();
            float stdDev = (float)Math.Sqrt(values.Select(num => (num - average) * (num - average)).Average());
            values = values.Select(num => Math.Max((num - average) / stdDev, 0)).ToArray();

            // Start index, end index, total value
            var possiblePeaks = new List<Tuple<int, int, float>>();
            // Identify peaks
            int peakStartI = -1;
            float peakPower = 0f;
            // Skip DC and very low frequencies
            for (int i = 3; i < values.Length; i++) {
                if (values[i] > 0f) {
                    if (peakStartI == -1) {
                        peakStartI = i;
                    }
                    peakPower += values[i];
                } else {
                    if (peakStartI != -1) {
                        possiblePeaks.Add(Tuple.Create<int, int, float>(peakStartI, i, peakPower));
                        peakStartI = -1;
                        peakPower = 0f;
                    }
                }
            }

            // Merge peaks that are close together if one could not be a peak on its own or is only a single index
            for (int i = 0; i + 1 < possiblePeaks.Count(); i++) {
                // If they are separated by only very little
                var peakA = possiblePeaks.ElementAt(i);
                var peakB = possiblePeaks.ElementAt(i + 1);
                if (peakA.Item2 + 1 >= peakB.Item1 &&
                   (peakA.Item1 == peakA.Item2 || peakA.Item3 < 1.0f ||
                    peakB.Item1 == peakB.Item2 || peakB.Item3 < 1.0f)) {
                    possiblePeaks.Insert(i, Tuple.Create<int, int, float>(peakA.Item1, peakB.Item2, peakA.Item3 + peakB.Item3));
                    possiblePeaks.Remove(peakA);
                    possiblePeaks.Remove(peakB);
                    i--;
                }
            }

            return possiblePeaks.OrderByDescending(peak => peak.Item3).Where(peak => peak.Item3 >= 1.0f).ToArray();
        }
    }
}
