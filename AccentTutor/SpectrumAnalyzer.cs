using MoreLinq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AccentTutor {
    public static class SpectrumAnalyzer {
        public const float MAX_FREQ = 4000f;

        public struct Peak {
            public int lowIndex;
            public int highIndex;
            public float totalValue;
            public float maxValue;
            public int maxIndex;

            public Peak(int lowIndex, int highIndex, float totalValue, float maxValue, int maxIndex) {
                this.lowIndex = lowIndex;
                this.highIndex = highIndex;
                this.totalValue = totalValue;
                this.maxValue = maxValue;
                this.maxIndex = maxIndex;
            }
        }

        public struct Formant {
            public float freqI;
            public float value;

            public Formant(float freqI, float value) {
                this.freqI = freqI;
                this.value = value;
            }
        }

        public struct VowelMatching {
            public string vowel;
            public float c;
            public Tuple<int, int>[] matches;
            public float score;

            public VowelMatching(string vowel, float c, Tuple<int, int>[] matches, float score) {
                this.vowel = vowel;
                this.c = c;
                this.matches = matches;
                this.score = score;
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
                int maxIndex = -1;
                for (int i = 0; i < values.Length; i++) {
                    if (values[i] > threshold) {
                        if (peakStartI == -1) {
                            peakStartI = i;
                        }
                        peakPower += values[i];
                        if (values[i] > maxPeak) {
                            maxPeak = values[i];
                            maxIndex = i;
                        }
                    } else {
                        if (peakStartI != -1) {
                            possiblePeaks.Add(new Peak(peakStartI, i, peakPower, maxPeak, maxIndex));
                            peakStartI = -1;
                            peakPower = 0f;
                            maxPeak = 0f;
                            maxIndex = -1;
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

            return possiblePeaks.OrderByDescending(peak => peak.maxValue).ToArray();
        }

        // Attempts to find the fundamental frequency captured by the fft values.
        public static float IdentifyFundamental(SpectrumAnalyzer.Peak[] topPeaks, out SpectrumAnalyzer.Peak[] fundamentalsPeaks) {
            // Identify fundamental
            float freqPerIndex = (float)AudioIn.SAMPLE_RATE / FftProcessor.SAMPLES_IN_FFT;

            float bestFundamentalFreq = -1;
            fundamentalsPeaks = null;
            float bestScore = 0;
            var orderedPeaks = topPeaks.OrderBy(peak => peak.lowIndex);
            // Consider all the deltas between peaks, both by amplitude, and then sequence
            var potentialFundamentals = MoreEnumerable.Zip(topPeaks, orderedPeaks.Skip(1), (a, b) => {
                //int lowBound = b.lowIndex - a.highIndex; // low bound for index of frequency
                //int highBound = b.highIndex - a.lowIndex; // range of frequency above the low bound
                return (float)Math.Abs(b.maxIndex - a.maxIndex);//(lowBound + highBound) / 2f;
            });
            potentialFundamentals = potentialFundamentals.Concat(MoreEnumerable.Zip(orderedPeaks, orderedPeaks.Skip(1), (a, b) => {
                //int lowBound = b.lowIndex - a.highIndex; // low bound for index of frequency
                //int highBound = b.highIndex - a.lowIndex; // range of frequency above the low bound
                return (float)b.maxIndex - a.maxIndex;//(lowBound + highBound) / 2f;
            }));
            // as well as the peaks themselves
            potentialFundamentals = potentialFundamentals.Concat(orderedPeaks.Select(p => (float)p.maxIndex));//(p.lowIndex + p.highIndex) / 2f));

            // Remove very low frequencies. We are talking about people here!
            potentialFundamentals = potentialFundamentals.Where(a => a * freqPerIndex >= 50f);

            foreach (var fund in potentialFundamentals) {
                // How many of the top peaks are a multiple of this?
                var matchingPeaks = orderedPeaks.Where(p => {
                    int harmonicNum = (int)Math.Round(p.highIndex / fund);
                    float offset = harmonicNum * fund;
                    float tolerance = Math.Max((float)Math.Log10(fund) * 4f, p.highIndex / 100f);
                    return harmonicNum > 0 && p.lowIndex - offset <= tolerance && p.highIndex - offset >= -tolerance;
                });
                // Remove duplicate peaks with the same harmonic number (group them by harmonic number then take the highest scoring of each group)
                matchingPeaks = matchingPeaks.GroupBy(p => Math.Round(p.highIndex / fund)).Select(group => group.OrderByDescending(p => p.maxValue).First());

                if (matchingPeaks.Count() > 0) {
                    // This scoring function is the most important part for correctly determining the fundamental
                    // Multiply all the maxValues together for the score
                    float score = matchingPeaks.Count() * matchingPeaks.Aggregate(1f, (v, p) => v * Math.Max(1, p.maxValue)) * fund;
                    float freq = matchingPeaks.Select(p => p.maxIndex / (float)Math.Round(p.maxIndex / fund)).Average() * freqPerIndex;//fund * freqPerIndex;//matchingPeaks.Select(p => (p.highIndex + p.lowIndex) / (float)Math.Round((p.highIndex + p.lowIndex) / (fund * 2f))).Average() / 2f * freqPerIndex;
                    if (score > bestScore) {
                        bestScore = score;
                        bestFundamentalFreq = freq;
                        fundamentalsPeaks = matchingPeaks.ToArray();
                    }
                }
            }
            return bestFundamentalFreq;
        }

        // Finds the amplitude of each harmonic based on the given fundamental
        public static float[] EvaluateHarmonicSeries(float[] spectrum, float fundamentalFreq) {
            float freqPerIndex = (float)AudioIn.SAMPLE_RATE / FftProcessor.SAMPLES_IN_FFT;
            int maxHarmonics = (int)Math.Floor(MAX_FREQ / fundamentalFreq);

            return Enumerable.Range(1, maxHarmonics).Select(n => {
                float fundI = fundamentalFreq / freqPerIndex;
                float harmonicI = n * fundI;
                float tolerance = Math.Max((float)Math.Log(fundI) * 4f, harmonicI / 100f);
                int lowIndex = Math.Max(0, (int)Math.Floor(harmonicI - tolerance));
                int highIndex = Math.Min(spectrum.Length - 1, (int)Math.Ceiling(harmonicI + tolerance));
                // Find the indexes of the lower and upper bounds of the harmonic and then sum up the components
                //return Enumerable.Range(lowIndex, highIndex - lowIndex)
                //       .Select(i => spectrum[i]).Sum();
                // Find the maximum value in the range
                return Enumerable.Range(lowIndex, highIndex - lowIndex)
                       .Select(i => spectrum[i]).Max();
            }).ToArray();
        }

        // Finds the peaks of the harmonic values, using the weighted average of neighboring peaks, and also combining
        // peaks when their neighbors differ by only the given percentage change difference. Peak power 10 followed by 12 => .167 difference.
        // Returns list of index, magnitude pairs.
        public static List<Formant> IdentifyApparentFormants(float[] harmonicValues, float sameFormantDifference) {
            // Find the peaks in the harmonics, the points both ascended to and descended from
            var peakIndices = new List<int>();
            if (harmonicValues != null) {
                // First item only has to be descended from
                if (harmonicValues.Length > 1 && harmonicValues[0] > harmonicValues[1]) {
                    peakIndices.Add(0);
                }
                for (int i = 1; i < harmonicValues.Length - 1; i++) {
                    if (harmonicValues[i] > harmonicValues[i - 1] &&
                        harmonicValues[i] > harmonicValues[i + 1]) {
                        peakIndices.Add(i);
                    }
                }
                // Vice verca for last item
                if (harmonicValues.Length > 1 && harmonicValues[harmonicValues.Length - 1] > harmonicValues[harmonicValues.Length - 2]) {
                    peakIndices.Add(harmonicValues.Length - 1);
                }
            }

            // Also make sure to try matching against the high values that may not be peaks
            // IF, the fundamental isn't too high
            /*if (fundamentalFrequency <= 200) {
                peakIndices.AddRange(harmonicValues.Index().OrderByDescending(v => v.Value).Take(2).Select(v => v.Key).Except(peakIndices));
                peakIndices.Sort();
            }*/

            var apparentFormants = new List<Formant>();
            // Make formants out of these peak indices
            // Consectuive peaks are combined into a single formant
            // But only if the fundamental isn't too high
            int consecutivePeakCount = 0;
            for (int i = 0; i < peakIndices.Count; i++) {
                int peakIndex = peakIndices[i];
                // If the line of consecutive peaks is over
                if (i + 1 == peakIndices.Count || peakIndices[i + 1] != peakIndex + 1) {//fundamentalFrequency > 200 || (i + 1 == peakIndices.Count || peakIndices[i + 1] != peakIndex + 1)) {
                    float weightedPeakIndex = 0;
                    float sumFormantValue = 0;
                    float maxFormantValue = 0;
                    for (int j = peakIndex - consecutivePeakCount; j <= peakIndex; j++) {
                        weightedPeakIndex += j * harmonicValues[j];
                        sumFormantValue += harmonicValues[j];
                        maxFormantValue = Math.Max(maxFormantValue, harmonicValues[j]);
                    }
                    int oneBeforeIndex = peakIndex - consecutivePeakCount - 1;
                    if (oneBeforeIndex >= 0) {
                        float oneBeforeValue = harmonicValues[oneBeforeIndex];
                        float firstValue = harmonicValues[peakIndex - consecutivePeakCount];
                        if (Math.Abs(oneBeforeValue - firstValue) / Math.Max(oneBeforeValue, firstValue) < 0.2f) {
                            weightedPeakIndex += oneBeforeIndex * oneBeforeValue;
                            sumFormantValue += oneBeforeValue;
                        }
                    }
                    int oneAfterIndex = peakIndex + 1;
                    if (oneAfterIndex < harmonicValues.Length) {
                        float oneAfterValue = harmonicValues[oneAfterIndex];
                        float lastValue = harmonicValues[peakIndex];
                        if (Math.Abs(oneAfterValue - lastValue) / Math.Max(oneAfterValue, lastValue) < 0.2f) {
                            weightedPeakIndex += oneAfterIndex * oneAfterValue;
                            sumFormantValue += oneAfterValue;
                        }
                    }
                    weightedPeakIndex /= sumFormantValue;
                    apparentFormants.Add(new Formant(weightedPeakIndex, maxFormantValue));
                    consecutivePeakCount = 0;
                } else {
                    consecutivePeakCount++;
                }
            }

            return apparentFormants;
        }

        private static Tuple<string, Formant>[] IdentifyFirstFormants(List<Formant> apparentFormants, float fundamentalFrequency) {
            float deltaFreq = (float)AudioIn.SAMPLE_RATE / FftProcessor.SAMPLES_IN_FFT;
            // For each vowel...
            return VowelData.Vowels.Select(vowel => {
                // Choose the lowest formant with that best fits in that vowels first formant range
                Formant bestFormant = apparentFormants.Select(apFormant => {
                    float apFreq = apFormant.freqI * deltaFreq;
                    // The distances are positive in the amount the frequency is out of the total formant range
                    float distanceL = Math.Max(0f, VowelData.FormantLows[vowel][0] - apFreq);
                    float distanceH = Math.Max(0f, apFreq - VowelData.FormantHighs[vowel][0]);
                    return Tuple.Create(Math.Max(distanceL, distanceH), apFormant);
                }).MaxBy(v => v.Item1).Item2;
                return Tuple.Create(vowel, bestFormant);
            }).ToArray();
        }

        public static VowelMatching[] IdentifyVowel(List<Formant> apparentFormants, float fundamentalFrequency) {
            float deltaFreq = (float)AudioIn.SAMPLE_RATE / FftProcessor.SAMPLES_IN_FFT;

            // First formants are determined separely, as the second and third will be in terms of the first
            var vowelMatchings = VowelData.Vowels.Select(vowel => {
                VowelMatching bestMatching = new VowelMatching();
                bestMatching.score = float.MaxValue;
                for (int i = 0; i <= 10; i++) {
                    // Proportion constant for this vowel between low and high values
                    float c = i / 10f;

                    int vowelFormantN = VowelData.FormantLows[vowel].Length;
                    var costsMatrix = new float[apparentFormants.Count(), vowelFormantN];
                    for (int j = 0; j < apparentFormants.Count(); j++) {
                        float apFormantIndex = apparentFormants[j].freqI;
                        float apFormantValue = apparentFormants[j].value;
                        for (int k = 0; k < vowelFormantN; k++) {
                            float vowelFormantFreq = (1 - c) * VowelData.FormantLows[vowel][k] + c * VowelData.FormantHighs[vowel][k];
                            float vFormantI = vowelFormantFreq / fundamentalFrequency - 1;
                            float misalign = Math.Abs(apFormantIndex - vFormantI);
                            if (misalign <= 0.5) {
                                misalign = 0; // Do not penalize when the mismatch is below the resolution of the fundamental frequency.
                            }
                            if (apFormantValue == 0.0) {
                                costsMatrix[j, k] = float.MaxValue; // Prevent unfeasable choice (and possible divide by 0)
                            } else {
                                // formants should have a certain relative amplitude
                                // We make this relative to a fixed value (thankfully we have z-scored these values before)
                                int decibels = VowelData.FormantAmplitudes[vowel][k] - VowelData.FormantAmplitudes[vowel][0];
                                float amplitude = (float)Math.Pow(10, decibels / 20.0);
                                float desiredValue = 10f * amplitude;
                                float valueRatio = Math.Max(desiredValue, apFormantValue) / Math.Min(desiredValue, apFormantValue);
                                // Penalize incorrect frequency, but go easy when amplitude is supposed to be low
                                costsMatrix[j, k] = (deltaFreq * misalign) * amplitude + amplitude * (valueRatio - 1) + 4f / apFormantValue;
                            }
                        }
                    }
                    var bestPairs = Munkres.MunkresSolver.MunkresAssignment(costsMatrix);
                    var newMatching = new VowelMatching(vowel, c, bestPairs, bestPairs.Sum(p => costsMatrix[p.Item1, p.Item2]));
                    if (newMatching.score < bestMatching.score) {
                        bestMatching = newMatching;
                    }
                }
                return bestMatching;
            });
            //var bestVowelMatching = vowelMatchings.MinBy(m => m.Item3);
            return vowelMatchings.OrderBy(m => m.score).ToArray();
        }

        public static VowelMatching[] IdentifyVowel2(List<Formant> apparentFormants, float fundamentalFrequency) {
            float deltaFreq = (float)AudioIn.SAMPLE_RATE / FftProcessor.SAMPLES_IN_FFT;

            // First formants are determined separely, as the second and third will be in terms of the first
            var vowelMatchings = IdentifyFirstFormants(apparentFormants, fundamentalFrequency).Select(entry => {
                string vowel = entry.Item1;
                Formant firstF = entry.Item2;
                // Proportion constant for this vowel between low and high values
                float c = ((firstF.freqI + 1) * fundamentalFrequency - VowelData.FormantLows[vowel][0]) /
                           (VowelData.FormantHighs[vowel][0] - VowelData.FormantLows[vowel][0]);
                c = Math.Max(0, Math.Min(1, c)); // Constrain value

                int vowelFormantN = VowelData.FormantLows[vowel].Length;
                var costsMatrix = new float[apparentFormants.Count(), vowelFormantN];
                for (int i = 0; i < apparentFormants.Count(); i++) {
                    float apFormantIndex = apparentFormants[i].freqI;
                    float apFormantValue = apparentFormants[i].value;
                    for (int j = 0; j < vowelFormantN; j++) {
                        float vowelFormantFreq = (1 - c) * VowelData.FormantLows[vowel][j] + c * VowelData.FormantHighs[vowel][j];
                        float vFormantI = vowelFormantFreq / fundamentalFrequency - 1;
                        float misalign = Math.Abs(apFormantIndex - vFormantI);
                        if (misalign <= 0.5) {
                            misalign = 0; // Do not penalize when the mismatch is below the resolution of the fundamental frequency.
                        }
                        if (apFormantValue == 0.0) {
                            costsMatrix[i, j] = float.MaxValue; // Prevent divide by 0 for unfeasable choice
                        } else {
                            // formants should have a certain relative amplitude
                            // We make this relative to a fixed value (thankfully we have z-scored these values before)
                            int decibels = VowelData.FormantAmplitudes[vowel][j] - VowelData.FormantAmplitudes[vowel][0];
                            float amplitude = (float)Math.Pow(10, decibels / 20.0);
                            float desiredValue = firstF.value * amplitude;
                            float valueRatio = Math.Max(desiredValue, apFormantValue) / Math.Min(desiredValue, apFormantValue);
                            // Penalize incorrect frequency, but go easy when amplitude is supposed to be low
                            costsMatrix[i, j] = (deltaFreq * misalign) * amplitude + amplitude * (valueRatio - 1);// + 4f / apFormantValue;
                        }
                    }
                }
                var bestPairs = Munkres.MunkresSolver.MunkresAssignment(costsMatrix);
                return new VowelMatching(vowel, c, bestPairs, bestPairs.Sum(p => costsMatrix[p.Item1, p.Item2]));
            });
            //var bestVowelMatching = vowelMatchings.MinBy(m => m.Item3);
            return vowelMatchings.OrderBy(m => m.score).ToArray();

            // Identify the vowel
            /*var vowelMatchings = VowelData.FormantLows.Select(entry => {
                string vowel = entry.Key;
                // Project the vowel formants onto harmonic indices
                //var vFormantIs = entry.Value.Select(vf => (int)Math.Round(vf / fundamentalFrequency) - 1).ToArray();
                var vFormants = entry.Value;

                var costsMatrix = new float[apparentFormants.Count(), vFormants.Count()];
                for (int i = 0; i < apparentFormants.Count(); i++) {
                    // int peakI = peakIndices[i];
                    float apFormantIndex = apparentFormants[i].freqI;
                    float apFormantValue = apparentFormants[i].value;
                    for (int j = 0; j < vFormants.Length; j++) {
                        float vFormant = vFormants[j] / fundamentalFrequency - 1;
                        float misalign = Math.Abs(apFormantIndex - vFormant);
                        if (misalign <= 0.5) {
                            misalign = 0; // Do not penalize when the mismatch is below the resolution of the fundamental frequency.
                        }
                        if (apFormantValue == 0.0) {
                            costsMatrix[i, j] = float.MaxValue; // Prevent divide by 0 for unfeasable choice
                        } else {
                            // formants should have a certain relative amplitude
                            // We make this relative to a fixed value (thankfully we have z-scored these values before)
                            int decibels = VowelData.FormantAmplitudes[vowel][j];
                            float amplitude = (float)Math.Pow(10, decibels / 20.0);
                            float desiredValue = 10f * amplitude;
                            float valueRatio = Math.Max(desiredValue, apFormantValue) / Math.Min(desiredValue, apFormantValue);
                            //costsMatrix[i, j] = valueRatio;
                            // Penalize incorrect frequency, but go easy when amplitude is supposed to be low
                            costsMatrix[i, j] = (deltaFreq * misalign) * amplitude + amplitude * valueRatio + 4f / apFormantValue;
                            //if (j == 1) {
                            //    costsMatrix[i, j] *= 0.8f;
                            //} else if (j == 2) {
                            //    costsMatrix[i, j] *= 0.3f;
                            //}
                        }
                    }
                }

                var bestPairs = Munkres.MunkresSolver.MunkresAssignment(costsMatrix);
                //var bestPairs2 = HungarianAlgorithm(costsMatrix);
                return Tuple.Create(vowel, bestPairs, bestPairs.Sum(p => costsMatrix[p.Item1, p.Item2]));
            }).ToArray();*/
        }

    }
}
