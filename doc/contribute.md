# Add a new Export setting

- Add the setting in [AppSettings.cs](/src/OneNoteMdExporter/Infrastructure/AppSettings.cs) with a description
- Add the setting in [appSettings.json](/src/OneNoteMdExporter/appSettings.json) with a description
- Use your settings in ExportServiceBase.cs / JoplinExportService or MdExportService using the `AppSettings` class

# Add a new langage to translations

You can contribute by adding translation in your language. You just need to :
* fork the repo
* copy file `/src/Resources/trad.en.json`, translate it, and save the new file into `trad.<TwoLetterLanguageCode>.json`  (list of language code [here](https://en.wikipedia.org/wiki/List_of_ISO_3166_country_codes)).
* send a [pull-request](https://docs.github.com/en/github/collaborating-with-issues-and-pull-requests/creating-a-pull-request) 

# Add a new Export format

## Getting started

[ExportServiceBase.cs](/src/OneNoteMdExporter/Services/Export/ExportServiceBase.cs) is an Abstract class that implements the shared logic for exporting a Notebook.

## Implement a new export service

You needs to inherit from [ExportServiceBase.cs](/src/OneNoteMdExporter/Services/Export/ExportServiceBase.cs) and implement the abstract methods. You can take inspiration from [JoplinExportService.cs](/src/OneNoteMdExporter/Services/Export/JoplinExportService.cs) and [MdExportService.cs](/src/OneNoteMdExporter/Services/Export/MdExportService.cs).

## Integrate the new export service

- Update [ExportServiceFactory.cs](/src/OneNoteMdExporter/Infrastructure/ExportServiceFactory.cs) to register your new exporter