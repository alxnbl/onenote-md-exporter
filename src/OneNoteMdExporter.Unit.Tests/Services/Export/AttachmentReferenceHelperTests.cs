using alxnbl.OneNoteMdExporter.Models;
using alxnbl.OneNoteMdExporter.Services.Export;
using NUnit.Framework;

namespace OneNoteMdExporter.Unit.Tests.Services.Export
{
    /// <summary>
    /// Unit tests for the COM-free attachment reference logic (Obsidian-safe markdown links + file-name
    /// normalization). Uses only synthetic file names (no PII / no real attachment names).
    /// </summary>
    [TestFixture]
    public class AttachmentReferenceHelperTests
    {
        private static Attachement FileAttachment(string preferredFileName)
        {
            var notebook = new Notebook();
            var section = new Section(notebook) { Title = "TestAbschnitt" };
            var page = new Page(section) { Title = "TestSeite" };
            return new Attachement(page)
            {
                Type = AttachementType.File,
                OneNotePreferredFileName = preferredFileName
            };
        }

        // Runs the placeholder replacement with a fixed markdown reference and returns the resulting page text.
        private static string Insert(string pageMd, Attachement attach, string mdRef)
        {
            AttachmentReferenceHelper.InsertPageMdAttachmentReference(ref pageMd, attach, _ => mdRef);
            return pageMd;
        }

        // The <<name>> placeholder as PanDoc emits it in markdown: each angle bracket is backslash-escaped.
        private static string Placeholder(string escapedName) => @"\<\<" + escapedName + @"\>\>";

        [Test]
        public void Insert_SimpleName_WrapsTargetInAngleBracketsAndKeepsLabel()
        {
            var attach = FileAttachment("Rechnung 2024.pdf");
            var result = Insert(Placeholder("Rechnung 2024.pdf"), attach, "resources/Rechnung 2024.pdf");
            Assert.That(result, Is.EqualTo("[Rechnung 2024.pdf](<resources/Rechnung 2024.pdf>)"));
        }

        [Test]
        public void Insert_NameWithPlusAndComma_StaysLiteralInsideAngleBrackets()
        {
            var attach = FileAttachment("R+V-Vertrag, Muster.pdf");
            var result = Insert(Placeholder("R+V-Vertrag, Muster.pdf"), attach, "resources/R+V-Vertrag, Muster.pdf");
            Assert.That(result, Is.EqualTo("[R+V-Vertrag, Muster.pdf](<resources/R+V-Vertrag, Muster.pdf>)"));
        }

        [Test]
        public void Insert_NameWithCommaPlusAndSquareBracket_LabelRoundBracketsTargetKeepsAllCharsLiteral()
        {
            // The core-fix combination: comma + plus + square bracket in the same name, exercising label
            // and target transformation at the same time.
            var attach = FileAttachment("R+V, Vertrag[2024].pdf");
            var result = Insert(Placeholder(@"R+V, Vertrag\[2024\].pdf"), attach, "resources/R+V, Vertrag[2024].pdf");
            Assert.That(result, Is.EqualTo("[R+V, Vertrag(2024).pdf](<resources/R+V, Vertrag[2024].pdf>)"));
        }

        [Test]
        public void Insert_NameWithSquareBrackets_LabelUsesRoundBracketsTargetKeepsSquare()
        {
            var attach = FileAttachment("Anhang[1].pdf");
            // PanDoc escapes '[' and ']' in the placeholder, the unescape step must restore them before comparing.
            var result = Insert(Placeholder(@"Anhang\[1\].pdf"), attach, "resources/Anhang[1].pdf");
            Assert.That(result, Is.EqualTo("[Anhang(1).pdf](<resources/Anhang[1].pdf>)"));
        }

        [Test]
        public void Insert_PanDocEscapedUnderscore_MatchesAfterUnescape()
        {
            var attach = FileAttachment("Datei_2.pdf");
            var result = Insert(Placeholder(@"Datei\_2.pdf"), attach, "resources/Datei_2.pdf");
            Assert.That(result, Is.EqualTo("[Datei_2.pdf](<resources/Datei_2.pdf>)"));
        }

        [Test]
        public void Insert_TwoIdenticalPlaceholders_ReplacesOnlyTheFirst()
        {
            var attach = FileAttachment("Beleg.pdf");
            var pageMd = Placeholder("Beleg.pdf") + Placeholder("Beleg.pdf");
            var result = Insert(pageMd, attach, "resources/Beleg.pdf");
            Assert.That(result, Is.EqualTo("[Beleg.pdf](<resources/Beleg.pdf>)" + Placeholder("Beleg.pdf")));
        }

