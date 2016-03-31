using System.Collections.Generic;
using System.Linq;

namespace SpectrumAnalyzer {
    public static class VowelData {
        public struct Vowel {
            public string vowel;
            public string description;
            public float[] formantLows;
            public float[] formantHighs;
            public int[] formantStdDevs;
            public int[] formantAmplitudes;

            public Vowel(string vowel, string description, float[] formantLows, float[] formantHighs, int[] formantStdDevs, int[] formantAmplitudes) {
                this.vowel = vowel;
                this.description = description;
                this.formantLows = formantLows;
                this.formantHighs = formantHighs;
                this.formantStdDevs = formantStdDevs;
                this.formantAmplitudes = formantAmplitudes;
            }
        }

        public static Vowel[] EnglishVowels = {
            new Vowel("i", "as in bead", new float[] { 270, 2290, 3010 }, new float[] { 370, 3200, 3730 }, new int[] { 46, 166, 217 }, new int[] { -4, -24, -28 }),
            new Vowel("I", "as in bid", new float[] { 390, 1990, 2550 }, new float[] { 530, 2730, 3600 }, new int[] { 54, 111, 132 }, new int[] { -3, -23, -27 }),
            new Vowel("ɛ", "as in bed", new float[] { 530, 1840, 2480 }, new float[] { 690, 2610, 3570 }, new int[] { 48, 124, 139 }, new int[] { -2, -17, -24 }),
            new Vowel("æ", "as in bad", new float[] { 660, 1720, 2410 }, new float[] { 1010, 2320, 3320 }, new int[] { 101, 103, 123 }, new int[] { -1, -12, -22 }),
            new Vowel("ə", "as the 'a' in about", new float[] { 490, 1350, 1690 }, new float[] { 560, 1820, 2160 }, new int[] { 80, 140, 220 }, new int[] { -5, -15, -20 }),
            new Vowel("ʌ", "as in bud", new float[] { 640, 1190, 2390 }, new float[] { 850, 1590, 3360 }, new int[] { 60, 140, 200 }, new int[] { -1, -10, -27 }),
            new Vowel("u", "as in food", new float[] { 280, 870, 2240 }, new float[] { 350, 1170, 3260 }, new int[] { 80, 109, 144 }, new int[] { -3, -19, -43 }),
            new Vowel("ʊ", "as in good", new float[] { 440, 1020, 2240 }, new float[] { 560, 1410, 3310 }, new int[] { 46, 76, 213 }, new int[] { -1, -12, -34 }),
            new Vowel("ɔ", "as in born", new float[] { 570, 840, 2410 }, new float[] { 680, 1060, 3320 }, new int[] { 66, 85, 183 }, new int[] { 0, -7, -34 }),
            new Vowel("ɑ", "as in bard", new float[] { 730, 1090, 2440 }, new float[] { 1030, 1370, 3170 }, new int[] { 105, 70, 176 }, new int[] { -1, -5, -28 }),
        };

