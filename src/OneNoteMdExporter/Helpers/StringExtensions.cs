using System;
using System.IO;

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

        /// <summary>
        /// Remove space character that is not supported for md references
        /// </summary>
        /// <param name="reference"></param>
        /// <returns></returns>
        public static string RemoveMdReferenceInvalidChars(this string reference)
            => reference?.Replace(" ", "_");

        public static string Left(this String input, int length)
         => (input.Length < length) ? input : input.Substring(0, length);
        
    }
}
