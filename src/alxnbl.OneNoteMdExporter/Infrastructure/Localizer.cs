using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace alxnbl.OneNoteMdExporter.Infrastructure
{
    public static class Localizer
    {
        public static string GetString(string code)
        {
            var lang = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            var transFile = Path.Combine("Resources", $"trad.{lang}.json");
            var transFileEn = Path.Combine("Resources", $"trad.en.json");
            string localizedText = null;

            if (File.Exists(transFile))
            {
                var tradsFile = File.ReadAllText(transFile);
                JObject d = JObject.Parse(tradsFile);
                localizedText = d[code]?.ToString();
            }

            if(localizedText == null)
            {
                // Translation not found in current language

                var tradsFile = File.ReadAllText(transFileEn);
                JObject d = JObject.Parse(tradsFile);

                if (d[code] != null)
                    localizedText = d[code].ToString();
                else
                    throw new InvalidOperationException($"Missing translation for code {code}");
            }

            return localizedText;
        }
    }
}
