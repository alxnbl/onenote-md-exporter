// XML-direct Markdown renderer, intended as a fallback path for when the
// OneNote Publish -> Word .docx -> PanDoc route fails for a page.
//
// Design inspired by (but not copied from) segunak/one-note-to-markdown,
// file OneNoteMarkdownExporter/Services/OneNoteXmlToMarkdownConverter.cs:
//   Copyright (c) 2025 Segun Akinyemi -- MIT License.
// Borrowed ideas: the element handling order (Outline > OEChildren > OE, list-run
// detection, cell content joined with <br>) and the "protect literal text" principle.
// The implementation here is independent: no HtmlAgilityPack, no ReverseMarkdown, and
// it writes Markdown directly instead of going through an intermediate HTML document.
// See THIRD-PARTY-NOTICES.md.
//
// Deliberately free of any dependency on the rest of this project (no Page, no
// AppSettings, no Serilog) so it can be compiled and unit-tested on its own.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace alxnbl.OneNoteMdExporter.Services
{
    /// <summary>
    /// Everything the renderer needs from its caller. All members are optional: with an empty
    /// options object the renderer still produces text, headings, lists, tables and links.
    /// </summary>
    public sealed class XmlRenderOptions
    {
        /// <summary>callbackID of a one:Image / one:Data element =&gt; its bytes, or null when not available</summary>
        public Func<string, byte[]> GetBinary { get; set; }

        /// <summary>Absolute folder the image files are written to (created on demand)</summary>
        public string ResourceFolder { get; set; }

        /// <summary>Folder holding the .md file: image links are made relative to it</summary>
        public string MdDir { get; set; }

        /// <summary>
        /// Prefix of the image file names (page.StableKey), so the same page always writes
        /// the same file names and a re-export overwrites instead of leaving orphans
        /// </summary>
        public string ImageKeyPrefix { get; set; } = "page";

        /// <summary>
        /// one:InsertedFile / one:MediaFile element =&gt; the markdown link target the normal
        /// attachment pipeline uses for it (relative path), or null when the caller has none
        /// </summary>
        public Func<XElement, string> AttachmentRef { get; set; }

        /// <summary>Emit the page title as a level 1 heading (off when the front matter already carries it)</summary>
        public bool IncludeTitle { get; set; } = false;

        /// <summary>
        /// Called with the path of an image file just written. Returns the path of a displayable copy to
        /// show instead (an EMF / WMF metafile converted to PNG), or null to reference the file as it is.
        /// The original is kept and linked next to the image. The caller owns the conversion, so the
        /// renderer stays free of System.Drawing and of the rest of the exporter.
        /// </summary>
        public Func<string, string> DisplayableCopy { get; set; }
    }

    /// <summary>
    /// Renders a OneNote 2013 page XML (http://schemas.microsoft.com/office/onenote/2013/onenote)
    /// straight to Markdown, without Word and without PanDoc.
    /// </summary>
    public static class XmlMarkdownRenderer
    {
        /// <summary>
        /// OneNote TagDef "symbol" values that mean a check box (To Do and its variants).
        /// Same symbols as Models.TagsDefMap maps to the check box glyphs.
        /// </summary>
        private static readonly HashSet<string> ToDoSymbols = ["3", "8", "12", "28", "71", "94", "95"];

        public static string Render(XElement page, XNamespace ns, XmlRenderOptions options, List<string> warnings)
        {
            ArgumentNullException.ThrowIfNull(page);
            options ??= new XmlRenderOptions();
            warnings ??= [];
            ns ??= page.Name.Namespace;

            var ctx = new Ctx(ns, options, warnings);
            var body = new Emitter();

            if (options.IncludeTitle)
            {
                var title = PlainText(page.Element(ns + "Title")?.Element(ns + "OE"), ctx);
                if (!string.IsNullOrWhiteSpace(title))
                    body.Block("# " + title.Trim());
            }

            foreach (var child in page.Elements())
            {
                var name = child.Name.LocalName;
                if (name == "Outline")
                {
                    foreach (var oeChildren in child.Elements(ns + "OEChildren"))
                        RenderOEChildren(oeChildren, ctx, body, 0);
                }
                else if (name == "Image")
                {
                    var img = RenderImage(child, ctx);
                    if (img != null) body.Block(img);
                }
                else if (name == "InsertedFile" || name == "MediaFile")
                {
                    var link = RenderAttachment(child, ctx);
                    if (link != null) body.Block(link);
                }
                // one:Ink* is left out on purpose: the .ink.svg and the PDF already carry the ink,
                // and the recognized handwriting text is added by the caller.
            }

            return body.ToString();
        }

        /*
         * Block level
         */

        private sealed class Ctx(XNamespace ns, XmlRenderOptions options, List<string> warnings)
        {
            public readonly XNamespace Ns = ns;
            public readonly XmlRenderOptions Options = options;
            public readonly List<string> Warnings = warnings;
            public int ImageCounter;

            private Dictionary<string, string> quickStyles;
            private Dictionary<string, (string Name, bool IsToDo)> tagDefs;

            /// <summary>quickStyleIndex =&gt; QuickStyleDef name (PageTitle, h1..h6, p, cite, code, ...)</summary>
            public Dictionary<string, string> QuickStyles(XElement root)
            {
                if (quickStyles != null) return quickStyles;
                quickStyles = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var def in root.Descendants(Ns + "QuickStyleDef"))
                {
                    var index = def.Attribute("index")?.Value;
                    var name = def.Attribute("name")?.Value;
                    if (!string.IsNullOrEmpty(index) && !string.IsNullOrEmpty(name))
                        quickStyles[index] = name;
                }
                return quickStyles;
            }

            /// <summary>Tag index =&gt; the tag's real name and whether it is a check box tag</summary>
            public Dictionary<string, (string Name, bool IsToDo)> TagDefs(XElement root)
            {
                if (tagDefs != null) return tagDefs;
                tagDefs = new Dictionary<string, (string, bool)>(StringComparer.Ordinal);
                foreach (var def in root.Descendants(Ns + "TagDef"))
                {
                    var index = def.Attribute("index")?.Value;
                    if (string.IsNullOrEmpty(index)) continue;
                    var name = def.Attribute("name")?.Value ?? "tag";
                    var symbol = def.Attribute("symbol")?.Value ?? "";
                    tagDefs[index] = (name, ToDoSymbols.Contains(symbol));
                }
                return tagDefs;
            }
        }

        /// <summary>
        /// Collects markdown blocks, keeping exactly one blank line between them
        /// </summary>
        private sealed class Emitter
        {
            private readonly List<string> blocks = [];

            public void Block(string text)
            {
                if (text == null) return;
                var trimmed = text.TrimEnd();
                if (trimmed.Length == 0) return;
                blocks.Add(trimmed);
            }

            /// <summary>A list item (or its continuation): glued to the previous item without a blank line</summary>
            public void ListLine(string text)
            {
                if (string.IsNullOrEmpty(text)) return;
                if (blocks.Count > 0 && blocks[^1].StartsWith(ListRunMarker, StringComparison.Ordinal))
                    blocks[^1] += "\n" + text;
                else
                    blocks.Add(ListRunMarker + text);
            }

            private const string ListRunMarker = "\u0001";

            public override string ToString()
            {
                var text = string.Join("\n\n", blocks.Select(b => b.StartsWith(ListRunMarker, StringComparison.Ordinal) ? b[1..] : b));
                text = text.Replace("\r\n", "\n").Replace('\r', '\n');
                text = Regex.Replace(text, @"\n{3,}", "\n\n");
                return text.Trim() + "\n";
            }
        }

        private static void RenderOEChildren(XElement oeChildren, Ctx ctx, Emitter body, int listDepth)
        {
            var ns = ctx.Ns;
            var oes = oeChildren.Elements(ns + "OE").ToList();

            // Numbering runs: consecutive one:Number siblings are numbered 1., 2., 3., ...
            var number = 0;
            var previousWasNumber = false;

            foreach (var oe in oes)
            {
                var list = oe.Element(ns + "List");
                var isBullet = list?.Element(ns + "Bullet") != null;
                var isNumber = list?.Element(ns + "Number") != null;

                if (isNumber)
                {
                    number = previousWasNumber ? number + 1 : 1;
                    previousWasNumber = true;
                }
                else if (HasContent(oe, ns))
                {
                    previousWasNumber = false;
                }

                RenderOE(oe, ctx, body, listDepth, isBullet, isNumber, number);
            }
        }

        private static void RenderOE(XElement oe, Ctx ctx, Emitter body, int listDepth, bool isBullet, bool isNumber, int number)
        {
            var ns = ctx.Ns;
            var root = oe.Document?.Root ?? Root(oe);
            var isListItem = isBullet || isNumber;

            // Style
            var styleName = "";
            var quickStyleIndex = oe.Attribute("quickStyleIndex")?.Value;
            if (quickStyleIndex != null)
                ctx.QuickStyles(root).TryGetValue(quickStyleIndex, out styleName);
            styleName ??= "";

            // Tags on this paragraph
            bool? checkboxDone = CheckBoxState(oe, ctx, root);
            var checkbox = checkboxDone == null ? null : checkboxDone.Value ? "- [x] " : "- [ ] ";
            var hashTags = new List<string>();
            foreach (var tag in oe.Elements(ns + "Tag"))
            {
                var index = tag.Attribute("index")?.Value;
                if (index == null || !ctx.TagDefs(root).TryGetValue(index, out var def)) continue;
                if (!def.IsToDo)
                    hashTags.Add("#" + Slug(def.Name));
            }

            // Inline text of this paragraph
            var text = new StringBuilder();
            foreach (var t in oe.Elements(ns + "T"))
                text.Append(InlineToMarkdown(RawText(t), inTable: false));
            var inline = ProtectLineStarts(CollapseInlineWhitespace(text.ToString()));

            if (hashTags.Count > 0)
                inline = string.Join(" ", hashTags) + (inline.Length > 0 ? " " + inline : "");

            var indent = new string(' ', 2 * Math.Max(0, listDepth));
            var headingLevel = HeadingLevel(styleName);

            if (inline.Length > 0)
            {
                if (checkbox != null)
                {
                    // A check box tag always renders as a task list item, bullet or not
                    body.ListLine(indent + checkbox + inline.Replace("\n", "\n" + indent + "  "));
                }
                else if (isListItem)
                {
                    var marker = isNumber ? number.ToString(CultureInfo.InvariantCulture) + ". " : "- ";
                    body.ListLine(indent + marker + inline.Replace("\n", "\n" + indent + new string(' ', marker.Length)));
                }
                else if (headingLevel > 0)
                {
                    // A heading cannot hold line breaks
                    body.Block(new string('#', headingLevel) + " " + inline.Replace("\n", " ").Trim());
                }
                else if (styleName == "code")
                {
                    body.Block("```\n" + Unescape(inline) + "\n```");
                }
                else if (styleName == "cite" || styleName == "blockquote")
                {
                    body.Block("> " + inline.Replace("\n", "\n> "));
                }
                else if (listDepth > 0)
                {
                    // Plain paragraph inside a list item: keep it attached to the item
                    body.ListLine(indent + inline.Replace("\n", "\n" + indent));
                }
                else
                {
                    body.Block(inline);
                }
            }
            else if (checkboxDone != null)
            {
                // A check box tag on a paragraph that carries no text at all (a ticked box in a form):
                // the box itself is the content, so it is written as a symbol and not as a list item
                var symbol = CheckBoxSymbol(checkboxDone.Value);
                if (listDepth > 0) body.ListLine(indent + symbol); else body.Block(symbol);
            }

            // Tables, images and attachments of this paragraph
            foreach (var child in oe.Elements())
            {
                switch (child.Name.LocalName)
                {
                    case "Table":
                        body.Block(RenderTable(child, ctx));
                        break;
                    case "Image":
                        var img = RenderImage(child, ctx);
                        if (img == null) break;
                        if (listDepth > 0) body.ListLine(indent + "  " + img); else body.Block(img);
                        break;
                    case "InsertedFile":
                    case "MediaFile":
                        var link = RenderAttachment(child, ctx);
                        if (link == null) break;
                        if (listDepth > 0) body.ListLine(indent + "  " + link); else body.Block(link);
                        break;
                }
            }

            // Nested paragraphs: one more indentation level
            foreach (var nested in oe.Elements(ns + "OEChildren"))
                RenderOEChildren(nested, ctx, body, listDepth + 1);
        }

        /// <summary>
        /// True / false when the paragraph carries a check box tag (ticked or not), null when it has none
        /// </summary>
        private static bool? CheckBoxState(XElement oe, Ctx ctx, XElement root)
        {
            bool? state = null;
            foreach (var tag in oe.Elements(ctx.Ns + "Tag"))
            {
                var index = tag.Attribute("index")?.Value;
                if (index == null || !ctx.TagDefs(root).TryGetValue(index, out var def) || !def.IsToDo) continue;
                state = string.Equals(tag.Attribute("completed")?.Value, "true", StringComparison.OrdinalIgnoreCase);
            }
            return state;
        }

        /// <summary>
        /// A check box where a task list item cannot be used (inside a table cell, or on a paragraph with
        /// no text): the box as a character, the way a reader of the archive sees it.
        /// </summary>
        private static string CheckBoxSymbol(bool completed) => completed ? "☑" : "☐";

        private static XElement Root(XElement e)
        {
            var r = e;
            while (r.Parent != null) r = r.Parent;
            return r;
        }

        /// <summary>h1..h6 =&gt; 1..6; PageTitle =&gt; 1; everything else =&gt; 0</summary>
        private static int HeadingLevel(string styleName)
        {
            if (string.IsNullOrEmpty(styleName)) return 0;
            if (styleName == "PageTitle") return 1;
            var m = Regex.Match(styleName, @"^h([1-6])$", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value[0] - '0' : 0;
        }

        private static bool HasContent(XElement oe, XNamespace ns)
        {
            foreach (var t in oe.Elements(ns + "T"))
                if (!string.IsNullOrWhiteSpace(Regex.Replace(RawText(t), "<[^>]*>", ""))) return true;
            return oe.Elements(ns + "Table").Any() || oe.Elements(ns + "Image").Any()
                || oe.Elements(ns + "InsertedFile").Any() || oe.Elements(ns + "MediaFile").Any();
        }

        private static string RawText(XElement t)
        {
            var cdata = t.Nodes().OfType<XCData>().FirstOrDefault();
            return cdata?.Value ?? t.Value ?? "";
        }

        private static string PlainText(XElement oe, Ctx ctx)
        {
            if (oe == null) return "";
            var sb = new StringBuilder();
            foreach (var t in oe.Elements(ctx.Ns + "T"))
                sb.Append(InlineToMarkdown(RawText(t), inTable: false));
            return CollapseInlineWhitespace(sb.ToString());
        }

        /*
         * Tables
         */

        private static string RenderTable(XElement table, Ctx ctx)
        {
            var ns = ctx.Ns;
            var rows = table.Elements(ns + "Row").ToList();
            if (rows.Count == 0) return "";

            var columns = rows.Max(r => r.Elements(ns + "Cell").Count());
            if (columns == 0) return "";

            // OneNote says whether the first row is a header; when it does not, the first row is used
            // as the header anyway, because GFM has no header-less table
            var cells = rows
                .Select(r => r.Elements(ns + "Cell")
                    .Select(c => CellContent(c, ctx))
                    .Concat(Enumerable.Repeat("", columns))
                    .Take(columns)
                    .ToList())
                .ToList();

            var sb = new StringBuilder();
            sb.Append("| ").Append(string.Join(" | ", cells[0])).AppendLine(" |");
            sb.Append('|').Append(string.Concat(Enumerable.Repeat("---|", columns))).AppendLine();
            foreach (var row in cells.Skip(1))
                sb.Append("| ").Append(string.Join(" | ", row)).AppendLine(" |");
            return sb.ToString().TrimEnd();
        }

        private static string CellContent(XElement cell, Ctx ctx)
        {
            var ns = ctx.Ns;
            var oeChildren = cell.Element(ns + "OEChildren");
            if (oeChildren == null) return "";

            var root = cell.Document?.Root ?? Root(cell);
            var lines = new List<string>();
            foreach (var oe in oeChildren.Descendants(ns + "OE"))
            {
                var sb = new StringBuilder();
                foreach (var t in oe.Elements(ns + "T"))
                    sb.Append(InlineToMarkdown(RawText(t), inTable: true));
                var line = CollapseInlineWhitespace(sb.ToString());
                // A cell cannot hold a real line break in GFM
                line = line.Replace("\n", "<br>").Trim();

                // A check box tag inside a cell: a task list item is not rendered as one inside a table,
                // so the box is written as a symbol. In a review form every cell of the "Above / OK / Below"
                // columns is a tagged paragraph with no text, and the tick was the whole content of the cell.
                var checkbox = CheckBoxState(oe, ctx, root);
                if (checkbox != null)
                    line = line.Length > 0 ? CheckBoxSymbol(checkbox.Value) + " " + line : CheckBoxSymbol(checkbox.Value);

                if (line.Length > 0) lines.Add(line);
            }
            return string.Join("<br>", lines);
        }

        /*
         * Images and attachments
         */

        private static string RenderImage(XElement image, Ctx ctx)
        {
            var ns = ctx.Ns;
            var alt = image.Attribute("alt")?.Value
                ?? image.Element(ns + "AltText")?.Value
                ?? "image";

            byte[] bytes = null;
            try
            {
                var data = image.Element(ns + "Data")?.Value;
                if (!string.IsNullOrWhiteSpace(data))
                    bytes = Convert.FromBase64String(Regex.Replace(data, @"\s+", ""));

                if (bytes == null)
                {
                    var callbackId = image.Element(ns + "CallbackID")?.Attribute("callbackID")?.Value
                        ?? image.Attribute("callbackID")?.Value;
                    if (!string.IsNullOrEmpty(callbackId) && ctx.Options.GetBinary != null)
                        bytes = ctx.Options.GetBinary(callbackId);
                }
            }
            catch (Exception ex)
            {
                ctx.Warnings.Add($"xml renderer: image could not be decoded ({ex.Message})");
            }

            var index = ++ctx.ImageCounter;

            if (bytes == null || bytes.Length == 0)
            {
                ctx.Warnings.Add($"xml renderer: image {index} has no data in the page XML (see the PDF)");
                return $"*[image {index}: not stored in the page XML, see the PDF version of this page]*";
            }

            if (string.IsNullOrEmpty(ctx.Options.ResourceFolder) || string.IsNullOrEmpty(ctx.Options.MdDir))
            {
                ctx.Warnings.Add($"xml renderer: no resource folder given, image {index} not written");
                return $"*[image {index}: not written]*";
            }

            try
            {
                var extension = Extension(image.Attribute("format")?.Value);
                var fileName = $"{ctx.Options.ImageKeyPrefix}-xml-{index:D3}{extension}";
                Directory.CreateDirectory(ctx.Options.ResourceFolder);
                var path = Path.Combine(ctx.Options.ResourceFolder, fileName);
                var info = new FileInfo(path);
                if (info.Exists && info.IsReadOnly) info.IsReadOnly = false;
                File.WriteAllBytes(path, bytes);

                // Markdown tools cannot display an EMF / WMF metafile: show a converted copy and keep the
                // original next to it, exactly as the normal route does for unpaired originals
                var shown = path;
                try
                {
                    var copy = ctx.Options.DisplayableCopy?.Invoke(path);
                    if (!string.IsNullOrEmpty(copy) && File.Exists(copy)) shown = copy;
                }
                catch (Exception ex)
                {
                    ctx.Warnings.Add($"xml renderer: image {index} could not be converted to a displayable format ({ex.Message})");
                }

                var reference = ToLinkPath(Path.GetRelativePath(ctx.Options.MdDir, shown));
                if (shown == path)
                    return $"![{EscapeLinkText(alt)}]({reference})";

                var originalRef = ToLinkPath(Path.GetRelativePath(ctx.Options.MdDir, path));
                var originalKind = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
                return $"![{EscapeLinkText(alt)}]({reference}) ([original {originalKind}]({originalRef}))";
            }
            catch (Exception ex)
            {
                ctx.Warnings.Add($"xml renderer: image {index} could not be written ({ex.Message})");
                return $"*[image {index}: could not be written]*";
            }
        }

        private static string Extension(string format) => (format ?? "png").ToLowerInvariant() switch
        {
            "jpg" or "jpeg" => ".jpg",
            "gif" => ".gif",
            "bmp" => ".bmp",
            "tiff" or "tif" => ".tif",
            "emf" => ".emf",
            "wmf" => ".wmf",
            _ => ".png",
        };

        private static string RenderAttachment(XElement file, Ctx ctx)
        {
            var name = file.Attribute("preferredName")?.Value
                ?? Path.GetFileName(file.Attribute("pathSource")?.Value ?? "")
                ?? "file";
            if (string.IsNullOrWhiteSpace(name)) name = "file";

            var target = ctx.Options.AttachmentRef?.Invoke(file);
            if (string.IsNullOrEmpty(target))
            {
                ctx.Warnings.Add($"xml renderer: attachment '{name}' has no exported copy, named only");
                return $"*[attachment: {EscapeLinkText(name)}]*";
            }
            return $"[{EscapeLinkText(name)}]({ToLinkPath(target)})";
        }

        /*
         * Inline: the small HTML fragment inside a one:T CDATA
         */

        private static readonly Regex TagRegex = new(@"<(?<close>/?)(?<name>[A-Za-z][A-Za-z0-9]*)(?<attrs>[^>]*?)(?<self>/?)>", RegexOptions.Compiled);

        /// <summary>
        /// Converts the HTML fragment OneNote stores in a one:T element to inline Markdown.
        /// Handles &lt;span style&gt;, &lt;a href&gt;, &lt;br&gt;, &lt;b&gt;/&lt;strong&gt;, &lt;i&gt;/&lt;em&gt;,
        /// &lt;u&gt;, &lt;s&gt;/&lt;strike&gt;/&lt;del&gt;, &lt;sup&gt;, &lt;sub&gt;. Anything else is dropped,
        /// its text kept. Text is escaped so no character can be misread as Markdown.
        /// </summary>
        internal static string InlineToMarkdown(string html, bool inTable)
        {
            if (string.IsNullOrEmpty(html)) return "";

            // The fragment is first cut into segments, each carrying the emphasis open around it, and
            // only then written out. That way the markers can be placed once per *maximal* run of the
            // same formatting (OneNote splits a bold word into several runs: "**16****th**" was the
            // result of writing them as they come) and whitespace can be pushed outside the markers
            // ("**Meeting Date: **" is not bold in CommonMark).
            var segments = new List<InlineSegment>();
            var linkSegments = new List<InlineSegment>();
            // Emphasis open at this point, innermost last; one entry per marker pair
            var markers = new List<Marker>();
            // The tracked tags open at this point, and how many markers each of them added
            var tags = new List<(string Name, int MarkerCount)>();
            string linkHref = null;
            var linkBaseDepth = 0;

            void Add(string text, bool literal = false)
            {
                if (string.IsNullOrEmpty(text)) return;
                (linkHref != null ? linkSegments : segments)
                    .Add(new InlineSegment { Text = text, Stack = [.. markers], Literal = literal });
            }

            void CloseLink()
            {
                // The label is written with the emphasis that is inside the link only: the markers already
                // open around the whole <a> wrap the finished link instead
                var label = EmitSegments(linkSegments, linkBaseDepth).Trim();
                var href = linkHref;
                linkSegments.Clear();
                linkHref = null;
                Add(FinishLink(href, label), literal: true);
            }

            var position = 0;
            foreach (var m in TagRegex.Matches(html).Cast<Match>())
            {
                if (m.Index > position)
                    Add(EscapeText(WebDecode(html[position..m.Index]), inTable));
                position = m.Index + m.Length;

                var name = m.Groups["name"].Value.ToLowerInvariant();
                var isClose = m.Groups["close"].Value == "/";
                var attrs = m.Groups["attrs"].Value;

                if (name == "br")
                {
                    Add(inTable ? "<br>" : "\n");
                    continue;
                }

                if (name == "a")
                {
                    if (!isClose)
                    {
                        // Nested <a> cannot happen in OneNote's fragments; a stray one closes the previous
                        if (linkHref != null) CloseLink();
                        linkHref = WebDecode(Attr(attrs, "href") ?? "");
                        linkBaseDepth = markers.Count;
                        linkSegments.Clear();
                    }
                    else if (linkHref != null)
                    {
                        CloseLink();
                    }
                    continue;
                }

                if (!IsTracked(name))
                    continue; // p, div, font, ...: dropped, their text kept, the stack untouched

                if (isClose)
                {
                    // Close the innermost tag of this name. A closing tag carries no attributes, so the
                    // open stack, not the style, says what to close.
                    for (var i = tags.Count - 1; i >= 0; i--)
                    {
                        if (tags[i].Name != name) continue;
                        // Anything opened after it is closed too, so the markers stay properly nested
                        for (var j = tags.Count - 1; j >= i; j--)
                        {
                            markers.RemoveRange(markers.Count - tags[j].MarkerCount, tags[j].MarkerCount);
                            tags.RemoveAt(j);
                        }
                        break;
                    }
                    continue;
                }

                if (m.Groups["self"].Value == "/")
                    continue; // a self-closing formatting tag carries nothing

                // One stack entry per opened tag, even a style-free span: otherwise its closing tag
                // would pop an enclosing span's formatting instead of its own
                var opened = MarkersFor(name, attrs);
                markers.AddRange(opened);
                tags.Add((name, opened.Count));
            }

            if (position < html.Length)
                Add(EscapeText(WebDecode(html[position..]), inTable));

            if (linkHref != null)
                CloseLink();

            // Anything still open (unbalanced fragment) is closed by EmitSegments, so the markdown stays valid
            return EmitSegments(segments, 0);
        }

        /// <summary>One emphasis marker pair, ex: <c>("**", "**")</c></summary>
        private readonly record struct Marker(string Open, string Close);

        /// <summary>A piece of inline output together with the emphasis that is open around it</summary>
        private sealed class InlineSegment
        {
            public string Text;
            public Marker[] Stack;
            /// <summary>A finished link: its text is markdown already, so no whitespace is moved out of it</summary>
            public bool Literal;
        }

        /// <summary>
        /// Writes the segments out, opening and closing each marker once per maximal run of the same
        /// formatting and keeping whitespace outside the markers.
        /// </summary>
        /// <param name="baseDepth">Markers already open around the whole run; they are not repeated</param>
        private static string EmitSegments(List<InlineSegment> segments, int baseDepth)
        {
            var segs = segments.Where(s => !string.IsNullOrEmpty(s.Text)).ToList();
            if (segs.Count == 0) return "";

            var stacks = segs.Select(s => s.Stack.Length > baseDepth ? s.Stack[baseDepth..] : []).ToArray();

            // Whitespace between two runs carries no emphasis of its own: it keeps only the markers both
            // of its neighbours have. So "**a**<b> </b>**b**" of one bold run stays one run, and a space
            // that is only inside the emphasis is pushed out of it instead of being wrapped in "** **".
            for (var i = 0; i < segs.Count; i++)
            {
                if (segs[i].Literal || !IsBlank(segs[i].Text)) continue;
                var before = i > 0 ? stacks[i - 1] : [];
                var after = i + 1 < segs.Count ? stacks[i + 1] : [];
                var keep = Math.Min(CommonPrefix(before, stacks[i]), CommonPrefix(stacks[i], after));
                stacks[i] = stacks[i][..keep];
            }

            var sb = new StringBuilder();
            for (var i = 0; i < segs.Count; i++)
            {
                var want = stacks[i];
                var before = i > 0 ? stacks[i - 1] : [];
                var after = i + 1 < segs.Count ? stacks[i + 1] : [];
                var carried = CommonPrefix(before, want); // already open, nothing to write
                var kept = CommonPrefix(want, after);     // stays open after this segment

                var text = segs[i].Text;
                var lead = "";
                var trail = "";
                if (!segs[i].Literal && want.Length > carried) (lead, text) = SplitLeadingBlanks(text);
                if (!segs[i].Literal && want.Length > kept) (text, trail) = SplitTrailingBlanks(text);

                sb.Append(lead);
                for (var j = carried; j < want.Length; j++) sb.Append(want[j].Open);
                sb.Append(text);
                for (var j = want.Length - 1; j >= kept; j--) sb.Append(want[j].Close);
                sb.Append(trail);
            }
            return sb.ToString();
        }

        private static int CommonPrefix(Marker[] a, Marker[] b)
        {
            var n = Math.Min(a.Length, b.Length);
            var i = 0;
            while (i < n && a[i] == b[i]) i++;
            return i;
        }

        /// <summary>Spaces, tabs and line breaks. A non-breaking space is content, not whitespace.</summary>
        private static bool IsBlankChar(char c) => c is ' ' or '\t' or '\r' or '\n';

        private static bool IsBlank(string text)
        {
            foreach (var c in text) if (!IsBlankChar(c)) return false;
            return true;
        }

        private static (string Blanks, string Text) SplitLeadingBlanks(string text)
        {
            var i = 0;
            while (i < text.Length && IsBlankChar(text[i])) i++;
            return (text[..i], text[i..]);
        }

        private static (string Text, string Blanks) SplitTrailingBlanks(string text)
        {
            var i = text.Length;
            while (i > 0 && IsBlankChar(text[i - 1])) i--;
            return (text[..i], text[i..]);
        }

        private static string FinishLink(string href, string text)
        {
            var label = text.Trim();
            if (label.Length == 0) label = EscapeLinkText(href);
            if (string.IsNullOrEmpty(href)) return label;

            // onenote: links must survive verbatim so OneNoteLinkTranslatorService / LinkRepair
            // can resolve them into relative links afterwards. http(s) and mailto are kept as they are.
            return $"[{label}]({EscapeLinkTarget(href)})";
        }

        /// <summary>The inline tags whose nesting the renderer follows; everything else is dropped</summary>
        private static bool IsTracked(string name) => name is
            "b" or "strong" or "i" or "em" or "u" or "s" or "strike" or "del" or "sup" or "sub" or "span";

        private static List<Marker> MarkersFor(string name, string attrs)
        {
            var markers = new List<Marker>();

            switch (name)
            {
                case "b":
                case "strong":
                    markers.Add(new Marker("**", "**"));
                    return markers;
                case "i":
                case "em":
                    markers.Add(new Marker("*", "*"));
                    return markers;
                case "u":
                    markers.Add(new Marker("<u>", "</u>")); // no Markdown equivalent: kept as HTML
                    return markers;
                case "s":
                case "strike":
                case "del":
                    markers.Add(new Marker("~~", "~~"));
                    return markers;
                case "sup":
                    markers.Add(new Marker("<sup>", "</sup>"));
                    return markers;
                case "sub":
                    markers.Add(new Marker("<sub>", "</sub>"));
                    return markers;
                case "span":
                    break;
                default:
                    return markers;
            }

            // <span style="...">: only the styles that carry meaning in Markdown
            var style = Attr(attrs, "style") ?? "";
            var declarations = ParseStyle(style);

            if (declarations.TryGetValue("font-weight", out var weight)
                && (weight.Contains("bold", StringComparison.OrdinalIgnoreCase)
                    || (int.TryParse(weight, out var numeric) && numeric >= 600)))
                markers.Add(new Marker("**", "**"));

            if (declarations.TryGetValue("font-style", out var fontStyle) && fontStyle.Contains("italic", StringComparison.OrdinalIgnoreCase))
                markers.Add(new Marker("*", "*"));

            if (declarations.TryGetValue("text-decoration", out var decoration))
            {
                if (decoration.Contains("line-through", StringComparison.OrdinalIgnoreCase)) markers.Add(new Marker("~~", "~~"));
                if (decoration.Contains("underline", StringComparison.OrdinalIgnoreCase)) markers.Add(new Marker("<u>", "</u>"));
            }

            // Highlight: OneNote writes background / background-color. "white" and the default are noise
            // (MarkdownCleanup strips those from the Word route too), so only a real colour is kept.
            if ((declarations.TryGetValue("background", out var background) || declarations.TryGetValue("background-color", out background))
                && IsRealHighlight(background))
                markers.Add(new Marker("==", "=="));

            return markers;
        }

        private static bool IsRealHighlight(string value)
        {
            var v = (value ?? "").Trim().ToLowerInvariant();
            return v.Length > 0 && v != "white" && v != "transparent" && v != "none"
                && v != "#ffffff" && v != "#fff" && v != "rgb(255,255,255)" && v != "rgb(255, 255, 255)";
        }

        private static Dictionary<string, string> ParseStyle(string style)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var declaration in style.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = declaration.Split(':', 2);
                if (parts.Length == 2) result[parts[0].Trim()] = parts[1].Trim();
            }
            return result;
        }

        private static string Attr(string attrs, string name)
        {
            var m = Regex.Match(attrs, name + @"\s*=\s*(?:'(?<v>[^']*)'|""(?<v>[^""]*)""|(?<v>[^\s>]+))", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups["v"].Value : null;
        }

        /*
         * Literal protection: no character of the original text may be read as Markdown
         */

        /// <summary>
        /// Escapes every character that Markdown would otherwise read as syntax. Angle brackets become
        /// entities (a backslash does not protect them from being read as an HTML tag), the rest is
        /// backslash-escaped.
        /// </summary>
        internal static string EscapeText(string text, bool inTable)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder(text.Length + 8);
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                switch (c)
                {
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '&': sb.Append("&amp;"); break;
                    case '\\':
                    case '`':
                    case '*':
                    case '_':
                    case '[':
                    case ']':
                        sb.Append('\\').Append(c);
                        break;
                    // "==" is a highlight and "~~" a strikethrough in the extended syntax; a single
                    // one of either is ordinary text and is left alone, so plain prose stays readable
                    case '=':
                    case '~':
                        if (i + 1 < text.Length && text[i + 1] == c)
                        {
                            sb.Append('\\').Append(c).Append('\\').Append(c);
                            i++;
                        }
                        else sb.Append(c);
                        break;
                    case '|':
                        sb.Append(inTable ? "\\|" : "|");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Escapes what would break out of a [link label]
        /// </summary>
        internal static string EscapeLinkText(string text)
            => (text ?? "").Replace("\\", "\\\\").Replace("[", "\\[").Replace("]", "\\]").Replace("\n", " ");

        /// <summary>
        /// Escapes what would break out of a (link target). An onenote: URL is otherwise untouched,
        /// so the link translator and the link repair step still recognise it.
        /// </summary>
        internal static string EscapeLinkTarget(string href)
        {
            var target = (href ?? "").Replace("\n", "").Replace("\r", "");
            if (target.Contains(' ') || target.Contains('(') || target.Contains(')'))
                return "<" + target.Replace("<", "%3C").Replace(">", "%3E") + ">";
            return target;
        }

        /// <summary>
        /// A line of plain text that happens to start like a heading, a list item, a quote or a
        /// thematic break must not be read as one
        /// </summary>
        internal static string ProtectLineStarts(string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return markdown;
            var lines = markdown.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                lines[i] = Regex.Replace(lines[i], @"^(?<indent>[ \t]*)(?<token>#{1,6}(?=\s|$)|>|[-+](?=\s)|\d{1,9}[.)](?=\s))",
                    m => m.Groups["indent"].Value + "\\" + m.Groups["token"].Value);
            }
            return string.Join("\n", lines);
        }

        /// <summary>
        /// OneNote stores soft wrapping as real spaces; collapse runs of spaces and tabs but keep
        /// the line breaks that came from &lt;br&gt;
        /// </summary>
        internal static string CollapseInlineWhitespace(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\u00A0", "\u00A0"); // non-breaking space kept as it is
            text = Regex.Replace(text, @"[ \t]{2,}", " ");
            text = string.Join("\n", text.Split('\n').Select(l => l.TrimEnd()));
            return text.Trim('\n').Trim();
        }

        private static string Unescape(string markdown)
            => Regex.Replace(markdown ?? "", @"\\(.)", "$1")
                .Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");

        /// <summary>
        /// Decodes the HTML entities OneNote writes (&amp;amp;, &amp;lt;, &amp;nbsp;, &amp;#233;, &amp;#x2019;)
        /// </summary>
        internal static string WebDecode(string text)
        {
            if (string.IsNullOrEmpty(text) || !text.Contains('&')) return text ?? "";
            return Regex.Replace(text, @"&(?:#(?<dec>[0-9]{1,7})|#[xX](?<hex>[0-9a-fA-F]{1,6})|(?<name>[A-Za-z][A-Za-z0-9]{1,31}));", m =>
            {
                if (m.Groups["dec"].Success && int.TryParse(m.Groups["dec"].Value, out var dec) && dec is > 0 and <= 0x10FFFF)
                    return char.ConvertFromUtf32(dec);
                if (m.Groups["hex"].Success && int.TryParse(m.Groups["hex"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex) && hex is > 0 and <= 0x10FFFF)
                    return char.ConvertFromUtf32(hex);
                return m.Groups["name"].Value switch
                {
                    "amp" => "&",
                    "lt" => "<",
                    "gt" => ">",
                    "quot" => "\"",
                    "apos" => "'",
                    "nbsp" => "\u00A0",
                    "aring" => "å",
                    "Aring" => "Å",
                    "aelig" => "æ",
                    "AElig" => "Æ",
                    "oslash" => "ø",
                    "Oslash" => "Ø",
                    "eacute" => "é",
                    "hellip" => "…",
                    "ndash" => "–",
                    "mdash" => "—",
                    "rsquo" => "’",
                    "lsquo" => "‘",
                    "ldquo" => "“",
                    "rdquo" => "”",
                    "bull" => "•",
                    "middot" => "·",
                    "copy" => "©",
                    "reg" => "®",
                    "trade" => "™",
                    "deg" => "°",
                    "euro" => "€",
                    "pound" => "£",
                    _ => m.Value, // unknown entity: left as it stands, nothing lost
                };
            });
        }

        private static string ToLinkPath(string relativePath)
            => (relativePath ?? "").Replace('\\', '/').Replace(" ", "%20").Replace("#", "%23").Replace("?", "%3F");

        /// <summary>
        /// Makes a tag name usable as a #tag (same shape as ArchiveHelpers.ToTag produces)
        /// </summary>
        internal static string Slug(string name)
        {
            var slug = Regex.Replace((name ?? "tag").Trim().ToLowerInvariant(), @"[\s/\\]+", "-");
            slug = Regex.Replace(slug, @"[^\p{L}\p{N}_\-]", "");
            slug = Regex.Replace(slug, @"-{2,}", "-").Trim('-');
            return slug.Length == 0 ? "tag" : slug;
        }
    }
}
