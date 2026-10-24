using alxnbl.OneNoteMdExporter.Services;
using NUnit.Framework;

namespace alxnbl.OneNoteMdExporter.Tests
{
    public class MarkdownCleanupTests
    {
        // ---- UnescapeAngleBrackets ----

        [Test]
        public void EscapedTagLikeAngleBecomesEntity()
        {
            Assert.That(MarkdownCleanup.UnescapeAngleBrackets(@"use \<div> here"), Is.EqualTo("use &lt;div> here"));
        }

        [Test]
        public void EscapedAngleBeforeNonLetterIsLiteral()
        {
            Assert.That(MarkdownCleanup.UnescapeAngleBrackets(@"a \< b"), Is.EqualTo("a < b"));
        }

        [Test]
        public void LeadingEscapedGreaterThanIsKeptEscaped()
        {
            // Unescaping it would turn the line into a Markdown quote block
            Assert.That(MarkdownCleanup.UnescapeAngleBrackets(@"\> not a quote"), Is.EqualTo(@"\> not a quote"));
        }

        [Test]
        public void LeadingEscapedGreaterThanAfterListMarkerIsKept()
        {
            Assert.That(MarkdownCleanup.UnescapeAngleBrackets(@"- \> item"), Is.EqualTo(@"- \> item"));
        }

        [Test]
        public void MidLineEscapedGreaterThanIsUnescaped()
        {
            Assert.That(MarkdownCleanup.UnescapeAngleBrackets(@"a \> b"), Is.EqualTo("a > b"));
        }

        // ---- StripNoiseSpans ----

        [Test]
        public void DefaultStyleSpanIsUnwrapped()
        {
            Assert.That(MarkdownCleanup.StripNoiseSpans("x <span style='color:black'>kept</span> y"), Is.EqualTo("x kept y"));
        }

        [Test]
        public void EmptySpanIsRemoved()
        {
            Assert.That(MarkdownCleanup.StripNoiseSpans("a<span style='color:red'></span>b"), Is.EqualTo("ab"));
        }

        [Test]
        public void RealStylingSpanIsKept()
        {
            const string s = "x <span style='color:red'>red</span> y";
            Assert.That(MarkdownCleanup.StripNoiseSpans(s), Is.EqualTo(s));
        }

        [Test]
        public void LineWithoutSpansIsUnchanged()
        {
            Assert.That(MarkdownCleanup.StripNoiseSpans("plain text"), Is.EqualTo("plain text"));
        }

        // ---- RenumberOrderedLists ----

        [Test]
        public void AllOnesAreRenumbered()
        {
            var inp = new[] { "1. a", "1. b", "1. c" };
            Assert.That(MarkdownCleanup.RenumberOrderedLists(inp), Is.EqualTo(new[] { "1. a", "2. b", "3. c" }));
        }

        [Test]
        public void BlankLinesBetweenItemsAreTolerated()
        {
            var inp = new[] { "1. a", "", "1. b" };
            Assert.That(MarkdownCleanup.RenumberOrderedLists(inp), Is.EqualTo(new[] { "1. a", "", "2. b" }));
        }

        [Test]
        public void AlreadyNumberedListIsLeftAlone()
        {
            var inp = new[] { "1. a", "2. b", "3. c" };
            Assert.That(MarkdownCleanup.RenumberOrderedLists(inp), Is.EqualTo(inp));
        }

        [Test]
        public void SingleItemIsNotTouched()
        {
            var inp = new[] { "1. only" };
            Assert.That(MarkdownCleanup.RenumberOrderedLists(inp), Is.EqualTo(inp));
        }

        [Test]
        public void DifferentIndentIsASeparateList()
        {
            // The indented pair is its own "1. 1." list and is renumbered; the outer single item is left
            var inp = new[] { "1. top", "    1. sub", "    1. sub2" };
            Assert.That(MarkdownCleanup.RenumberOrderedLists(inp), Is.EqualTo(new[] { "1. top", "    1. sub", "    2. sub2" }));
        }

        // ---- CleanupMarkdown (integration) ----

        [Test]
        public void FencedCodeBlockIsNotTouched()
        {
            var md = "```\n1. a\n1. b\n\\<div>\n```";
            Assert.That(MarkdownCleanup.CleanupMarkdown(md), Is.EqualTo(md));
        }

        [Test]
        public void CleanupAppliesOutsideFence()
        {
            var md = "1. a\n1. b";
            Assert.That(MarkdownCleanup.CleanupMarkdown(md), Is.EqualTo("1. a\n2. b"));
        }

        [Test]
        public void CrlfIsPreserved()
        {
            var md = "a\r\nb";
            Assert.That(MarkdownCleanup.CleanupMarkdown(md), Does.Contain("\r\n"));
        }
    }
}
