using SpectrumAnalyzer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SpectrumAnalyzer.Analyzer;
using static SpectrumAnalyzer.VowelData;

namespace CompareLanguageRecordings {
    class Program {

        static List<Formant[]> csvToObservations(string filename) {
            string[] lines = File.ReadAllLines(filename);
            List<Formant[]> observations = new List<Formant[]>(lines.Length - 1);
            for (int i = 1; i < lines.Length; i++) {
                Formant[] observation = new Formant[3];
                string[] parts = lines[i].Split(',');
                if (parts.Length != 6) {
                    throw new InvalidDataException("Invalid number of items in csv line: " + lines[i]);
                }
                for (int j = 0; j < 3; j++) {
                    observation[j].freq = float.Parse(parts[j]);
                    observation[j].value = float.Parse(parts[j + 3]);
                }
                observations.Add(observation);
            }
            return observations;
        }

        static float percentInLanguage(List<Formant[]> observations, Vowel[] language) {
            int countInLanguage = 0;
            float score = 0;
            foreach (var observation in observations) {
                score += Math.Max(0, language.Min(v => {
                    float formant1Middle = v.formantLows[0];//(v.formantHighs[0] + v.formantLows[0]) / 2;
                    float formant2Middle = v.formantLows[1];//(v.formantHighs[1] + v.formantLows[1]) / 2;
                    return Math.Max(
                                observation[0].freq - formant1Middle - 0 * v.formantStdDevs[0],
                                formant1Middle - 0 * v.formantStdDevs[0] - observation[0].freq) +
                            Math.Max(
                                observation[1].freq - formant2Middle - 0 * v.formantStdDevs[1],
                                formant2Middle - 0 * v.formantStdDevs[1] - observation[1].freq);
                }));
                if (language.Any(v => {
                    return observation[0].freq < v.formantHighs[0] + v.formantStdDevs[0] &&
                           observation[0].freq > v.formantLows[0] - v.formantStdDevs[0] &&
                           observation[1].freq < v.formantHighs[1] + v.formantStdDevs[1] &&
                           observation[1].freq > v.formantLows[1] - v.formantStdDevs[1];
                })) {
                    countInLanguage++;
                }
            }
            return score; //(float)countInLanguage / observations.Count * 100;
        }

        static void Main(string[] args) {
            // Load data
            var acshiEnglish = csvToObservations("../../../EnglishMandarinComparison/acshiEnglishSlowText.csv");
            var acshiMandarin = csvToObservations("../../../EnglishMandarinComparison/acshiMandarinSlowText.csv");

            float englishInMandarin = percentInLanguage(acshiEnglish, MandarinVowels);
            Console.WriteLine("Acshi English in Mandarin: " + englishInMandarin.ToString("00.0") + "%");

            float mandarinInMandarin = percentInLanguage(acshiMandarin, MandarinVowels);
            Console.WriteLine("Acshi Mandarin in Mandarin: " + mandarinInMandarin.ToString("00.0") + "%");

            float englishInEnglish = percentInLanguage(acshiEnglish, EnglishVowels);
            Console.WriteLine("Acshi English in English: " + englishInEnglish.ToString("00.0") + "%");

            float mandarinInEnglish = percentInLanguage(acshiMandarin, EnglishVowels);
            Console.WriteLine("Acshi Mandarin in English: " + mandarinInEnglish.ToString("00.0") + "%");

            Console.ReadKey();
        }
    }
}
