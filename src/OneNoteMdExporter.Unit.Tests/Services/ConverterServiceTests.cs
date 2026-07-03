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
        public void MaxTwoLineBreaksInARow_FourLinebreaks_ReducedToTwo()
        {
            var result = ConverterService.MaxTwoLineBreaksInARow("a\n\n\n\nb");
            Assert.That(result, Is.EqualTo("a" + Environment.NewLine + Environment.NewLine + "b"));
        }

        [Test]
        public void RemoveHtmlCommentBlocks_EmptyComment_IsRemoved()
        {
            var result = ConverterService.RemoveHtmlCommentBlocks("a\n<!-- -->");
            Assert.That(result, Is.EqualTo("a"));
        }

        [Test]
        public void RemoveUTF8NonBreakingSpace_NoBreakSpace_IsStripped()
        {
            var input = "a" + (char)0x00A0 + "b";
            var result = ConverterService.RemoveUTF8NonBreakingSpace(input);
            Assert.That(result, Is.EqualTo("ab"));
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
    }
}
