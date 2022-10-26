using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace alxnbl.OneNoteMdExporter.Models
{
    public class NotebookExportResult
    {
        /// <summary>
        /// Contains the errorCode of the error that cause a crash during the export. Null if export ended correctly.
        /// </summary>
        public string NoteBookExportErrorCode { get; set; }

        /// <summary>
        /// Contains the error message the an error that cause a crash during the export. Null if export ended correctly.
        /// </summary>
        public string NoteBookExportErrorMessage { get; set; }

        /// <summary>
        /// Number of pages that fail to be exported
        /// </summary>
        public int PagesOnError { get; set; } = 0;
    }
}
