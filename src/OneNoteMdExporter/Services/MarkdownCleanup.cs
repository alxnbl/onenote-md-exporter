using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace alxnbl.OneNoteMdExporter.Services
{
    /// <summary>
    /// Optional clean-up pass on the generated Markdown, applied after attachment references are inserted:
    /// unescape the "\&lt;" / "\&gt;" PanDoc emits, unwrap default-style and empty &lt;span&gt; noise, and renumber
    /// ordered lists PanDoc writes as "1. 1. 1.". Pure string functions, no OneNote dependency, so they are easy
    /// to unit-test. Fenced code blocks are left untouched.
    /// </summary>
    public static class MarkdownCleanup
    {
        public static string CleanupMarkdown(string md)
        {
            var newLine = md.Contains("\r\n") ? "\r\n" : "\n";
            var lines = md.Replace("\r\n", "\n").Split('\n');

            // A fence closes only with the same character, at least as long as the one that opened it.
            // inFence marks every line that is part of a fenced code block (the fence lines included), so the
            // clean-up and the list renumbering both leave code blocks untouched.
            var inFence = new bool[lines.Length];
            string openFence = null;
            for (int i = 0; i < lines.Length; i++)
            {
                var fm = Regex.Match(lines[i], @"^\s{0,3}(`{3,}|~{3,})");
                if (fm.Success)
                {
                    var f = fm.Groups[1].Value;
                    if (openFence == null) { openFence = f; inFence[i] = true; continue; }
                    if (f[0] == openFence[0] && f.Length >= openFence.Length && lines[i].Trim().Trim(f[0]).Length == 0) { openFence = null; inFence[i] = true; continue; }
                }
                if (openFence == null)
                    lines[i] = StripNoiseSpans(UnescapeAngleBrackets(lines[i]));
                else
                    inFence[i] = true;
            }

            lines = RenumberOrderedLists(lines, inFence);

            return string.Join(newLine, lines);
        }

        private static readonly Regex EmptySpan = new(@"<span style='[^']*'>\s*</span>");
        private static readonly Regex DefaultStyleSpan = new(
            @"<span style='(?:\s*(?:color:\s*(?:black|windowtext|automatic|#000000)|background:\s*white)\s*;?\s*)+'>(?<inner>(?:(?!</?span\b).)*)</span>",
            RegexOptions.IgnoreCase);

        /// <summary>
        /// Text pasted from Outlook / Word arrives wrapped in spans that only say "color:black" or
        /// "background:white" (the defaults), plus empty spans. They are unwrapped; coloured text, highlights,
        /// bold and fonts stay as they are.
        /// </summary>
        public static string StripNoiseSpans(string line)
        {
            if (!line.Contains("<span", System.StringComparison.OrdinalIgnoreCase))
                return line;
            string before;
            do
            {
                before = line;
                line = EmptySpan.Replace(line, "");
                line = DefaultStyleSpan.Replace(line, m => m.Groups["inner"].Value);
            } while (line != before);
            return line;
        }

        public static string UnescapeAngleBrackets(string line)
        {
            // "\<" followed by something that looks like a tag start would be swallowed by an HTML-aware renderer => use the entity
            line = Regex.Replace(line, @"\\<(?=[A-Za-z/!?])", "&lt;");
            line = line.Replace(@"\<", "<");
            // Keep "\>" at the start of a line (after indentation or list markers): unescaped it turns the line
            // into a quote block ("> text"), which changes the page
            var lead = Regex.Match(line, @"^(\s*([-*+]|\d+[.)])?\s*)\\>");
            if (lead.Success)
                return line[..(lead.Length)] + line[lead.Length..].Replace(@"\>", ">");
            line = line.Replace(@"\>", ">");
            return line;
        }

        private static readonly Regex OrderedItem = new(@"^(?<indent>[ \t]*)(?<num>\d+)(?<sep>[.)])(?<rest>\s.*)?$");

        /// <summary>
        /// PanDoc often writes every item of a numbered list as "1." with blank lines between items.
        /// Consecutive items at the same indentation, separated only by blank lines, that are ALL numbered "1"
        /// are renumbered 1, 2, 3... Lists with any other numbering are left as they are.
        /// </summary>
        public static string[] RenumberOrderedLists(string[] lines) => RenumberOrderedLists(lines, null);

        /// <summary>
        /// As above; <paramref name="inFence"/> (when given) marks lines inside fenced code blocks, which are
        /// never treated as list items and break a list run, so numbering inside code is left untouched.
        /// </summary>
        public static string[] RenumberOrderedLists(string[] lines, bool[] inFence)
        {
            bool Fenced(int k) => inFence != null && k < inFence.Length && inFence[k];
            var result = (string[])lines.Clone();
            int i = 0;
            while (i < result.Length)
            {
                if (Fenced(i)) { i++; continue; }
                var m = OrderedItem.Match(result[i]);
                if (!m.Success) { i++; continue; }

                var indent = m.Groups["indent"].Value;
                var items = new List<int> { i };
                int j = i + 1;
                while (j < result.Length)
                {
                    if (Fenced(j)) break;
                    if (string.IsNullOrWhiteSpace(result[j])) { j++; continue; }
                    var mj = OrderedItem.Match(result[j]);
                    if (mj.Success && mj.Groups["indent"].Value == indent) { items.Add(j); j++; continue; }
                    // A more indented line (sub-list or continuation) belongs to the current item
                    if (result[j].Length > indent.Length && result[j].StartsWith(indent) && char.IsWhiteSpace(result[j][indent.Length])) { j++; continue; }
                    break;
                }

                if (items.Count > 1 && items.All(k => OrderedItem.Match(result[k]).Groups["num"].Value == "1"))
                {
                    int n = 1;
                    foreach (var k in items)
                    {
                        var mk = OrderedItem.Match(result[k]);
                        result[k] = $"{mk.Groups["indent"].Value}{n++}{mk.Groups["sep"].Value}{mk.Groups["rest"].Value}";
                    }
                }

                i = items.Last() + 1;
            }
            return result;
        }
    }
}
