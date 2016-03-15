using MoreLinq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static SpectrumAnalyzer.VowelData;

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

        // Determines if a sequence of time domain samples constitutes a silent signal
        public static bool IsSilence(float[] values, float[] spectrum) {
            float sumSquares = 0;
            float absSum = 0;
            for (int i = 0; i < values.Length; i++) {
                sumSquares += values[i] * values[i];
                absSum += Math.Abs(values[i]);
            }
            float rms = (float)Math.Sqrt(sumSquares / values.Length);
            float absMean = absSum / values.Length;
            Debug.WriteLine(rms / absMean);
            return rms < 2000;
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

            float fundI = fund / freqPerIndex;

            // How many of the top peaks are a multiple of this?
            var matchingPeaks = orderedPeaks.Where(p => {
                int harmonicNum = (int)Math.Round(p.highIndex / fundI);
                float offset = harmonicNum * fundI;
                float tolerance = Math.Max((float)Math.Log10(fundI) * 4f, p.highIndex / 100f);
                return harmonicNum > 0 && p.lowIndex - offset <= tolerance && p.highIndex - offset >= -tolerance;
            });
            // Remove duplicate peaks with the same harmonic number (group them by harmonic number then take the highest scoring of each group),
            // then order by descending by value
            matchingPeaks = matchingPeaks.GroupBy(p => Math.Round(p.highIndex / fundI)).Select(group => group.OrderByDescending(p => p.maxValue).First()).OrderByDescending(p => p.maxValue);

            if (matchingPeaks.Count() > 0) {
                // This scoring function is the most important part for correctly determining the fundamental
                var matched = new List<Peak>();
                float score = fundI;
                foreach (var p in matchingPeaks) {
                    int count = matched.Count;
                    float scoreMult = Math.Min(1, p.maxValue * fundI / p.maxIndex * 128);
                    if (count == 0) {
                        matched.Add(p);
                        score *= scoreMult;
                    } else {
                        scoreMult *= (count + 1) * (count + 1) / (float)(count * count);
                        if (scoreMult > 1f) {
                            matched.Add(p);
                            score *= scoreMult;
                        }
                    }
                }

                //float score = matched.Count() * matched.Count() * matched.Aggregate(1f, (v, p) => v * p.maxValue / p.maxIndex * fundI * 42);
                float freq = matched.Select(p => p.maxIndex / (float)Math.Round(p.maxIndex / fundI)).Average() * freqPerIndex;//fund * freqPerIndex;//matchingPeaks.Select(p => (p.highIndex + p.lowIndex) / (float)Math.Round((p.highIndex + p.lowIndex) / (fund * 2f))).Average() / 2f * freqPerIndex;
                
                refinedFund = freq;
                matchedPeaks = matched.ToArray();
                return score;
            }
            refinedFund = fund;
            matchedPeaks = null;
            return 0;
        }

        // Attempts to find the fundamental frequency captured by the fft values.
        public static float IdentifyFundamental(float[] spectrum) {
            float freqPerIndex = (float)SAMPLE_RATE / FftProcessor.FFT_LENGTH;

            double minValue = 0.0001;

            // Use the Harmonic Product Spectrum
            double[] hps = new double[spectrum.Length];
            for (int i = 0; i < spectrum.Length; i++) {
                hps[i] = Math.Max(minValue, spectrum[i]) * i;
            }

            for (int h = 2; h <= 26; h++) {
                for (int i = 0; i < spectrum.Length; i++) {
                    if (i * h >= spectrum.Length) {
                        hps[i] *= minValue;
                    } else {
                        // Let it be off just a little to either side
                        double maxInDownSample = 0;
                        int plusMinus = (int)Math.Max(Math.Log10(i) * 1.5, (i * h) / 100.0);
                        for (int j = Math.Max(0, i * h - plusMinus); j <= i * h + plusMinus && j < spectrum.Length; j++) {
                            maxInDownSample = Math.Max(maxInDownSample, spectrum[j]);
                        }
                        
                        hps[i] *= Math.Max(minValue, maxInDownSample * i / h); // put a minimum value to avoid multiplying by 0.
                    }
                }
            }

            float bestFrequency = -1;
            double bestScore = 0;

            // start at a not too low frequency
            for (int i = (int)(70 / freqPerIndex); i < spectrum.Length; i++) {
                if (hps[i] > bestScore) {
                    bestScore = hps[i];
                    //if (i != 0 && hps[i / 2] * 3000f > hps[i]) {
                    //    bestFrequency = i / 2 * freqPerIndex;
                    //} else {
                    bestFrequency = i * freqPerIndex;
                    //}
                }
            }

            //Debug.WriteLine(bestFrequency);
            return bestFrequency;
        }

        // Attempts to find the fundamental frequency captured by the fft values.
        public static float IdentifyFundamental2(Peak[] topPeaks, out Peak[] fundamentalsPeaks) {
            float freqPerIndex = (float)SAMPLE_RATE / FftProcessor.FFT_LENGTH;

            float bestFundamentalFreq = -1;
            fundamentalsPeaks = null;
            
            var orderedPeaks = topPeaks.OrderBy(peak => peak.lowIndex).ToArray();
            // Consider all the deltas between peaks, both by amplitude, and then sequence
            var potentialFundamentals = topPeaks.ZipLongest(orderedPeaks.Skip(1), (a, b) => {
                //int lowBound = b.lowIndex - a.highIndex; // low bound for index of frequency
                //int highBound = b.highIndex - a.lowIndex; // range of frequency above the low bound
                return (float)Math.Abs(b.maxIndex - a.maxIndex) * freqPerIndex;//(lowBound + highBound) / 2f;
            });
            potentialFundamentals = potentialFundamentals.Concat(orderedPeaks.ZipLongest(orderedPeaks.Skip(1), (a, b) => {
                //int lowBound = b.lowIndex - a.highIndex; // low bound for index of frequency
                //int highBound = b.highIndex - a.lowIndex; // range of frequency above the low bound
                return (float)(b.maxIndex - a.maxIndex) * freqPerIndex;//(lowBound + highBound) / 2f;
            }));
            // as well as the peaks themselves
            potentialFundamentals = potentialFundamentals.Concat(orderedPeaks.Select(p => (float)p.maxIndex * freqPerIndex));//(p.lowIndex + p.highIndex) / 2f));

            // Remove very low frequencies. We are talking about people here!
            potentialFundamentals = potentialFundamentals.Where(a => a >= 50f);

            // One pass will yield more accurate frequencies for those that had any harmonic matches
            var refinedFundamentals = new List<float>();
            foreach (var fund in potentialFundamentals) {
                float refinedFund;
                Peak[] matchedPeaks;
                ScoreOfPotentialFundamental(orderedPeaks, fund, out refinedFund, out matchedPeaks);
                if (!refinedFundamentals.Contains(refinedFund)) {
                    refinedFundamentals.Add(refinedFund);
                }
            }

            refinedFundamentals.RemoveAll(f => f <= 50f); // Again, we don't want to allow too low frequencies

            // A second pass will then find more harmonics with the improved frequencies
            float bestScore = 0;
            foreach (var fund in refinedFundamentals) {
                float refinedFund;
                Peak[] matchedPeaks;
                float score = ScoreOfPotentialFundamental(orderedPeaks, fund, out refinedFund, out matchedPeaks);
                if (score > bestScore) {
                    if (refinedFund > 550) {
                        refinedFund = 600;
                    }
                    bestScore = score;
                    bestFundamentalFreq = refinedFund;
                    fundamentalsPeaks = matchedPeaks;
                }
            }

            return bestFundamentalFreq;
        }

        // Adds new fundamental to a list of the last found fundamentals, returns the median to eliminate outliers
        public static float MakeSmoothedFundamental(float newFundamental, float[] lastObservedFundamentals) {
            if (newFundamental != -1) {
                // shift in the new observed value
                Array.Copy(lastObservedFundamentals, 1, lastObservedFundamentals, 0, lastObservedFundamentals.Length - 1);
                lastObservedFundamentals[lastObservedFundamentals.Length - 1] = newFundamental;
            }
            var sortedFs = lastObservedFundamentals.Where(f => f != 0).DefaultIfEmpty(-1).OrderBy(f => f);
            float medianF = sortedFs.ElementAt(sortedFs.Count() / 2);
            if (sortedFs.Count() > 1 && sortedFs.Count() % 2 == 1) {
                medianF = (medianF + sortedFs.ElementAt(sortedFs.Count() / 2 - 1)) / 2f;
            }
            if (medianF == 0) {
                return -1;
            }
            return medianF;//lastChosenFundamentals.Where(f => f != 0).DefaultIfEmpty(-1).Average();
        }

        // Uses two arrays to remove outlier fundamentals and smooth changes.
        public static float MakeSmoothedFundamental2(float newFundamental, float[] lastObservedFundamentals, float[] lastChosenFundamentals) {
            if (newFundamental != -1) {
                float priorFundAvg = lastObservedFundamentals.Where(f => f != 0).DefaultIfEmpty(newFundamental).Average();
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
                /*for (int j = peakIndex - 1; j >= 0; j--) {
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
                }*/

                // Check a prior and a later value and add them if close enough in value
                int priorI = peakIndex - 1;
                if (priorI >= 0 && Math.Abs(maxFormantValue - harmonicValues[priorI]) / maxFormantValue < sameFormantDifference) {
                    weightedPeakFreq += (priorI + 1) * fundamentalFrequency * harmonicValues[priorI];
                    sumFormantValue += harmonicValues[priorI];
                }

                int laterI = peakIndex + 1;
                if (laterI < harmonicValues.Length && Math.Abs(maxFormantValue - harmonicValues[laterI]) / maxFormantValue < sameFormantDifference) {
                    weightedPeakFreq += (laterI + 1) * fundamentalFrequency * harmonicValues[laterI];
                    sumFormantValue += harmonicValues[laterI];
                }

                weightedPeakFreq /= sumFormantValue;
                if (!apparentFormants.Any(f => f.freq == weightedPeakFreq)) {
                    apparentFormants.Add(new Formant(weightedPeakFreq, sumFormantValue));
                }
            }
            return apparentFormants.ToArray();
        }

        // From all the apparent formants, decides on which actually seem to be F1, F2, and F3.
        // The decision will be based on finding a better match for one of the given vowels
        // The return array will have 3 entries, but if a formant is not found, that formant will have 0 values.
        public static Formant[] IdentifyFormants123(Formant[] apparentFormants, Vowel[] vowels) {
            // Simply choose the highest value formants in each of three ranges, exluding already chosen values
            //F1 between 0 and 1400Hz
            //F2 between 500 and 4000Hz
            //F3 between 1500 and 4000Hz
            //But all apparent formants will have frequency < 4000Hz.
            var formants123 = new Formant[3];
            formants123[0] = apparentFormants.Where(f => f.freq < 1400).DefaultIfEmpty().MaxBy(f => f.value);
            formants123[1] = apparentFormants.Where(f => f.freq > 500 && f.freq != formants123[0].freq).DefaultIfEmpty().MaxBy(f => {
                // Give a bonus to a formant that fits the f2 of a vowel that also fits f1
                /*if (vowels.Any(v => v.formantLows[0] < formants123[0].freq && formants123[0].freq < v.formantHighs[0] &&
                                    v.formantLows[1] < f.freq && f.freq < v.formantHighs[1])) {
                    return f.value * 3.0f;
                }*/
                return f.value;
            });
            formants123[2] = apparentFormants.Where(f => f.freq > 1500 && f.freq != formants123[1].freq).DefaultIfEmpty().MaxBy(f => {
                // Give a bonus to a formant that fits the f3 of a vowel that also fits f2
                if (vowels.Any(v => v.formantLows[1] < formants123[1].freq && formants123[1].freq < v.formantHighs[1] &&
                                    v.formantLows[2] < f.freq && f.freq < v.formantHighs[2])) {
                    return f.value * 4f;
                }
                return f.value;
            });


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

        // Adds new fundamental to a list of the last found fundamentals, returns the median to eliminate outliers
        public static Formant[] MakeSmoothedFormants123(Formant[] newFormants123, Formant[][] lastObservedFormants) {
            var formants = new Formant[3];

            // shift in the new apparent values
            Array.Copy(lastObservedFormants, 1, lastObservedFormants, 0, lastObservedFormants.Length - 1);
            lastObservedFormants[lastObservedFormants.Length - 1] = newFormants123;

            for (int i = 0; i < 3; i++) {
                var sortedFs = lastObservedFormants.Where(fs => fs != null).Select(fs => fs[i]).Where(f => f.freq != 0.0f).DefaultIfEmpty().OrderBy(f => f.freq);
                Formant medianF = sortedFs.ElementAt(sortedFs.Count() / 2);
                formants[i] = medianF;
                if (sortedFs.Count() > 1 && sortedFs.Count() % 2 == 1) {
                    Formant medianF2 = sortedFs.ElementAt(sortedFs.Count() / 2 - 1);
                    formants[i].freq = (formants[i].freq + medianF2.freq) / 2;
                    formants[i].value = (formants[i].value + medianF2.value) / 2;
                }
            }

            return formants;
        }

        // Uses two arrays to remove outlier formant values and smooth changes.
        public static Formant[] MakeSmoothedFormants123_2(Formant[] newFormants123, Formant[][] lastObservedFormants, Formant[][] lastAcceptedFormants) {
            var formants = new Formant[3];
            for (int i = 0; i < 3; i++) {
                if (newFormants123[i].value != 0) {
                    float priorFormantFAvg = lastObservedFormants.Where(fs => fs != null).Select(fs => fs[i].freq)
                                                                 .Where(f => f != 0.0f).DefaultIfEmpty(newFormants123[i].freq).Average();

                    if (Math.Max(priorFormantFAvg, newFormants123[i].freq) / Math.Min(priorFormantFAvg, newFormants123[i].freq) < 1.3f) {
                        // Shift in the accepted values
                        Array.Copy(lastAcceptedFormants[i], 1, lastAcceptedFormants[i], 0, lastAcceptedFormants[i].Length - 1);
                        lastAcceptedFormants[i][lastAcceptedFormants[i].Length - 1] = newFormants123[i];
                    }
                }

                formants[i].freq = lastAcceptedFormants[i].Where(f => f.freq != 0.0f).DefaultIfEmpty().Average(f => f.freq);
                formants[i].value = lastAcceptedFormants[i].Where(f => f.freq != 0.0f).DefaultIfEmpty().Average(f => f.value);
            }

            // shift in the new apparent values
            Array.Copy(lastObservedFormants, 1, lastObservedFormants, 0, lastObservedFormants.Length - 1);
            lastObservedFormants[lastObservedFormants.Length - 1] = newFormants123;

            return formants;
        }
        
        public static VowelMatching MatchVowel(Formant[] apparentFormants, float fundamentalFrequency, Vowel v) {
            VowelMatching bestMatching = new VowelMatching();
            bestMatching.score = float.MaxValue;
            for (int i = 0; i <= 10; i++) {
                // Proportion constant for this vowel between low and high values
                float c = i / 10f;

                int vowelFormantN = v.formantLows.Length;
                var costsMatrix = new float[apparentFormants.Length, vowelFormantN];
                for (int j = 0; j < apparentFormants.Length; j++) {
                    float apFormantFreq = apparentFormants[j].freq;
                    float apFormantValue = apparentFormants[j].value;
                    float apFormantI = apFormantFreq / fundamentalFrequency - 1;
                    for (int k = 0; k < vowelFormantN; k++) {
                        float vowelFormantFreq = (1 - c) * v.formantLows[k] + c * v.formantHighs[k];
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
                            int decibels = v.formantAmplitudes[k] - v.formantAmplitudes[0];
                            float amplitude = (float)Math.Pow(10, decibels / 20.0);
                            float desiredValue = 10f * amplitude;
                            float valueRatio = Math.Max(desiredValue, apFormantValue) / Math.Min(desiredValue, apFormantValue);
                            // Penalize incorrect frequency and amplitude
                            costsMatrix[j, k] = misalign + (float)Math.Sqrt(valueRatio - 1);
                            // f3 not as important
                            if (k == 2) {
                                costsMatrix[j, k] *= 0.33f;
                            }
                        }
                    }
                }
                var bestPairs = Munkres.MunkresSolver.MunkresAssignment(costsMatrix);
                var newMatching = new VowelMatching(v.vowel, c, bestPairs, bestPairs.Sum(p => costsMatrix[p.Item1, p.Item2]));
                if (newMatching.score <= bestMatching.score) {
                    bestMatching = newMatching;
                }
            }
            return bestMatching;
        }

        public static VowelMatching[] IdentifyVowel(Formant[] apparentFormants, float fundamentalFrequency) {
            var vowelMatchings = VowelData.EnglishVowels.Select(v => {
                return MatchVowel(apparentFormants, fundamentalFrequency, v);
            });
            return vowelMatchings.Where(m => m.vowel != null).OrderBy(m => m.score).ToArray();
        }

    }
}