        public static Vowel[] MandarinVowels = {
new Vowel("a", "as in a/阿", new float[] { 937, 1448, 3206 }, new float[] { 1036, 1571, 3338 }, new int[] { 99, 123, 132 }, new int[] { -1, -10, -23 }),
new Vowel("e", "as in e/饿", new float[] { 444, 1073, 3500 }, new float[] { 482, 1246, 3700 }, new int[] { 38, 173, 200 }, new int[] { 0, -14, -22 }),
new Vowel("ê", "as in mei/美", new float[] { 609, 1792, 2620 }, new float[] { 653, 1946, 2878 }, new int[] { 45, 154, 258 }, new int[] { -1, -8, -17 }),
new Vowel("o", "as in wo/我", new float[] { 546, 801, 3153 }, new float[] { 670, 882, 3259 }, new int[] { 124, 81, 106 }, new int[] { -1, -7, -25 }),
new Vowel("u", "as in wu/五", new float[] { 308, 1016, 3510 }, new float[] { 379, 1297, 3640 }, new int[] { 71, 281, 130 }, new int[] { -1, -37, -39 }),
new Vowel("i", "as in yi/已", new float[] { 358, 2307, 3270 }, new float[] { 386, 2369, 3512 }, new int[] { 30, 62, 242 }, new int[] { -1, -17, -10 }),
new Vowel("ü", "as in yu/雨", new float[] { 319, 2059, 3037 }, new float[] { 372, 2144, 3286 }, new int[] { 54, 85, 249 }, new int[] { -1, -12, -12 }),
new Vowel("r", "as in ri/日", new float[] { 333, 1617, 2723 }, new float[] { 360, 1741, 2920 }, new int[] { 30, 124, 198 }, new int[] { -1, -7, -15 }),
new Vowel("ɨ", "as in zi/子", new float[] { 346, 1489, 2487 }, new float[] { 326, 1510, 2598 }, new int[] { 30, 30, 111 }, new int[] { -1, -15, -23 }),

            /*new Vowel("a", "as in a/阿", new float[] { 896, 2516, 3066 }, new float[] { 988, 2677, 3381 }, new int[] { 32, 50, 136 }, new int[] { -2, -16, -19 }),
            new Vowel("e", "as in e/饿", new float[] { 544, 1611, 3049 }, new float[] { 594, 1703, 3191 }, new int[] { 19, 38, 46 }, new int[] { 0, -17, -22 }),
            new Vowel("ê", "as in mei/美", new float[] { 515, 2571, 3196 }, new float[] { 515, 2571, 3197 }, new int[] { 0, 0, 1 }, new int[] { -1, -9, -9 }),
            new Vowel("r", "as in er/儿", new float[] { 481, 1669, 2193 }, new float[] { 522, 1832, 2248 }, new int[] { 15, 56, 20 }, new int[] { -1, -9, -12 }),
            new Vowel("o", "as in wo/我", new float[] { 644, 2998, 3679 }, new float[] { 726, 3234, 3915 }, new int[] { 28, 95, 89 }, new int[] { -1, -26, -26 }),
            new Vowel("ri", "as in ri/日", new float[] { 332, 2215, 2920 }, new float[] { 382, 2320, 3098 }, new int[] { 20, 35, 61 }, new int[] { -1, -15, -17 }),
            new Vowel("u", "as in wu/五", new float[] { 419, 2758, 3238 }, new float[] { 454, 2938, 3497 }, new int[] { 12, 74, 94 }, new int[] { 0, -46, -45 }),
            new Vowel("i", "as in yi/已", new float[] { 237, 2876, 3544 }, new float[] { 255, 2936, 3569 }, new int[] { 9, 23, 11 }, new int[] { -2, -17, -13 }),
            new Vowel("ü", "as in yu/雨", new float[] { 255, 2556, 3368 }, new float[] { 287, 2674, 3670 }, new int[] { 12, 43, 113 }, new int[] { -2, -11, -15 }),*/
        };

        public static Vowel GetVowel(string language, string vowel) {
            return GetVowels(language).First(v => v.vowel == vowel);
        }

        public static Vowel[] GetVowels(string language) {
            switch (language) {
                case "english":
                    return EnglishVowels;
                case "mandarin":
                    return MandarinVowels;
            }
            throw new System.Exception("Invalid language " + language + " specified");
        }

        public static string[] Vowels1 = {"i",
                                                 "I",
                                                 "ɛ",
                                                 "æ",
                                                 //"ɜ",
                                                 "ə",
                                                 "ʌ",
                                                 "u",
                                                 "ʊ",
                                                 "ɔ",
                                                 //"ɒ",
                                                 "ɑ", };//,
                                            //"l",
                                            //"ɫ"};

        public static Dictionary<string, string> Descriptions1 = new Dictionary<string, string> {
            ["i"] = "as in bead",
            ["I"] = "as in bid",
            ["ɛ"] = "as in bed",
            ["æ"] = "as in bad",
            //["ɜ"] = "the 'ir' in bird",
            ["ə"] = "as the 'a' in about",
            ["ʌ"] = "as in bud",
            ["u"] = "as in food",
            ["ʊ"] = "as in good",
            ["ɔ"] = "as in born",
            //["ɒ"] = "as in body",
            ["ɑ"] = "as in bard",
            //["l"] = "the 'l' in like by itself",
            //["ɫ"] = "the ll in full by itself"
        };

