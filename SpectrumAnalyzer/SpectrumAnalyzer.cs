using MoreLinq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpectrumAnalyzer {
    public static class Analyzer {
        public const int SAMPLE_RATE = 44100;
        public const int MAX_FREQ = 4000;

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
            public float freq;
            public float value;

            public Formant(float freq, float value) {
                this.freq = freq;
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

        private static float ScoreOfPotentialFundamental(Peak[] orderedPeaks, float fund, out float refinedFund, out Peak[] matchedPeaks) {
            float freqPerIndex = (float)SAMPLE_RATE / FftProcessor.FFT_LENGTH;

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
                float score = matchingPeaks.Count() * matchingPeaks.Count() * matchingPeaks.Aggregate(1f, (v, p) => v * Math.Max(0.85f, p.maxValue)) * fund;
                float freq = matchingPeaks.Select(p => p.maxIndex / (float)Math.Round(p.maxIndex / fund)).Average() * freqPerIndex;//fund * freqPerIndex;//matchingPeaks.Select(p => (p.highIndex + p.lowIndex) / (float)Math.Round((p.highIndex + p.lowIndex) / (fund * 2f))).Average() / 2f * freqPerIndex;
                
                refinedFund = freq;
                matchedPeaks = matchingPeaks.ToArray();
                return score;
            }
            refinedFund = fund;
            matchedPeaks = null;
            return 0;
        }

        // Attempts to find the fundamental frequency captured by the fft values.
        public static float IdentifyFundamental(Peak[] topPeaks, out Peak[] fundamentalsPeaks) {
            float freqPerIndex = (float)SAMPLE_RATE / FftProcessor.FFT_LENGTH;

            float bestFundamentalFreq = -1;
            fundamentalsPeaks = null;
            
            var orderedPeaks = topPeaks.OrderBy(peak => peak.lowIndex).ToArray();
            // Consider all the deltas between peaks, both by amplitude, and then sequence
            var potentialFundamentals = topPeaks.ZipLongest(orderedPeaks.Skip(1), (a, b) => {
                //int lowBound = b.lowIndex - a.highIndex; // low bound for index of frequency
                //int highBound = b.highIndex - a.lowIndex; // range of frequency above the low bound
                return (float)Math.Abs(b.maxIndex - a.maxIndex);//(lowBound + highBound) / 2f;
            });
            potentialFundamentals = potentialFundamentals.Concat(orderedPeaks.ZipLongest(orderedPeaks.Skip(1), (a, b) => {
                //int lowBound = b.lowIndex - a.highIndex; // low bound for index of frequency
                //int highBound = b.highIndex - a.lowIndex; // range of frequency above the low bound
                return (float)b.maxIndex - a.maxIndex;//(lowBound + highBound) / 2f;
            }));
            // as well as the peaks themselves
            potentialFundamentals = potentialFundamentals.Concat(orderedPeaks.Select(p => (float)p.maxIndex));//(p.lowIndex + p.highIndex) / 2f));

            // Remove very low frequencies. We are talking about people here!
            potentialFundamentals = potentialFundamentals.Where(a => a * freqPerIndex >= 50f);

            // One pass will yield more accurate frequencies for those that had any harmonic matches
            var refinedFundamentals = new List<float>();
            foreach (var fund in potentialFundamentals) {
                float refinedFund;
                Peak[] matchedPeaks;
                ScoreOfPotentialFundamental(orderedPeaks, fund, out refinedFund, out matchedPeaks);
                refinedFundamentals.Add(refinedFund);
            }

            // A second pass will then find more harmonics with the improved frequencies
            float bestScore = 0;
            foreach (var fund in refinedFundamentals) {
                float refinedFund;
                Peak[] matchedPeaks;
                float score = ScoreOfPotentialFundamental(orderedPeaks, fund, out refinedFund, out matchedPeaks);
                if (score > bestScore) {
                    bestScore = score;
                    bestFundamentalFreq = refinedFund;
                    fundamentalsPeaks = matchedPeaks;
                }
            }

            return bestFundamentalFreq;
        }

        // Uses two arrays to remove outlier fundamentals and smooth changes.
        public static float MakeSmoothedFundamental(float newFundamental, float[] lastObservedFundamentals, float[] lastChosenFundamentals) {
            if (newFundamental != -1) {
                float priorFundAvg = lastObservedFundamentals.Average();
                if (Math.Max(priorFundAvg, newFundamental) / Math.Min(priorFundAvg, newFundamental) < 1.3f) {
                    // Shift in the new chosen value
                    Array.Copy(lastChosenFundamentals, 1, lastChosenFundamentals, 0, lastChosenFundamentals.Length - 1);
                    lastChosenFundamentals[lastChosenFundamentals.Length - 1] = newFundamental;
                }

                // shift in the new observed value
                Array.Copy(lastObservedFundamentals, 1, lastObservedFundamentals, 0, lastObservedFundamentals.Length - 1);
                lastObservedFundamentals[lastObservedFundamentals.Length - 1] = newFundamental;
            }

            return lastChosenFundamentals.Where(f => f != 0).DefaultIfEmpty(-1).Average();
        }

        // Finds the amplitude of each harmonic based on the given fundamental
        public static float[] EvaluateHarmonicSeries(float[] spectrum, float fundamentalFreq) {
            float freqPerIndex = (float)SAMPLE_RATE / FftProcessor.FFT_LENGTH;
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
        public static Formant[] IdentifyApparentFormants(float fundamentalFrequency, float[] harmonicValues, float sameFormantDifference) {
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

            var apparentFormants = new List<Formant>();
            // Make formants out of these peak indices
            // Similar valued neighbors of a peak are combined with it into a single formant
            for (int i = 0; i < peakIndices.Count; i++) {
                int peakIndex = peakIndices[i];
                float weightedPeakFreq = (peakIndex + 1) * fundamentalFrequency * harmonicValues[peakIndex];
                float sumFormantValue =  harmonicValues[peakIndex];
                float maxFormantValue = harmonicValues[peakIndex];

                // Check prior values and add them if close enough in value
                for (int j = peakIndex - 1; j >= 0; j--) {
                    if (Math.Abs(maxFormantValue - harmonicValues[j]) / maxFormantValue < sameFormantDifference) {
                        weightedPeakFreq += (j + 1) * fundamentalFrequency * harmonicValues[j];
                        sumFormantValue += harmonicValues[j];
                    } else {
                        break; // done with prior values, go no further
                    }
                }

                // Check later values similarly
                for (int j = peakIndex + 1; j < harmonicValues.Length; j++) {
                    if (Math.Abs(maxFormantValue - harmonicValues[j]) / maxFormantValue < sameFormantDifference) {
                        weightedPeakFreq += (j + 1) * fundamentalFrequency * harmonicValues[j];
                        sumFormantValue += harmonicValues[j];
                    } else {
                        break;
                    }
                }

                weightedPeakFreq /= sumFormantValue;
                if (!apparentFormants.Any(f => f.freq == weightedPeakFreq)) {
                    apparentFormants.Add(new Formant(weightedPeakFreq, sumFormantValue));
                }
            }
            return apparentFormants.ToArray();
        }

        // From all the apparent formants, decides on which actually seem to be F1, F2, and F3.
        // The return array will have 3 entries, but if no formant was found, the formant will have 0 values.
        public static Formant[] IdentifyFormants123(Formant[] apparentFormants) {
            // Simply choose the highest value formants in each of three ranges, exluding already chosen values
            //F1 between 0 and 1400Hz
            //F2 between 500 and 4000Hz
            //F3 between 1500 and 4000Hz
            //But all apparent formants will have frequency < 4000Hz.
            var formants123 = new Formant[3];
            formants123[0] = apparentFormants.Where(f => f.freq < 1400).DefaultIfEmpty().MaxBy(f => f.value);
            formants123[1] = apparentFormants.Where(f => f.freq > 500 && f.freq != formants123[0].freq).DefaultIfEmpty().MaxBy(f => f.value);
            formants123[2] = apparentFormants.Where(f => f.freq > 1500 && f.freq != formants123[1].freq).DefaultIfEmpty().MaxBy(f => f.value);
            // Then just make sure that F1 < F2 < F3 and swap if necessary
            if (formants123[2].freq < formants123[1].freq) {
                Formant f = formants123[2];
                formants123[2] = formants123[1];
                formants123[1] = f;
            }
            if (formants123[1].freq < formants123[0].freq) {
                Formant f = formants123[1];
                formants123[1] = formants123[0];
                formants123[0] = f;
            }
            return formants123;
        }

        // Uses two arrays to remove outlier formant values and smooth changes.
        public static Formant[] MakeSmoothedFormants123(Formant[] newFormants123, Formant[][] lastObservedFormants, Formant[][] lastAcceptedFormants) {
            var formants = new Formant[3];
            for (int i = 0; i < 3; i++) {
                if (newFormants123[i].value != 0 && lastObservedFormants.All(f => f != null)) {
                    float priorFormantFAvg = 0;
                    int count = 0;
                    foreach (var f in lastObservedFormants) {
                        if (f.Length > i) {
                            priorFormantFAvg += f[i].freq;
                            count++;
                        }
                    }
                    priorFormantFAvg /= count;

                    if (Math.Max(priorFormantFAvg, newFormants123[i].freq) / Math.Min(priorFormantFAvg, newFormants123[i].freq) < 1.3f) {
                        // Shift in the accepted values
                        Array.Copy(lastAcceptedFormants[i], 1, lastAcceptedFormants[i], 0, lastAcceptedFormants[i].Length - 1);
                        lastAcceptedFormants[i][lastAcceptedFormants[i].Length - 1] = newFormants123[i];
                    }
                }

                formants[i].freq = lastAcceptedFormants[i].Average(f => f.freq);
                formants[i].value = lastAcceptedFormants[i].Average(f => f.value);
            }

            // shift in the new apparent values
            Array.Copy(lastObservedFormants, 1, lastObservedFormants, 0, lastObservedFormants.Length - 1);
            lastObservedFormants[lastObservedFormants.Length - 1] = newFormants123;

            return formants;
        }

        // Finds the peaks of the harmonic values, using the weighted average of neighboring peaks, and also combining
        // peaks when their neighbors differ by only the given percentage change difference. Peak power 10 followed by 12 => .167 difference.
        // Returns list of index, magnitude pairs.
        public static List<Formant> IdentifyApparentFormants2(float[] harmonicValues, float sameFormantDifference) {
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

        public static VowelMatching MatchVowel(Formant[] apparentFormants, float fundamentalFrequency, string vowel) {
            VowelMatching bestMatching = new VowelMatching();
            bestMatching.score = float.MaxValue;
            for (int i = 0; i <= 10; i++) {
                // Proportion constant for this vowel between low and high values
                float c = i / 10f;

                int vowelFormantN = VowelData.FormantLows[vowel].Length;
                var costsMatrix = new float[apparentFormants.Length, vowelFormantN];
                for (int j = 0; j < apparentFormants.Length; j++) {
                    float apFormantFreq = apparentFormants[j].freq;
                    float apFormantValue = apparentFormants[j].value;
                    float apFormantI = apFormantFreq / fundamentalFrequency - 1;
                    for (int k = 0; k < vowelFormantN; k++) {
                        float vowelFormantFreq = (1 - c) * VowelData.FormantLows[vowel][k] + c * VowelData.FormantHighs[vowel][k];
                        float vFormantI = vowelFormantFreq / fundamentalFrequency - 1;
                        float misalign = Math.Abs(apFormantI - vFormantI);
                        // Do not penalize when the mismatch is below the resolution of the fundamental frequency.
                        // And when the apparent formant is higher than the vowel formant
                        if (misalign <= 0.5 && apFormantI >= vFormantI) {
                            misalign = 0;
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
                            costsMatrix[j, k] = misalign + (float)Math.Sqrt(valueRatio - 1);
                        }
                    }
                }
                var bestPairs = Munkres.MunkresSolver.MunkresAssignment(costsMatrix);
                var newMatching = new VowelMatching(vowel, c, bestPairs, bestPairs.Sum(p => costsMatrix[p.Item1, p.Item2]));
                if (newMatching.score <= bestMatching.score) {
                    bestMatching = newMatching;
                }
            }
            return bestMatching;
        }

        public static VowelMatching[] IdentifyVowel(Formant[] apparentFormants, float fundamentalFrequency) {
            var vowelMatchings = VowelData.Vowels.Select(vowel => {
                return MatchVowel(apparentFormants, fundamentalFrequency, vowel);
            });
            return vowelMatchings.Where(m => m.vowel != null).OrderBy(m => m.score).ToArray();
        }

    }
}
