using alxnbl.OneNoteMdExporter.Services;
using NUnit.Framework;
using System;

namespace OneNoteMdExporter.Unit.Tests.Services
{
    /// <summary>
    /// Unit tests for the pure markdown post-processing transforms of ConverterService (COM-free).
    /// Each transform is tested directly (internal), which avoids the static AppSettings coupling of
    /// the public PageMdPostConversion entry point.
    /// </summary>
    [TestFixture]
    public class ConverterServiceTests
    {
        [Test]
        public void DeduplicateLinebreaks_DoubleLinebreak_CollapsesToOne()
        {
            var result = ConverterService.DeduplicateLinebreaks("a\n\nb");
            Assert.That(result, Is.EqualTo("a" + Environment.NewLine + "b"));
        }

        [Test]
        public void DeduplicateLinebreaks_SingleLinebreak_Unchanged()
        {
            var input = "a\nb";
            var result = ConverterService.DeduplicateLinebreaks(input);
            Assert.That(result, Is.EqualTo(input));
        }

        [Test]
        public void DeduplicateLinebreaks_BlockquoteMarkedLinebreak_CollapsesToOne()
        {
            // PanDoc special case: a blockquote-marked linebreak "\n>\n" must also collapse to one linebreak.
            var result = ConverterService.DeduplicateLinebreaks("a\n>\nb");
            Assert.That(result, Is.EqualTo("a" + Environment.NewLine + "b"));
        }

        [Test]
        public void MaxTwoLineBreaksInARow_FourLinebreaks_ReducedToTwo()
        {
            var result = ConverterService.MaxTwoLineBreaksInARow("a\n\n\n\nb");
            Assert.That(result, Is.EqualTo("a" + Environment.NewLine + Environment.NewLine + "b"));
        }

        [Test]
        public void MaxTwoLineBreaksInARow_ExactlyTwoLinebreaks_Unchanged()
        {
            var input = "a\n\nb";
            var result = ConverterService.MaxTwoLineBreaksInARow(input);
            Assert.That(result, Is.EqualTo("a" + Environment.NewLine + Environment.NewLine + "b"));
        }

        [Test]
        public void RemoveHtmlCommentBlocks_EmptyComment_IsRemoved()
        {
            var result = ConverterService.RemoveHtmlCommentBlocks("a\n<!-- -->");
            Assert.That(result, Is.EqualTo("a"));
        }

        [Test]
        public void RemoveHtmlCommentBlocks_CommentWithText_IsNotRemoved()
        {
            // The regex only matches an empty comment block; a comment carrying text must survive.
            var input = "a\n<!-- Hinweis -->";
            var result = ConverterService.RemoveHtmlCommentBlocks(input);
            Assert.That(result, Is.EqualTo(input));
        }

        [Test]
        public void RemoveUTF8NonBreakingSpace_NoBreakSpace_IsStripped()
        {
            var input = "a" + (char)0x00A0 + "b";
            var result = ConverterService.RemoveUTF8NonBreakingSpace(input);
            Assert.That(result, Is.EqualTo("ab"));
        }

        [Test]
        public void RemoveUTF8NonBreakingSpace_PlainText_Unchanged()
        {
            var input = "ab";
            var result = ConverterService.RemoveUTF8NonBreakingSpace(input);
            Assert.That(result, Is.EqualTo(input));
        }

        [Test]
        public void InsertMdHighlight_MarkSpan_BecomesDoubleEquals()
        {
            var result = ConverterService.InsertMdHighlight("<span class=\"mark\">Wichtig</span>");
            Assert.That(result, Is.EqualTo("==Wichtig=="));
        }

        [Test]
        public void InsertMdHighlight_NoMarkSpan_Unchanged()
        {
            var input = "kein <span>markup</span> hier";
            Assert.That(ConverterService.InsertMdHighlight(input), Is.EqualTo(input));
        }

        [Test]
        public void InsertMdHighlight_EmptyMarkSpan_BecomesEmptyDoubleEquals()
        {
            var result = ConverterService.InsertMdHighlight("<span class=\"mark\"></span>");
            Assert.That(result, Is.EqualTo("===="));
        }

        [Test]
        public void InsertMdHighlight_TwoMarkSpans_BothReplaced()
        {
            var result = ConverterService.InsertMdHighlight(
                "<span class=\"mark\">Eins</span> und <span class=\"mark\">Zwei</span>");
            Assert.That(result, Is.EqualTo("==Eins== und ==Zwei=="));
        }

