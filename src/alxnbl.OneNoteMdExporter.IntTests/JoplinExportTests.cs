using alxnbl.OneNoteMdExporter.IntTests.Helpers;
using NUnit.Framework;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace alxnbl.OneNoteMdExporter.IntTests
{
    public class JoplinExportTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Image_SimpleImage()
        {
            var res = TestHelper.RunExporter("2", "TestBloc", "Image", "Simple image");

            throw new NotImplementedException();
        }
    }
}