        public static Dictionary<string, float[]> FormantLows1 = new Dictionary<string, float[]> {
            ["i"] = new float[] { 270, 2290, 3010 },
            ["I"] = new float[] { 390, 1990, 2550 },
            ["ɛ"] = new float[] { 530, 1840, 2480 },
            ["æ"] = new float[] { 660, 1720, 2410 },
            //["ɜ"] = new float[] { 474, 1379, 1710 },
            ["ə"] = new float[] { 490, 1350, 1690 },
            ["ʌ"] = new float[] { 640, 1190, 2390 },
            ["u"] = new float[] { 280, 870, 2240 },
            ["ʊ"] = new float[] { 440, 1020, 2240 },
            ["ɔ"] = new float[] { 570, 840, 2410 },
            //["ɒ"] = new float[] { 449, 737, 2635 },
            ["ɑ"] = new float[] { 730, 1090, 2440 },
            //["l"] = new float[] { 300, 1225, 2950 },
            //["ɫ"] = new float[] { 450, 750, 2600 }
        };

        public static Dictionary<string, float[]> FormantHighs1 = new Dictionary<string, float[]> {
            ["i"] = new float[] { 370, 3200, 3730 },
            ["I"] = new float[] { 530, 2730, 3600 },
            ["ɛ"] = new float[] { 690, 2610, 3570 },
            ["æ"] = new float[] { 1010, 2320, 3320 },
            //["ɜ"] = new float[] { 474, 1379, 1710 },
            ["ə"] = new float[] { 560, 1820, 2160 },
            ["ʌ"] = new float[] { 850, 1590, 3360 },
            ["u"] = new float[] { 350, 1170, 3260 },
            ["ʊ"] = new float[] { 560, 1410, 3310 },
            ["ɔ"] = new float[] { 680, 1060, 3320 },
            //["ɒ"] = new float[] { 449, 737, 2635 },
            ["ɑ"] = new float[] { 1030, 1370, 3170 },
            //["l"] = new float[] { 300, 1225, 2950 },
            //["ɫ"] = new float[] { 450, 750, 2600 }
        };

        public static Dictionary<string, int[]> FormantStdDevs1 = new Dictionary<string, int[]> {
            ["i"] = new int[] { 46, 166, 217 },
            ["I"] = new int[] { 54, 111, 132 },
            ["ɛ"] = new int[] { 48, 124, 139 },
            ["æ"] = new int[] { 101, 103, 123 },
            //["ɜ"] = new int[] { 0, 0, 0 },
            ["ə"] = new int[] { 80, 140, 220 },
            ["ʌ"] = new int[] { 60, 140, 200 },
            ["u"] = new int[] { 80, 109, 144 },
            ["ʊ"] = new int[] { 46, 76, 213 },
            ["ɔ"] = new int[] { 66, 85, 183 },
            //["ɒ"] = new int[] { 0, 0, 0 },
            ["ɑ"] = new int[] { 105, 70, 176 },
            //["l"] = new int[] { 0, 0, 0 },
            //["ɫ"] = new int[] { 0, 0, 0 }
        };

        // In decibels
        public static Dictionary<string, int[]> FormantAmplitudes1 = new Dictionary<string, int[]> {
            ["i"] = new int[] { -4, -24, -28 },
            ["I"] = new int[] { -3, -23, -27 },
            ["ɛ"] = new int[] { -2, -17, -24 },
            ["æ"] = new int[] { -1, -12, -22 },
            //["ɜ"] = new int[] { 0, 0, 0 },
            ["ə"] = new int[] { -5, -15, -20 },
            ["ʌ"] = new int[] { -1, -10, -27 },
            ["u"] = new int[] { -3, -19, -43 },
            ["ʊ"] = new int[] { -1, -12, -34 },
            ["ɔ"] = new int[] { 0, -7, -34 },
            //["ɒ"] = new int[] { 0, 0, 0 },
            ["ɑ"] = new int[] { -1, -5, -28 },
            //["l"] = new int[] { 0, 0, 0 },
            //["ɫ"] = new int[] { 0, 0, 0 }
        };
    }
}
