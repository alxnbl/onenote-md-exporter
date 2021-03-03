using alxnbl.OneNoteMdExporter.IntTests.Helpers;
using NUnit.Framework;
using System.IO;

namespace alxnbl.OneNoteMdExporter.IntTests
{
    public class JoplinExportTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void ExecArguments_NotebookNameNotFound()
        {
            var res = TestHelper.RunExporter(" -n TestBloc");

            Assert.Pass();
        }
    }
}