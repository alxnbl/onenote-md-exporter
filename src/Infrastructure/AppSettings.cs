using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace alxnbl.OneNoteMdExporter.Infrastructure
{
    public class AppSettings
    {
        public bool Debug { get; internal set; } = false;

        public bool PostProcessingMdImgRef { get; internal set; } = true;

        public bool PostProcessingRemoveQuotationBlocks { get; internal set; } = true;

        public bool PostProcessingRemoveOneNoteHeader { get; internal set; } = true;

        public bool RemoveConsecutiveLinebreaks { get; internal set; } = true;

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
