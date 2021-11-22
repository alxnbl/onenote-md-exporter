using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace alxnbl.OneNoteMdExporter.Helpers
{
    public static class StringExtensions
    {
        public static string RemoveInvalidFileNameChars(this string filename)
            => string.Concat(filename.Split(Path.GetInvalidFileNameChars()));

        public static string AddPrefixLevel(this string label, int level)
        {
            var prefix = "";
            for (int i = 1; i < level; i++) prefix += "---";
            return prefix + (!string.IsNullOrEmpty(prefix) ? " " : "") + label;
        }

        public static string RemoveMdReferenceInvalidChars(this string reference)
            => reference.Replace(" ", "_");
    }
}
