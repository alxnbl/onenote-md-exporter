set version=%1
set msbuild="C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MsBuild.exe"

%msbuild% alxnbl.OneNoteMdExporter\alxnbl.OneNoteMdExporter.csproj /restore /p:Configuration=Release /p:Version="%version%"

xcopy ..\pandoc\pandoc-2.13\COPYING.rtf alxnbl.OneNoteMdExporter\bin\Release\net5.0\win7-x86\pandoc\ /E /C /R /Y
xcopy ..\pandoc\pandoc-2.13\COPYRIGHT.txt alxnbl.OneNoteMdExporter\bin\Release\net5.0\win7-x86\pandoc\ /E /C /R /Y
xcopy ..\pandoc\pandoc-2.13\MANUAL.html alxnbl.OneNoteMdExporter\bin\Release\net5.0\win7-x86\pandoc\ /E /C /R /Y
xcopy ..\pandoc\pandoc-2.13\pandoc.exe alxnbl.OneNoteMdExporter\bin\Release\net5.0\win7-x86\ /E /C /R /Y
xcopy ..\LICENSE alxnbl.OneNoteMdExporter\bin\Release\net5.0\win7-x86\ /C /R /Y

tar.exe -a -c -f "OneNoteMdExporter.v%version%".zip -C alxnbl.OneNoteMdExporter\bin\Release\net5.0\win7-x86\ *