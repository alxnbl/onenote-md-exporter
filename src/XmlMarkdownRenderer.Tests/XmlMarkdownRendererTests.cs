// Archive fork: unit tests for the XML-direct Markdown renderer (the fallback path).
// Hand-written OneNote 2013 page XML, no OneNote and no COM involved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using alxnbl.OneNoteMdExporter.Services;
using NUnit.Framework;

namespace alxnbl.OneNoteMdExporter.Tests
{
    [TestFixture]
    public class XmlMarkdownRendererTests
    {
        private const string Ns = "http://schemas.microsoft.com/office/onenote/2013/onenote";

        private List<string> warnings;

        [SetUp]
        public void SetUp() => warnings = [];

        /// <summary>
        /// Wraps paragraph XML in a one:Page with the quick styles and tag definitions a real page has
        /// </summary>
        private static string Page(string outlineBody, string extraDefs = "")
            => $"""
                <one:Page xmlns:one="{Ns}">
                  <one:QuickStyleDef index="0" name="PageTitle" />
                  <one:QuickStyleDef index="1" name="h1" />
                  <one:QuickStyleDef index="2" name="h2" />
                  <one:QuickStyleDef index="3" name="h3" />
                  <one:QuickStyleDef index="4" name="p" />
                  <one:QuickStyleDef index="5" name="cite" />
                  <one:QuickStyleDef index="6" name="code" />
                  {extraDefs}
                  <one:Title><one:OE><one:T><![CDATA[Test page]]></one:T></one:OE></one:Title>
                  <one:Outline><one:OEChildren>
                {outlineBody}
                  </one:OEChildren></one:Outline>
                </one:Page>
                """;

        private string Render(string pageXml, XmlRenderOptions options = null)
        {
            var root = XDocument.Parse(pageXml).Root;
            return XmlMarkdownRenderer.Render(root, root.Name.Namespace, options, warnings);
        }

        private static string Oe(string cdata, string attrs = "")
            => $"""<one:OE {attrs}><one:T><![CDATA[{cdata}]]></one:T></one:OE>""";

        /*
         * Headings
         */

        [Test]
        public void QuickStyleIndex_becomes_a_heading_of_the_matching_level()
        {
            var md = Render(Page(
                Oe("Top", "quickStyleIndex=\"1\"") +
                Oe("Second", "quickStyleIndex=\"2\"") +
                Oe("Third", "quickStyleIndex=\"3\"") +
                Oe("Just text", "quickStyleIndex=\"4\"")));

            Assert.That(md, Does.Contain("# Top"));
            Assert.That(md, Does.Contain("## Second"));
            Assert.That(md, Does.Contain("### Third"));
            Assert.That(md, Does.Contain("\nJust text"));
            Assert.That(md, Does.Not.Contain("# Just text"));
        }

        [Test]
        public void Cite_and_code_quick_styles_become_a_quote_and_a_fenced_block()
        {
            var md = Render(Page(
                Oe("A quotation", "quickStyleIndex=\"5\"") +
                Oe("var x = 1;", "quickStyleIndex=\"6\"")));

            Assert.That(md, Does.Contain("> A quotation"));
            Assert.That(md, Does.Contain("```\nvar x = 1;\n```"));
        }

        [Test]
        public void Title_is_only_emitted_when_asked_for()
        {
            Assert.That(Render(Page(Oe("body"))), Does.Not.Contain("Test page"));
            Assert.That(Render(Page(Oe("body")), new XmlRenderOptions { IncludeTitle = true }),
                Does.Contain("# Test page"));
        }

        /*
         * Inline formatting
         */

        [Test]
        public void Bold_italic_underline_and_strikethrough_spans_are_converted()
        {
            var md = Render(Page(Oe(
                "<span style='font-weight:bold'>bold</span> " +
                "<span style='font-style:italic'>italic</span> " +
                "<span style='text-decoration:underline'>under</span> " +
                "<span style='text-decoration:line-through'>gone</span>")));

            Assert.That(md, Does.Contain("**bold**"));
            Assert.That(md, Does.Contain("*italic*"));
            Assert.That(md, Does.Contain("<u>under</u>"));
            Assert.That(md, Does.Contain("~~gone~~"));
        }

