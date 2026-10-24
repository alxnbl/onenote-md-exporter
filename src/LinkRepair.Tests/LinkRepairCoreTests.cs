using System.Collections.Generic;
using alxnbl.OneNoteMdExporter.Services.Export;
using NUnit.Framework;

namespace alxnbl.OneNoteMdExporter.Tests
{
    public class LinkRepairCoreTests
    {
        private static Dictionary<string, string> Map(params (string key, string path)[] entries)
        {
            var d = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var (k, p) in entries) d[k] = p;
            return d;
        }

        [Test]
        public void KnownPlaceholderBecomesRelativeLink()
        {
            var map = Map(("page:abc123", @"C:\root\sub\target.md"));
            var text = "see [the page](onenote-page:ABC123 \"OneNote page link\") now";
            var outp = LinkRepairCore.ResolveText(text, map, @"C:\root", out var r, out var u);
            Assert.Multiple(() =>
            {
                Assert.That(outp, Is.EqualTo("see [the page](sub/target.md) now"));
                Assert.That(r, Is.EqualTo(1));
                Assert.That(u, Is.EqualTo(0));
            });
        }

        [Test]
        public void UnknownPlaceholderGetsANote()
        {
            var text = "[x](onenote-page:deadbeef)";
            var outp = LinkRepairCore.ResolveText(text, Map(), @"C:\root", out var r, out var u);
            Assert.Multiple(() =>
            {
                Assert.That(outp, Does.Contain("not in this archive"));
                Assert.That(r, Is.EqualTo(0));
                Assert.That(u, Is.EqualTo(1));
            });
        }

        [Test]
        public void IdMatchIsCaseInsensitive()
        {
            var map = Map(("page:abc", @"C:\root\a.md"));
            var outp = LinkRepairCore.ResolveText("[x](onenote-page:ABC)", map, @"C:\root", out var r, out _);
            Assert.That(r, Is.EqualTo(1));
            Assert.That(outp, Is.EqualTo("[x](a.md)"));
        }

        [Test]
        public void SpacesAndParensInPathArePercentEncoded()
        {
            var map = Map(("page:abc", @"C:\root\My Folder (1)\page.md"));
            var outp = LinkRepairCore.ResolveText("[x](onenote-page:abc)", map, @"C:\root", out _, out _);
            Assert.That(outp, Is.EqualTo("[x](My%20Folder%20%281%29/page.md)"));
        }

        [Test]
        public void TextWithoutPlaceholdersIsUnchanged()
        {
            var text = "plain [link](a.md) and text";
            var outp = LinkRepairCore.ResolveText(text, Map(), @"C:\root", out var r, out var u);
            Assert.Multiple(() =>
            {
                Assert.That(outp, Is.EqualTo(text));
                Assert.That(r, Is.EqualTo(0));
                Assert.That(u, Is.EqualTo(0));
            });
        }

        [Test]
        public void MultiplePlaceholdersInOneText()
        {
            var map = Map(("page:a", @"C:\root\a.md"), ("page:b", @"C:\root\x\b.md"));
            var outp = LinkRepairCore.ResolveText("[1](onenote-page:a) and [2](onenote-page:b)", map, @"C:\root", out var r, out _);
            Assert.That(r, Is.EqualTo(2));
            Assert.That(outp, Is.EqualTo("[1](a.md) and [2](x/b.md)"));
        }
    }
}
