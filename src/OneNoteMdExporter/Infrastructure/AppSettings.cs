using Microsoft.Extensions.Configuration;

namespace alxnbl.OneNoteMdExporter.Infrastructure
{
    public class AppSettings
    {
        /// <summary>
        /// Add at the begining of each page a FrontMatter header including Title and other metadata of the page
        /// </summary>
        public bool AddFrontMatterHeader { get; set; } = true;

        /// <summary>
        /// Enable verbose mode and keep temporary files (.docx)
        /// </summary>
        public bool Debug { get; set; } = false;


        public bool PostProcessingMdImgRef { get; set; } = true;

        public bool PostProcessingRemoveQuotationBlocks { get; set; } = true;

        public bool PostProcessingRemoveOneNoteHeader { get; set; } = true;

        public bool RemoveConsecutiveLinebreaks { get; set; } = true;
        
        public bool UserTempFolder { get; set; } = true;

        public bool RemoveCarriageReturn { get; set; } = true;

        /// <summary>
        /// Maximum file size of md file. Reduce this value to avoid error of max file system file path
        /// </summary>
        public int MdMaxFileLength { get; set; } = 50;

        /// <summary>
        /// One of pandoc format https://pandoc.org/MANUAL.html#general-options
        /// </summary>
        public string PanDocMarkdownFormat { get; set; } = "gfm";

        public static AppSettings LoadAppSettings()
        {
            IConfigurationRoot configRoot = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();
            AppSettings appSettings = configRoot.Get<AppSettings>();
            return appSettings;
        }
    }
}
