using System.Collections.Generic;

namespace SpectrumAnalyzer {
    public static class VowelData {
        public static string[] Vowels = {"i",
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

        public static Dictionary<string, string> Descriptions = new Dictionary<string, string> {
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

        public static Dictionary<string, float[]> FormantLows = new Dictionary<string, float[]> {
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

        public static Dictionary<string, float[]> FormantHighs = new Dictionary<string, float[]> {
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

        public static Dictionary<string, int[]> FormantStdDevs = new Dictionary<string, int[]> {
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
        public static Dictionary<string, int[]> FormantAmplitudes = new Dictionary<string, int[]> {
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
