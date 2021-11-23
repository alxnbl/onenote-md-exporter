# Getting started

[ExportServiceBase.cs](/src/OneNoteMdExporter/Services/Export/ExportServiceBase.cs) is an Abstract class that implements the shared logic for exporting a Notebook.

# Implement a new export service

You needs to inherit from [ExportServiceBase.cs](/src/OneNoteMdExporter/Services/Export/ExportServiceBase.cs) and implement the abstract methods. You can take inspiration from [JoplinExportService.cs](/src/OneNoteMdExporter/Services/Export/JoplinExportService.cs) and [MdExportService.cs](/src/OneNoteMdExporter/Services/Export/MdExportService.cs).

# Integrate the new export service

- Update [ExportServiceFactory.cs](/src/OneNoteMdExporter/Infrastructure/ExportServiceFactory.cs) to register your new exporter