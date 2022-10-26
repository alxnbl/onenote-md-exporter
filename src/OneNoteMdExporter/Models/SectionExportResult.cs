using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace alxnbl.OneNoteMdExporter.Models
{
    public class SectionExportResult
    {
        /// <summary>
        /// Number of pages that fail to be exported
        /// </summary>
        public int PagesOnError { get; set; } = 0;
    }
}
