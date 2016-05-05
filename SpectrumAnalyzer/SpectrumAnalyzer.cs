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

        // Applies memoization a function of a single argument
        public static Func<A, R> Memoize<A, R>(this Func<A, R> f) {
            var map = new Dictionary<A, R>();
            return a =>
            {
                R value;
                if (map.TryGetValue(a, out value))
                    return value;
                value = f(a);
                map.Add(a, value);
                return value;
            };
        }

        // Attempts to find the fundamental frequency captured by the fft values.
        public static float IdentifyFundamental(float[] spectrum) {
            float freqPerIndex = (float)SAMPLE_RATE / FftProcessor.FFT_LENGTH;
            double minValue = 0.0001;
            var memoizedLog10 = Memoize<double, double>(Math.Log10); // Memoize because it is in a tight loop.

            // Use the Harmonic Product Spectrum
            double[] hps = new double[spectrum.Length];
            for (int i = 0; i < spectrum.Length; i++) {
                hps[i] = Math.Max(minValue, spectrum[i]) * (i + 1);
            }
            for (int h = 2; h <= 26; h++) {
                for (int i = 0; i < spectrum.Length; i++) {
                    if (i * h >= spectrum.Length) {
                        hps[i] *= minValue;
                    } else {
                        // Let it be off just a little to either side
                        double maxInDownSample = minValue; // put a minimum value to avoid multiplying by 0.
                        int plusMinus = (int)Math.Max(memoizedLog10(i) * 1.5, (i * h) / 100.0);
                        for (int j = Math.Max(0, i * h - plusMinus); j <= i * h + plusMinus && j < spectrum.Length; j++) {
                            if (spectrum[j] > maxInDownSample) {
                                maxInDownSample = spectrum[j];
                            }
                        }
                        hps[i] *= maxInDownSample / h;
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

        // Returns the median formant with median frequency and median amplitude in the array
        public static Formant GetMedianFormant(Formant[] fs) {
            var freqSortedFs = fs.Where(f => f.freq != 0.0f).DefaultIfEmpty().OrderBy(f => f.freq);
            float medianFreq = freqSortedFs.ElementAt(freqSortedFs.Count() / 2).freq;
            if (freqSortedFs.Count() > 1 && freqSortedFs.Count() % 2 == 1) {
                float medianFreq2 = freqSortedFs.ElementAt(freqSortedFs.Count() / 2 - 1).freq;
                medianFreq = (medianFreq + medianFreq2) / 2;
            }

            var ampSortedFs = fs.Where(f => f.freq != 0.0f).DefaultIfEmpty().OrderBy(f => f.value);
            float medianAmp = ampSortedFs.ElementAt(ampSortedFs.Count() / 2).value;
            if (ampSortedFs.Count() > 1 && ampSortedFs.Count() % 2 == 1) {
                float medianAmp2 = ampSortedFs.ElementAt(ampSortedFs.Count() / 2 - 1).value;
                medianAmp = (medianAmp + medianAmp2) / 2;
            }

            return new Formant(medianFreq, medianAmp);
        }

        public static void PerformFormantAnalysis(float[][] lastFfts, float[] lastFundamentals, out float fundamental, out float[] harmonicSeries, out Formant[] formants) {
            var presentLastFfts = lastFfts.Where(s => s != null);

            // Find median fundamental (f0)
            // Don't calculate fundamentals -- too expensive. Let them be already calculated just one time each.

            //var lastFundamentals = presentLastSpectrums.Select(spectrum => IdentifyFundamental(spectrum)).ToArray();
            var sortedFs = lastFundamentals.Where(f => f != 0).DefaultIfEmpty(-1).OrderBy(f => f);

            float medianF = sortedFs.ElementAt(sortedFs.Count() / 2);
            if (sortedFs.Count() > 1 && sortedFs.Count() % 2 == 1) {
                medianF = (medianF + sortedFs.ElementAt(sortedFs.Count() / 2 - 1)) / 2f;
            }
            if (presentLastFfts.Count() == 0 || medianF == 0) {
                fundamental = -1;
                harmonicSeries = null;
                formants = null;
                return;
            }
            fundamental = medianF;

            // Find apparent formants
            var lastHarmonicSeries = presentLastFfts.Select(spectrum => EvaluateHarmonicSeries(spectrum, medianF));
            var lastApparentFormants = lastHarmonicSeries.Select(harmonics => IdentifyApparentFormants(medianF, harmonics, 0.8f));
            harmonicSeries = lastHarmonicSeries.Last();

            var medianFormants = new Formant[3];
            // The formants...
            Formant[] f1s = lastApparentFormants.Select(apparentFs => apparentFs.Where(f => f.freq >= 250 && f.freq < 1400).DefaultIfEmpty().MaxBy(f => f.value)).ToArray();
            medianFormants[0] = GetMedianFormant(f1s);

            Formant[] f2s = lastApparentFormants.Select(apparentFs =>
                                apparentFs.Where(f => f.freq > 600 && f.freq < 3000 && Math.Abs(f.freq - medianFormants[0].freq) > 100).DefaultIfEmpty().MaxBy(f => f.value)).ToArray();
            medianFormants[1] = GetMedianFormant(f2s);

            Formant[] f3s = lastApparentFormants.Select(apparentFs =>
                                apparentFs.Where(f => f.freq > 1500 && Math.Abs(f.freq - medianFormants[1].freq) > 100).DefaultIfEmpty().MaxBy(f => f.value)).ToArray();
            medianFormants[2] = GetMedianFormant(f3s);

            // Don't let any of these be backwards!
            medianFormants = medianFormants.OrderBy(f => f.freq).ToArray();

            formants = medianFormants;
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
                // Find the maximum value in the range
                float maxValue = 0;
                for (int i = lowIndex; i <= highIndex; i++) {
                    if (spectrum[i] > maxValue) {
                        maxValue = spectrum[i];
                    }
                }
                
                return maxValue;
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