        [Test]
        public void InsertMdTodoMarks_OpenBox_BecomesUncheckedTask()
        {
            var openBox = (char)0x00AB + ((char)0x2610).ToString() + (char)0x00BB; // «☐»
            var result = ConverterService.InsertMdTodoMarks(openBox + "Aufgabe");
            Assert.That(result, Is.EqualTo("- [ ] Aufgabe"));
        }

        [Test]
        public void InsertMdTodoMarks_CheckedBox_BecomesCheckedTask()
        {
            var checkedBox = (char)0x00AB + ((char)0x2611).ToString() + (char)0x00BB; // «☑»
            var result = ConverterService.InsertMdTodoMarks(checkedBox + "Erledigt");
            Assert.That(result, Is.EqualTo("- [x] Erledigt"));
        }

        [Test]
        public void InsertMdTodoMarks_PlainText_Unchanged()
        {
            Assert.That(ConverterService.InsertMdTodoMarks("nur Text"), Is.EqualTo("nur Text"));
        }

        [Test]
        public void InsertMdTodoMarks_MixedOpenAndCheckedBoxes_BothReplaced()
        {
            var openBox = (char)0x00AB + ((char)0x2610).ToString() + (char)0x00BB;
            var checkedBox = (char)0x00AB + ((char)0x2611).ToString() + (char)0x00BB;
            var result = ConverterService.InsertMdTodoMarks(openBox + "Offen" + "\n" + checkedBox + "Erledigt");
            Assert.That(result, Is.EqualTo("- [ ] Offen" + "\n" + "- [x] Erledigt"));
        }

        [Test]
        public void RemoveQuotationBlocks_EmptyQuoteLine_IsRemoved()
        {
            // Regex alternation order (\n|\r|\r\n) matches the bare "\r" before "\r\n" once the leading
            // "\r" already satisfied the first alternative, so the trailing "\n" of the second linebreak
            // survives as a plain LF alongside the inserted Environment.NewLine. Documents existing behavior.
            var result = ConverterService.RemoveQuotationBlocks("a" + Environment.NewLine + ">" + Environment.NewLine + "b");
            Assert.That(result, Is.EqualTo("a" + Environment.NewLine + "\n" + "b"));
        }

        [Test]
        public void RemoveQuotationBlocks_QuotedLineWithText_MarkerIsStrippedTextKept()
        {
            var result = ConverterService.RemoveQuotationBlocks("a" + Environment.NewLine + "> Zitat");
            Assert.That(result, Is.EqualTo("a" + Environment.NewLine + "Zitat"));
        }

        [Test]
        public void RemoveQuotationBlocks_NoQuoteMarker_Unchanged()
        {
            var input = "a" + Environment.NewLine + "b";
            var result = ConverterService.RemoveQuotationBlocks(input);
            Assert.That(result, Is.EqualTo(input));
        }

        [Test]
        public void RemoveOneNoteHeader_TitleAuthorTimeHeader_IsRemoved()
        {
            var input = "Seitentitel" + Environment.NewLine + "Max Muster" + Environment.NewLine + "12:34 Inhalt der Seite";
            var result = ConverterService.RemoveOneNoteHeader(input);
            Assert.That(result, Is.EqualTo("Inhalt der Seite"));
        }

        [Test]
        public void RemoveOneNoteHeader_NoTimePattern_Unchanged()
        {
            var input = "Seitentitel" + Environment.NewLine + "Zeile ohne Uhrzeit" + Environment.NewLine + "Inhalt";
            var result = ConverterService.RemoveOneNoteHeader(input);
            Assert.That(result, Is.EqualTo(input));
        }

        [Test]
        public void UnEscapeStylingSpan_EscapedSpanTag_IsUnescaped()
        {
            var result = ConverterService.UnEscapeStylingSpan(
                (char)0x00AB + "span style='color:#ff0000'" + (char)0x00BB + "Wichtig" + (char)0x00AB + "/span" + (char)0x00BB);
            Assert.That(result, Is.EqualTo("<span style='color:#ff0000'>Wichtig</span>"));
        }

        [Test]
        public void UnEscapeStylingSpan_NoEscapedSpan_Unchanged()
        {
            var input = "einfacher Text ohne Styling-Span";
            var result = ConverterService.UnEscapeStylingSpan(input);
            Assert.That(result, Is.EqualTo(input));
        }
    }
}
