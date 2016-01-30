using System;
using System.Collections.Generic;
using System.Linq;


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

        public void ClearVowel(string vowel) {
            vowelSpectrums.Remove(vowel);
        }

        public float[] GetSpectrum(string vowel) {
            return vowelSpectrums.ContainsKey(vowel) ? vowelSpectrums[vowel].Item2 : null;
        }

        public int GetSpectrumCount(string vowel) {
            return vowelSpectrums.ContainsKey(vowel) ? vowelSpectrums[vowel].Item1 : 0;
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
        /*public SpectrumAnalyzer.Peak[] GetTopPeaks(string vowel, int maxPeaks) {
            var values = vowelSpectrums[vowel].Item2;
            return SpectrumAnalyzer.GetTopPeaks(values, maxPeaks);
        }*/
    }
}
