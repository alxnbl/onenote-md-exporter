using alxnbl.OneNoteMdExporter.IntTests.Helpers;
using NUnit.Framework;
using System.IO;
using System.Text.RegularExpressions;

namespace alxnbl.OneNoteMdExporter.IntTests
{
    public class MarkdownExportTests : ExportTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public override void Image_SimpleImage()
        {
            var res = TestHelper.RunExporter("1", "TestBloc", "Image", "Simple image");


            // http://regexstorm.net/tester
            string regexImgAttributes = "!\\[.*\\]\\((?<path>\\.\\./_resources/)(?<filename>.*)\\)";

            MatchCollection matchs = Regex.Matches(res.exportResult, regexImgAttributes, RegexOptions.IgnoreCase);

            var folderPath = matchs[0].Groups["path"].Value;
            var fileName = matchs[0].Groups["filename"].Value;

            Assert.AreEqual("../_resources/", folderPath);
            Assert.IsTrue(File.Exists(Path.Combine("TestBloc", "Image", folderPath + fileName)));
        }
    }
}