        [Test]
        public void Plain_html_formatting_tags_are_converted_too()
        {
            var md = Render(Page(Oe("<b>b</b> <i>i</i> <u>u</u> <s>s</s>")));
            Assert.That(md, Does.Contain("**b**"));
            Assert.That(md, Does.Contain("*i*"));
            Assert.That(md, Does.Contain("<u>u</u>"));
            Assert.That(md, Does.Contain("~~s~~"));
        }

        [Test]
        public void A_coloured_background_becomes_a_highlight_but_white_does_not()
        {
            Assert.That(Render(Page(Oe("<span style='background:yellow'>hi</span>"))), Does.Contain("==hi=="));
            Assert.That(Render(Page(Oe("<span style='background:white'>hi</span>"))), Does.Not.Contain("=="));
        }

        [Test]
        public void A_style_free_span_is_unwrapped_and_its_text_kept()
        {
            var md = Render(Page(Oe("<span style='color:black'>kept</span>")));
            Assert.That(md.Trim(), Is.EqualTo("kept"));
        }

        [Test]
        public void A_br_becomes_a_line_break()
        {
            var md = Render(Page(Oe("first<br>second")));
            Assert.That(md, Does.Contain("first\nsecond"));
        }

        /*
         * Lists
         */

        [Test]
        public void Bullets_become_dashes_and_numbers_are_counted_up()
        {
            var md = Render(Page(
                """<one:OE><one:List><one:Bullet bullet="2"/></one:List><one:T><![CDATA[one]]></one:T></one:OE>""" +
                """<one:OE><one:List><one:Bullet bullet="2"/></one:List><one:T><![CDATA[two]]></one:T></one:OE>""" +
                """<one:OE><one:List><one:Number numberSequence="0" numberFormat="##."/></one:List><one:T><![CDATA[a]]></one:T></one:OE>""" +
                """<one:OE><one:List><one:Number numberSequence="0" numberFormat="##."/></one:List><one:T><![CDATA[b]]></one:T></one:OE>""" +
                """<one:OE><one:List><one:Number numberSequence="0" numberFormat="##."/></one:List><one:T><![CDATA[c]]></one:T></one:OE>"""));

            Assert.That(md, Does.Contain("- one\n- two"));
            Assert.That(md, Does.Contain("1. a"));
            Assert.That(md, Does.Contain("2. b"));
            Assert.That(md, Does.Contain("3. c"));
        }

        [Test]
        public void Nested_OEChildren_become_an_indented_list()
        {
            var md = Render(Page(
                """
                <one:OE><one:List><one:Bullet bullet="2"/></one:List><one:T><![CDATA[outer]]></one:T>
                  <one:OEChildren>
                    <one:OE><one:List><one:Bullet bullet="2"/></one:List><one:T><![CDATA[inner]]></one:T>
                      <one:OEChildren>
                        <one:OE><one:List><one:Bullet bullet="2"/></one:List><one:T><![CDATA[deepest]]></one:T></one:OE>
                      </one:OEChildren>
                    </one:OE>
                  </one:OEChildren>
                </one:OE>
                """));

            Assert.That(md, Does.Contain("- outer"));
            Assert.That(md, Does.Contain("\n  - inner"));
            Assert.That(md, Does.Contain("\n    - deepest"));
        }

        /*
         * Tables
         */

        [Test]
        public void A_table_becomes_a_GFM_table_with_pipes_and_line_breaks_handled()
        {
            var md = Render(Page($$"""
                <one:OE><one:Table>
                  <one:Row>
                    <one:Cell><one:OEChildren><one:OE><one:T><![CDATA[Name]]></one:T></one:OE></one:OEChildren></one:Cell>
                    <one:Cell><one:OEChildren><one:OE><one:T><![CDATA[Value]]></one:T></one:OE></one:OEChildren></one:Cell>
                  </one:Row>
                  <one:Row>
                    <one:Cell><one:OEChildren><one:OE><one:T><![CDATA[a|b]]></one:T></one:OE></one:OEChildren></one:Cell>
                    <one:Cell><one:OEChildren><one:OE><one:T><![CDATA[line one<br>line two]]></one:T></one:OE></one:OEChildren></one:Cell>
                  </one:Row>
                </one:Table></one:OE>
                """));

            Assert.That(md, Does.Contain("| Name | Value |"));
            Assert.That(md, Does.Contain("|---|---|"));
            // A literal pipe inside a cell is escaped, so the column count stays right
            Assert.That(md, Does.Contain(@"| a\|b | line one<br>line two |"));
            // Only the unescaped pipes are column separators: 3 of them for 2 columns
            var dataRow = md.Split('\n').First(l => l.Contains(@"a\|b"));
            var separators = System.Text.RegularExpressions.Regex.Matches(dataRow, @"(?<!\\)\|").Count;
            Assert.That(separators, Is.EqualTo(3), "an escaped pipe must not create a third column");
        }

