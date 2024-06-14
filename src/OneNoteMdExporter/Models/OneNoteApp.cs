using Microsoft.Office.Interop.OneNote;
using System.Runtime.InteropServices;

namespace alxnbl.OneNoteMdExporter.Models
{
    public static class OneNoteApp
    {
        public static Application Instance { get; private set; }

        public static void RenewInstance()
        {
            CleanUp();
            Instance = new();
        }

        // Releasing COM object to avoid ghost OneNote.exe
        public static void CleanUp()
        {
            if (Instance != null)
            {
                Marshal.ReleaseComObject(Instance);
                Instance = null;
            }
        }
    }
}
