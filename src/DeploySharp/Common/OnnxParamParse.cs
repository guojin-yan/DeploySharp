using DeploySharp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DeploySharp.Common
{
    public static class OnnxParamParse
    {

        // 正则匹配键值对（如 `2: 'storage tank'`）
        private static readonly Regex PairRegex = new Regex(
            @"(\d+)\s*:\s*'([^']*)'",
            RegexOptions.Compiled
        );
        public static Dictionary<int, string> ParseLabelString(string input)
        {
            var pairs = new Dictionary<int, string>();

            foreach (Match match in PairRegex.Matches(input))
            {
                if (match.Groups.Count != 3)
                    continue;

                int key = int.Parse(match.Groups[1].Value);
                string value = match.Groups[2].Value;

                pairs.Add(key, value);
            }

            return pairs;
        }
    }
}