        [Test]
        public void Insert_TwoDifferentPlaceholders_ReplacesOnlyMatchingOneAndLeavesOtherUntouched()
        {
            // Two DIFFERENT attachments on one page: only the placeholder matching the current attachment
            // is replaced; the other one is left for a later call.
            var attach = FileAttachment("Rechnung.pdf");
            var pageMd = Placeholder("Rechnung.pdf") + Placeholder("Vertrag.pdf");
            var result = Insert(pageMd, attach, "resources/Rechnung.pdf");
            Assert.That(result, Is.EqualTo("[Rechnung.pdf](<resources/Rechnung.pdf>)" + Placeholder("Vertrag.pdf")));
        }

        [Test]
        public void Insert_NonMatchingPlaceholder_LeftUntouched()
        {
            var attach = FileAttachment("Rechnung.pdf");
            var pageMd = Placeholder("Anderes.pdf");
            var result = Insert(pageMd, attach, "resources/Rechnung.pdf");
            Assert.That(result, Is.EqualTo(pageMd));
        }

        [Test]
        public void Insert_NameWithUmlaut_StaysLiteral()
        {
            var name = (char)0x00DC + "bergabe.pdf"; // "Übergabe.pdf" (U+00DC = capital U-umlaut)
            var attach = FileAttachment(name);
            var result = Insert(Placeholder(name), attach, "resources/" + name);
            Assert.That(result, Is.EqualTo("[" + name + "](<resources/" + name + ">)"));
        }

        [Test]
        public void Insert_EmptyFileName_ProducesEmptyLabelWithAngleBracketTarget()
        {
            var attach = FileAttachment("");
            var result = Insert(Placeholder(""), attach, "resources/leer");
            Assert.That(result, Is.EqualTo("[](<resources/leer>)"));
        }

        [Test]
        public void Insert_PageContentWithoutPlaceholder_LeftUntouched()
        {
            var attach = FileAttachment("Rechnung.pdf");
            var pageMd = "Text ohne jeden Anhang-Platzhalter.";
            var result = Insert(pageMd, attach, "resources/Rechnung.pdf");
            Assert.That(result, Is.EqualTo(pageMd));
        }

        // ---- RemoveInvisibleChars ----

        [Test]
        public void RemoveInvisibleChars_NoBreakSpace_BecomesNormalSpace()
        {
            var input = "a" + (char)0x00A0 + "b";
            Assert.That(AttachmentReferenceHelper.RemoveInvisibleChars(input), Is.EqualTo("a b"));
        }

        [Test]
        public void RemoveInvisibleChars_NarrowNoBreakSpace_BecomesNormalSpace()
        {
            var input = "a" + (char)0x202F + "b";
            Assert.That(AttachmentReferenceHelper.RemoveInvisibleChars(input), Is.EqualTo("a b"));
        }

        [Test]
        public void RemoveInvisibleChars_FigureSpace_BecomesNormalSpace()
        {
            var input = "a" + (char)0x2007 + "b";
            Assert.That(AttachmentReferenceHelper.RemoveInvisibleChars(input), Is.EqualTo("a b"));
        }

        [Test]
        public void RemoveInvisibleChars_SoftHyphen_IsStripped()
        {
            var input = "a" + (char)0x00AD + "b";
            Assert.That(AttachmentReferenceHelper.RemoveInvisibleChars(input), Is.EqualTo("ab"));
        }

        [Test]
        public void RemoveInvisibleChars_ZeroWidthSpace_IsStripped()
        {
            var input = "a" + (char)0x200B + "b";
            Assert.That(AttachmentReferenceHelper.RemoveInvisibleChars(input), Is.EqualTo("ab"));
        }

        [Test]
        public void RemoveInvisibleChars_ByteOrderMark_IsStripped()
        {
            var input = "a" + (char)0xFEFF + "b";
            Assert.That(AttachmentReferenceHelper.RemoveInvisibleChars(input), Is.EqualTo("ab"));
        }

        [Test]
        public void RemoveInvisibleChars_PlainName_Unchanged()
        {
            Assert.That(AttachmentReferenceHelper.RemoveInvisibleChars("Rechnung 2024.pdf"), Is.EqualTo("Rechnung 2024.pdf"));
        }

        [Test]
        public void RemoveInvisibleChars_Null_ReturnsNull()
        {
            Assert.That(AttachmentReferenceHelper.RemoveInvisibleChars(null), Is.Null);
        }

        [Test]
        public void RemoveInvisibleChars_EmptyString_ReturnsEmptyString()
        {
            Assert.That(AttachmentReferenceHelper.RemoveInvisibleChars(""), Is.Empty);
        }

        [Test]
        public void RemoveInvisibleChars_NoBreakSpaceAndSoftHyphenCombined_BothNormalized()
        {
            var input = "a" + (char)0x00A0 + "b" + (char)0x00AD + "c";
            Assert.That(AttachmentReferenceHelper.RemoveInvisibleChars(input), Is.EqualTo("a bc"));
        }
    }
}
