using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace alxnbl.OneNoteMdExporter.Helpers
{
    public static class DirectoryHelper
    {
        public static void ClearFolder(string exportFolder)
        {
            if (Directory.Exists(exportFolder))
                Directory.Delete(exportFolder, true);
            Directory.CreateDirectory(exportFolder);
        }
    }
}