        [Test]
        public void A_style_free_span_inside_a_bold_span_does_not_end_the_bold()
        {
            var md = Render(Page(Oe(
                "<span style='font-weight:bold'>one <span style='color:black'>two</span> three</span>")));

            Assert.That(md.Trim(), Is.EqualTo("**one two three**"));
        }

        [Test]
        public void A_short_row_is_padded_to_the_table_width()
        {
            var md = Render(Page("""
                <one:OE><one:Table>
                  <one:Row>
                    <one:Cell><one:OEChildren><one:OE><one:T><![CDATA[A]]></one:T></one:OE></one:OEChildren></one:Cell>
                    <one:Cell><one:OEChildren><one:OE><one:T><![CDATA[B]]></one:T></one:OE></one:OEChildren></one:Cell>
                  </one:Row>
                  <one:Row>
                    <one:Cell><one:OEChildren><one:OE><one:T><![CDATA[only]]></one:T></one:OE></one:OEChildren></one:Cell>
                  </one:Row>
                </one:Table></one:OE>
                """));

            Assert.That(md, Does.Contain("| only |  |"));
        }

        /*
         * Links
         */

        [Test]
        public void A_web_link_becomes_a_markdown_link()
        {
            var md = Render(Page(Oe("""see <a href="https://example.com/docs">the docs</a> now""")));
            Assert.That(md, Does.Contain("[the docs](https://example.com/docs)"));
        }

        [Test]
        public void A_onenote_link_is_preserved_verbatim_for_the_link_repair_step()
        {
            const string url = "onenote:#Page%20title&section-id={1234-5678}&page-id={ABCD}&end";
            var md = Render(Page(Oe($"""go to <a href="{url}">that page</a>""")));

            Assert.That(md, Does.Contain($"[that page]({url})"),
                "the onenote: URL must reach OneNoteLinkTranslatorService unchanged");
        }

        [Test]
        public void A_link_target_with_spaces_or_brackets_is_wrapped_in_angle_brackets()
        {
            var md = Render(Page(Oe("""<a href="onenote:C:\My Notes\x.one#Page (copy)">x</a>""")));
            Assert.That(md, Does.Contain(@"[x](<onenote:C:\My Notes\x.one#Page (copy)>)"));
        }

        /*
         * Images
         */

