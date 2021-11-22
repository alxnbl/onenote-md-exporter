using System;
using System.IO;

namespace alxnbl.OneNoteMdExporter.Helpers
{
    public static class PathExtensions
    {
        public static bool PathEquals(string path1, string path2)
        {
            return String.Compare(
                Path.GetFullPath(path1).TrimEnd('\\'),
                Path.GetFullPath(path2).TrimEnd('\\'),
                StringComparison.InvariantCultureIgnoreCase) == 0;
        }
    }
}
