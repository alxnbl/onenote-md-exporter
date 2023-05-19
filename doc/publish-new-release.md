# Publish a new release

1. Update `Package version` of `OneNoteMdExporter` project to the desired software version.
3. Ensure PanDoc archive has beed unzipped in folder `src\OneNoteMdExporter\pandoc`
3. From Visual Studio, use Publish profile of `OneNoteMdExporter` project to build the tool binaries
4. Open output folder (`bin\Release\net5.0\win7-x86\publish`) and Zip the content of the folder and name it `OneNoteMdExporter.vX.Y.Z.zip`
