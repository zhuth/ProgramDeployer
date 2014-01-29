using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProgramDeployerClient
{
    public class PropertiesParser
    {
        Dictionary<string, string> dict = new Dictionary<string, string>();

        public PropertiesParser(string text, params char[] delimeter)
        {
            if (delimeter.Length == 0) delimeter = new char[] { '&' };
            if (delimeter[0] == '&' && text.StartsWith("?")) text = text.Substring(1);
            string[] lines = text.Split(delimeter);
            constructor(lines);
        }

        public PropertiesParser(string[] lines)
        {
            constructor(lines);
        }

        private void constructor(string[] lines)
        {
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var p = SafeSplit(line, '=');
                if (!dict.ContainsKey(p.Key))
                    dict.Add(p.Key.ToLower(), p.Value);
            }
        }

        public string this[string key]
        {
            get
            {
                key = key.ToLower();
                if (dict.ContainsKey(key)) return dict[key];
                else return null;
            }
            set
            {
                key = key.ToLower();
                if (dict.ContainsKey(key)) dict[key] = value;
                else dict.Add(key, value);
            }
        }

        public static KeyValuePair<string, string> SafeSplit(string str, char delimeter)
        {
            int di = str.IndexOf(delimeter);
            if (di < 0) return new KeyValuePair<string, string>(str.Trim(), "");
            else return new KeyValuePair<string, string>(str.Substring(0, di).Trim(), str.Substring(di + 1).Trim());
        }

        public string ToString(char delimeter = '\n')
        {
            string result = "";
            foreach (var p in dict.OrderBy(p => p.Key))
            {
                if (p.Key == "sign") continue;
                result += p.Key + "=" + p.Value + delimeter;
            }
            return result == "" ? "" : result.Substring(0, result.Length - 1);
        }
    }
}
