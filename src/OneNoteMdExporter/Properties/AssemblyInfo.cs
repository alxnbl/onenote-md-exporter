using System.Runtime.CompilerServices;

// Expose internal members (AttachmentReferenceHelper, ConverterService helpers) to the unit test project
// so the COM-free string logic can be tested directly without reflection.
[assembly: InternalsVisibleTo("OneNoteMdExporter.Unit.Tests")]
