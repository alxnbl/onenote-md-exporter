using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace alxnbl.OneNoteMdExporter.Services.Export
{
    /// <summary>
    /// The pure part of link repair: turn the "onenote-page:&lt;id&gt;" placeholders in one file's text into relative
    /// links, given a map of id =&gt; absolute target path and the folder the file lives in. No file IO and no
    /// OneNote dependency, so it is unit-tested on its own.
    /// </summary>
    public static class LinkRepairCore
    {
        public static readonly Regex Placeholder = new(
            @"\]\(onenote-page:(?<id>[0-9a-fA-F-]+)(?:\s+""[^""]*"")?\)", RegexOptions.Compiled);

        /// <summary>
        /// Replace every placeholder in <paramref name="text"/>. A placeholder whose id is in <paramref name="map"/>
        /// becomes a relative link from <paramref name="mdDir"/> to the target; one that is not gets a note added.
        /// </summary>
        public static string ResolveText(string text, IReadOnlyDictionary<string, string> map, string mdDir, out int resolved, out int unresolved)
        {
            int r = 0, u = 0;
            var result = Placeholder.Replace(text, m =>
            {
                var key = "page:" + m.Groups["id"].Value.ToLowerInvariant();
                if (map.TryGetValue(key, out var target))
                {
                    r++;
                    return "](" + ToMdLinkPath(Path.GetRelativePath(mdDir, target)) + ")";
                }
                u++;
                return m.Value.Contains('"') ? m.Value : m.Value.TrimEnd(')') + " \"OneNote link to a page not in this archive\")";
            });
            resolved = r;
            unresolved = u;
            return result;
        }

        /// <summary>A relative path turned into a Markdown-safe link target (forward slashes, reserved characters percent-encoded).</summary>
        public static string ToMdLinkPath(string relativePath)
            => relativePath.Replace("\\", "/").Replace("%", "%25").Replace(" ", "%20").Replace("(", "%28").Replace(")", "%29")
                .Replace("#", "%23").Replace("?", "%3F").Replace("<", "%3C").Replace(">", "%3E").Replace("[", "%5B").Replace("]", "%5D");
    }
}
