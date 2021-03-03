using alxnbl.OneNoteMdExporter.IntTests.Helpers;
using NUnit.Framework;
using System.IO;
using System.Text.RegularExpressions;

namespace alxnbl.OneNoteMdExporter.IntTests
{
    public class MarkdownExportTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Image_SimpleImage()
        {
            var res = TestHelper.RunExporter("1", "TestBloc", "Image", "Simple image");


            // http://regexstorm.net/tester
            string regexImgAttributes = "!\\[.*\\]\\((?<path>\\.\\./_resources/).*\\.png\\)";

            MatchCollection matchs = Regex.Matches(res.exportResult, regexImgAttributes, RegexOptions.IgnoreCase);

            Assert.AreEqual("../_resources/", matchs[0].Groups["path"].Value);

        }
    }
}