        [Test]
        public void An_image_with_one_Data_is_written_to_the_resource_folder_and_linked_relatively()
        {
            var temp = Path.Combine(Path.GetTempPath(), "xmlmdrender-" + Guid.NewGuid().ToString("N")[..8]);
            var mdDir = Path.Combine(temp, "Section");
            var resources = Path.Combine(mdDir, "resources");
            Directory.CreateDirectory(mdDir);
            try
            {
                // A 1x1 transparent PNG
                const string png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8AAAwAB/AF+AQeFAAAAAElFTkSuQmCC";
                var md = Render(
                    Page($"""<one:OE><one:Image format="png" alt="a chart"><one:Data>{png}</one:Data></one:Image></one:OE>"""),
                    new XmlRenderOptions { ResourceFolder = resources, MdDir = mdDir, ImageKeyPrefix = "abc123" });

                Assert.That(md, Does.Contain("![a chart](resources/abc123-xml-001.png)"));
                Assert.That(File.Exists(Path.Combine(resources, "abc123-xml-001.png")), Is.True);
                Assert.That(new FileInfo(Path.Combine(resources, "abc123-xml-001.png")).Length, Is.GreaterThan(0));
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }

        [Test]
        public void An_EMF_image_is_shown_through_a_converted_copy_with_the_original_linked_next_to_it()
        {
            // Markdown tools cannot display a metafile. The caller converts it (LegacyFormatConverter
            // in the exporter, a stand-in here) and the renderer must show the copy and keep the original.
            var temp = Path.Combine(Path.GetTempPath(), "xmlmdrender-" + Guid.NewGuid().ToString("N")[..8]);
            var mdDir = Path.Combine(temp, "Section");
            var resources = Path.Combine(mdDir, "resources");
            Directory.CreateDirectory(mdDir);
            try
            {
                var converted = new List<string>();
                var md = Render(
                    Page($"""<one:OE><one:Image format="emf" alt="org chart"><one:Data>{Convert.ToBase64String([1, 2, 3, 4])}</one:Data></one:Image></one:OE>"""),
                    new XmlRenderOptions
                    {
                        ResourceFolder = resources,
                        MdDir = mdDir,
                        ImageKeyPrefix = "cbccc8359549",
                        DisplayableCopy = path =>
                        {
                            converted.Add(path);
                            if (!path.EndsWith(".emf", StringComparison.OrdinalIgnoreCase)) return null;
                            var png = Path.ChangeExtension(path, ".png");
                            File.WriteAllBytes(png, [5, 6, 7]);
                            return png;
                        },
                    });

                Assert.That(md, Does.Contain(
                    "![org chart](resources/cbccc8359549-xml-001.png) ([original EMF](resources/cbccc8359549-xml-001.emf))"));
                Assert.That(converted, Has.Count.EqualTo(1), "the callback is asked once per written image");
                Assert.That(File.Exists(Path.Combine(resources, "cbccc8359549-xml-001.emf")), Is.True,
                    "the EMF original is kept next to the copy");
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }

        [Test]
        public void Without_a_conversion_callback_the_image_file_is_referenced_as_it_is()
        {
            var temp = Path.Combine(Path.GetTempPath(), "xmlmdrender-" + Guid.NewGuid().ToString("N")[..8]);
            var mdDir = Path.Combine(temp, "Section");
            Directory.CreateDirectory(mdDir);
            try
            {
                var md = Render(
                    Page($"""<one:OE><one:Image format="emf" alt="x"><one:Data>{Convert.ToBase64String([1, 2, 3, 4])}</one:Data></one:Image></one:OE>"""),
                    new XmlRenderOptions { ResourceFolder = Path.Combine(mdDir, "resources"), MdDir = mdDir, ImageKeyPrefix = "k" });

                Assert.That(md, Does.Contain("![x](resources/k-xml-001.emf)"));
                Assert.That(md, Does.Not.Contain("original EMF"));
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }

        [Test]
        public void A_failing_conversion_callback_leaves_the_image_usable_and_warns()
        {
            var temp = Path.Combine(Path.GetTempPath(), "xmlmdrender-" + Guid.NewGuid().ToString("N")[..8]);
            var mdDir = Path.Combine(temp, "Section");
            Directory.CreateDirectory(mdDir);
            try
            {
                var md = Render(
                    Page($"""<one:OE><one:Image format="wmf" alt="x"><one:Data>{Convert.ToBase64String([1, 2, 3, 4])}</one:Data></one:Image></one:OE>"""),
                    new XmlRenderOptions
                    {
                        ResourceFolder = Path.Combine(mdDir, "resources"),
                        MdDir = mdDir,
                        ImageKeyPrefix = "k",
                        DisplayableCopy = _ => throw new InvalidOperationException("no GDI here"),
                    });

                Assert.That(md, Does.Contain("![x](resources/k-xml-001.wmf)"), "the original is still referenced");
                Assert.That(warnings, Has.Some.Contains("could not be converted"));
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }

        [Test]
        public void An_image_with_no_data_is_reported_as_a_warning_and_a_visible_note()
        {
            var md = Render(Page("""<one:OE><one:Image format="png" callbackID="cb1" /></one:OE>"""));
            Assert.That(md, Does.Contain("image 1"));
            Assert.That(warnings, Has.Some.Contains("no data in the page XML"));
        }

        /*
         * Attachments
         */

        [Test]
        public void An_inserted_file_uses_the_link_the_attachment_pipeline_gives()
        {
            var md = Render(
                Page("""<one:OE><one:InsertedFile pathCache="C:\cache\x.bin" preferredName="Budget 2015.xlsx" /></one:OE>"""),
                new XmlRenderOptions { AttachmentRef = _ => "resources/abc_Budget-2015.xlsx" });

            Assert.That(md, Does.Contain("[Budget 2015.xlsx](resources/abc_Budget-2015.xlsx)"));
        }

        [Test]
        public void An_inserted_file_without_an_exported_copy_is_still_named()
        {
            var md = Render(Page("""<one:OE><one:InsertedFile preferredName="Lost.docx" /></one:OE>"""));
            Assert.That(md, Does.Contain("Lost.docx"));
            Assert.That(warnings, Has.Some.Contains("no exported copy"));
        }

        /*
         * Ink
         */

        [Test]
        public void Ink_elements_produce_nothing()
        {
            var md = Render(Page(
                """<one:OE><one:InkDrawing><one:InkData>AQID</one:InkData></one:InkDrawing></one:OE>""" +
                """<one:OE><one:InkWord recognizedText="hello"><one:InkData>AQID</one:InkData></one:InkWord></one:OE>""" +
                Oe("real text")));

            Assert.That(md.Trim(), Is.EqualTo("real text"), "ink is covered by the .ink.svg and the PDF");
        }

        /*
         * Literal protection
         */

        [Test]
        public void Literal_brackets_and_angle_brackets_survive_as_text()
        {
            var md = Render(Page(Oe("a [b] c &lt;div&gt; d *star* e_f")));

            Assert.That(md, Does.Contain(@"\[b\]"), "brackets must not become a link");
            Assert.That(md, Does.Contain("&lt;div&gt;"), "angle brackets must not become an HTML tag");
            Assert.That(md, Does.Contain(@"\*star\*"), "asterisks must not become emphasis");
            Assert.That(md, Does.Contain(@"e\_f"));
        }

        [Test]
        public void A_line_that_starts_like_markdown_syntax_is_escaped()
        {
            Assert.That(Render(Page(Oe("# not a heading"))), Does.Contain(@"\# not a heading"));
            Assert.That(Render(Page(Oe("- not a bullet"))), Does.Contain(@"\- not a bullet"));
            Assert.That(Render(Page(Oe("&gt; not a quote"))), Does.Contain(@"&gt; not a quote"));
            Assert.That(Render(Page(Oe("1. not a list"))), Does.Contain(@"\1. not a list"));
        }

        [Test]
        public void A_hash_inside_a_line_is_left_alone()
        {
            Assert.That(Render(Page(Oe("Sprint #4 review"))), Does.Contain("Sprint #4 review"));
        }

        [Test]
        public void Double_equals_and_double_tilde_are_escaped_but_single_ones_are_not()
        {
            Assert.That(Render(Page(Oe("a == b"))), Does.Contain(@"a \=\= b"));
            Assert.That(Render(Page(Oe("x = y ~ z"))), Does.Contain("x = y ~ z"));
        }

        /*
         * Tags
         */

        [Test]
        public void A_To_Do_tag_becomes_a_task_list_item_checked_or_unchecked()
        {
            const string defs = """
                <one:TagDef index="0" type="3" symbol="3" name="To Do" />
                """;

            var open = Render(Page(
                """<one:OE><one:Tag index="0" completed="false" /><one:T><![CDATA[call Anne]]></one:T></one:OE>""", defs));
            var done = Render(Page(
                """<one:OE><one:Tag index="0" completed="true" /><one:T><![CDATA[send report]]></one:T></one:OE>""", defs));

            Assert.That(open, Does.Contain("- [ ] call Anne"));
            Assert.That(done, Does.Contain("- [x] send report"));
        }

        [Test]
        public void Another_tag_becomes_a_hash_tag_in_front_of_the_text()
        {
            const string defs = """<one:TagDef index="1" type="17" symbol="17" name="Important question" />""";
            var md = Render(Page(
                """<one:OE><one:Tag index="1" completed="false" /><one:T><![CDATA[why?]]></one:T></one:OE>""", defs));

            Assert.That(md, Does.Contain("#important-question why?"));
        }

        [Test]
        public void A_tag_on_a_paragraph_without_text_does_not_throw()
        {
            const string defs = """<one:TagDef index="0" type="3" symbol="3" name="To Do" />""";
            Assert.That(() => Render(Page("""<one:OE><one:Tag index="0" completed="false" /></one:OE>""", defs)),
                Throws.Nothing);
        }

        /*
         * Check boxes that cannot be a task list item (gap 2 of the 75-page run)
         */

        private const string ToDoDef = """<one:TagDef index="0" type="3" symbol="3" name="To Do" />""";

        [Test]
        public void A_check_box_on_a_paragraph_with_no_text_becomes_a_symbol()
        {
            var open = Render(Page("""<one:OE><one:Tag index="0" completed="false" /><one:T><![CDATA[]]></one:T></one:OE>""", ToDoDef));
            var done = Render(Page("""<one:OE><one:Tag index="0" completed="true" /><one:T><![CDATA[]]></one:T></one:OE>""", ToDoDef));

            Assert.That(open.Trim(), Is.EqualTo("☐"), "an empty task list item says nothing about the box");
            Assert.That(done.Trim(), Is.EqualTo("☑"));
        }

        [Test]
        public void A_cell_whose_only_content_is_a_check_box_tag_shows_the_box()
        {
            // The review form of the test notebook: one tagged, text-less paragraph per cell of the
            // "Above / OK / Below" columns. Before the fix those cells came out empty.
            var md = Render(Page("""
                <one:OE><one:Table>
                  <one:Row>
                    <one:Cell><one:OEChildren><one:OE><one:T><![CDATA[Overall assessment]]></one:T></one:OE></one:OEChildren></one:Cell>
                    <one:Cell><one:OEChildren><one:OE><one:Tag index="0" completed="true" /><one:T><![CDATA[]]></one:T></one:OE></one:OEChildren></one:Cell>
                    <one:Cell><one:OEChildren><one:OE><one:Tag index="0" completed="false" /><one:T><![CDATA[]]></one:T></one:OE></one:OEChildren></one:Cell>
                  </one:Row>
                </one:Table></one:OE>
                """, ToDoDef));

            Assert.That(md, Does.Contain("| Overall assessment | ☑ | ☐ |"));
        }

        [Test]
        public void A_tagged_cell_that_also_has_text_keeps_both()
        {
            var md = Render(Page("""
                <one:OE><one:Table>
                  <one:Row>
                    <one:Cell><one:OEChildren><one:OE><one:Tag index="0" completed="true" /><one:T><![CDATA[sent]]></one:T></one:OE></one:OEChildren></one:Cell>
                  </one:Row>
                </one:Table></one:OE>
                """, ToDoDef));

            Assert.That(md, Does.Contain("| ☑ sent |"));
            Assert.That(md, Does.Not.Contain("- [x]"), "a task list item is not rendered as one inside a table cell");
        }

        /*
         * Emphasis: whitespace outside the markers (gap 3) and one marker pair per run (gap 4)
         */

        [Test]
        public void Trailing_space_of_a_bold_run_is_moved_outside_the_markers()
        {
            var md = Render(Page(Oe("<b>Meeting Date: </b>12-03-2021")));

            Assert.That(md.Trim(), Is.EqualTo("**Meeting Date:** 12-03-2021"),
                "'**Meeting Date: **' is not bold in CommonMark");
        }

        [Test]
        public void Leading_space_of_an_emphasised_run_is_moved_outside_too()
        {
            var md = Render(Page(Oe("Join<b> on your computer </b>or phone")));
            Assert.That(md.Trim(), Is.EqualTo("Join **on your computer** or phone"));
        }

        [Test]
        public void A_run_that_is_only_padded_with_spaces_keeps_its_emphasis()
        {
            var md = Render(Page(Oe("<span style='font-weight:bold'>Join on your computer or mobile app </span>")));
            Assert.That(md.Trim(), Is.EqualTo("**Join on your computer or mobile app**"));
            Assert.That(md, Does.Not.Contain("** "), "a space before the closing marker breaks the bold");
        }

        [Test]
        public void Italic_highlight_and_strikethrough_move_their_spaces_out_as_well()
        {
            Assert.That(Render(Page(Oe("<i>when: </i>today"))).Trim(), Is.EqualTo("*when:* today"));
            Assert.That(Render(Page(Oe("<s>old price </s>new"))).Trim(), Is.EqualTo("~~old price~~ new"));
            Assert.That(Render(Page(Oe("<span style='background:yellow'>marked </span>text"))).Trim(),
                Is.EqualTo("==marked== text"));
        }

        [Test]
        public void Two_adjacent_bold_runs_become_one_bold_run()
        {
            var md = Render(Page(Oe("<b>16</b><b>th</b>")));

            Assert.That(md.Trim(), Is.EqualTo("**16th**"), "'**16****th**' is not bold in CommonMark");
            Assert.That(md, Does.Not.Contain("****"));
        }

        [Test]
        public void Adjacent_runs_merge_across_tag_shapes_but_a_space_between_them_stays_outside()
        {
            // <b> and a bold <span> are the same formatting, so one pair of markers covers both
            Assert.That(Render(Page(Oe("<b>16</b><span style='font-weight:bold'>th</span>"))).Trim(),
                Is.EqualTo("**16th**"));
            // Two bold words with plain text between them are still two runs
            Assert.That(Render(Page(Oe("<b>one</b> <b>two</b>"))).Trim(), Is.EqualTo("**one** **two**"));
        }

        [Test]
        public void Different_formatting_still_nests_inside_a_bold_run()
        {
            var md = Render(Page(Oe("<b>bold <i>and italic</i> bold</b>")));
            Assert.That(md.Trim(), Is.EqualTo("**bold *and italic* bold**"));
        }

        [Test]
        public void An_emphasised_link_keeps_its_markers_outside_the_link()
        {
            var md = Render(Page(Oe("""<b>see <a href="https://example.com">the docs</a></b>""")));
            Assert.That(md.Trim(), Is.EqualTo("**see [the docs](https://example.com)**"));
        }

        [Test]
        public void Emphasis_inside_a_link_label_is_kept_inside_the_label()
        {
            var md = Render(Page(Oe("""<a href="https://example.com"><b>bold</b> label</a>""")));
            Assert.That(md.Trim(), Is.EqualTo("[**bold** label](https://example.com)"));
        }

        /*
         * Characters and entities
         */

        [Test]
        public void Danish_characters_survive_unchanged()
        {
            var md = Render(Page(Oe("Blåbærgrød på Ærø med Øl — æøå ÆØÅ")));
            Assert.That(md, Does.Contain("Blåbærgrød på Ærø med Øl — æøå ÆØÅ"));
        }

        [Test]
        public void Html_entities_are_decoded()
        {
            var md = Render(Page(Oe("R&amp;D caf&eacute; 100&nbsp;kr &#248;re &#xE6;g &hellip;")));

            Assert.That(md, Does.Contain("R&amp;D"), "an ampersand is re-encoded so it is not read as an entity");
            Assert.That(md, Does.Contain("café"));
            Assert.That(md, Does.Contain("100\u00A0kr"));
            Assert.That(md, Does.Contain("øre"));
            Assert.That(md, Does.Contain("æg"));
            Assert.That(md, Does.Contain("…"));
        }

        [Test]
        public void An_unknown_entity_is_left_as_it_stands()
        {
            var md = Render(Page(Oe("&zzzz; stays")));
            Assert.That(md, Does.Contain("zzzz"), "nothing may be silently dropped");
        }

        /*
         * Robustness
         */

        [Test]
        public void An_empty_page_renders_to_nothing_rather_than_throwing()
        {
            var md = Render($"""<one:Page xmlns:one="{Ns}"><one:Outline><one:OEChildren /></one:Outline></one:Page>""");
            Assert.That(md.Trim(), Is.Empty);
        }

        [Test]
        public void An_unbalanced_html_fragment_still_produces_valid_markdown()
        {
            var md = Render(Page(Oe("<span style='font-weight:bold'>never closed")));
            Assert.That(md, Does.Contain("**never closed**"));
        }

        [Test]
        public void A_page_with_no_outline_at_all_does_not_throw()
        {
            Assert.That(() => Render($"""<one:Page xmlns:one="{Ns}" />"""), Throws.Nothing);
        }

        [Test]
        public void Render_rejects_a_null_page()
        {
            Assert.That(() => XmlMarkdownRenderer.Render(null, null, null, null), Throws.ArgumentNullException);
        }
    }
}
