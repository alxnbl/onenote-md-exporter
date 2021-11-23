using alxnbl.OneNoteMdExporter.Infrastructure;
using alxnbl.OneNoteMdExporter.Models;
using Microsoft.Office.Interop.OneNote;
using System;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.Converters;
using YamlDotNet.Serialization.NamingConventions;

namespace alxnbl.OneNoteMdExporter.Services.Export
{
    public class MdFrontMatterExportService : MdExportService
    {
        public MdFrontMatterExportService(AppSettings appSettings, Application oneNoteApp, ConverterService converterService) : base(appSettings, oneNoteApp, converterService)
        {
            _exportFormatCode = "md-frontmatter";
        }

        protected override void WritePageMdFile(Page page, string pageMd)
        {
            var headerModel = new FrontMatterHeader
            {
                Title = page.Title,
                Created = page.CreationDate,
                Updated = page.LastModificationDate
            };

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            var headerYaml = serializer.Serialize(headerModel);

            var pageMdWithHeader = "---\n" + headerYaml + "---\n\n" + pageMd;
            base.WritePageMdFile(page, pageMdWithHeader);
        }


        private class FrontMatterHeader
        {
            public string Title { get; set; }
            public DateTime Updated { get; set; }
            public DateTime Created { get; set; }
        }
    }
}
