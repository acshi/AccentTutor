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

        static float scoreOfLanguage(List<Formant[]> observations, Vowel[] language) {
            float score = 0;
            foreach (var observation in observations) {
                score += Math.Max(0, language.Min(v => {
                    float formant1Middle = (v.formantHighs[0] + v.formantLows[0]) / 2;
                    float formant2Middle = (v.formantHighs[1] + v.formantLows[1]) / 2;
                    float formant3Middle = (v.formantHighs[2] + v.formantLows[2]) / 2;
                    return Math.Abs(observation[0].freq - formant1Middle) +
                           Math.Abs(observation[1].freq - formant2Middle) +
                           Math.Abs(observation[2].freq - formant3Middle);
                }));
            }
            return score / observations.Count;
        }

        static void Main(string[] args) {
            // Load data
            var slowEnglish = csvToObservations("../../../EnglishMandarinComparison/3EnglishSlow.csv");
            var slowMandarin = csvToObservations("../../../EnglishMandarinComparison/3MandarinSlow.csv");
            var fastEnglish = csvToObservations("../../../EnglishMandarinComparison/3EnglishFast.csv");
            var fastMandarin = csvToObservations("../../../EnglishMandarinComparison/3MandarinFast.csv");

            float fastEnglishScore = scoreOfLanguage(fastEnglish, MandarinVowels);
            Console.WriteLine("Fast English Score: " + fastEnglishScore.ToString("00.0"));

            float fastMandarinScore = scoreOfLanguage(fastMandarin, MandarinVowels);
            Console.WriteLine("Fast Mandarin Score: " + fastMandarinScore.ToString("00.0"));

            float slowEnglishScore = scoreOfLanguage(slowEnglish, MandarinVowels);
            Console.WriteLine("Slow English Score: " + slowEnglishScore.ToString("00.0"));            

            float slowMandarinScore = scoreOfLanguage(slowMandarin, MandarinVowels);
            Console.WriteLine("Slow Mandarin Score: " + slowMandarinScore.ToString("00.0"));

            Console.ReadKey();
        }
    }
}
