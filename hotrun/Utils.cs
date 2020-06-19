using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace hotrun {
    static class Utils {
        static Tuple<int, int> GetToken(string s, int start = 0) {
            var tokStart = -1;
            var quoted = false;

            for (int i = start; i < s.Length; ++i) {
                var c = s[i];

                if (c == ' ') {
                    if (tokStart == -1 || quoted) continue;
                    else return Tuple.Create(tokStart, i);
                }

                if (c == '"') {
                    if (!(i > 0 && s[i - 1] == '\\')) {
                        quoted = !quoted;
                    }
                }

                if (tokStart == -1) tokStart = i;
            }

            return Tuple.Create(
                tokStart,
                tokStart == -1 ? -1 : s.Length
            );
        }

        public static string GetFileName(string s) {
            var range = GetToken(s);

            return range.Item1 == -1 ?
                "" :
                s.Substring(range.Item1, range.Item2 - range.Item1);
        }

        public static string GetArguments(string s) {
            var args = new List<string>();
            var range = GetToken(s);
            var start = range.Item2 == -1 ? s.Length : range.Item2;

            while (true) {
                range = GetToken(s, start);
                if (range.Item1 == -1) break;
                start = range.Item2;

                args.Add(s.Substring(range.Item1, range.Item2 - range.Item1));
            }

            return string.Join(" ", args);
        }
    }
}
