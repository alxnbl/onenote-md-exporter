set version=%1

dotnet publish  --configuration Release --self-contained true -r win7-x64 /p:Version="%version%"
dotnet publish  --configuration Release --self-contained true -r win7-x86 /p:Version="%version%"

tar.exe -a -c -f "OneNoteMdExporter.v%version%"-x64.zip -C bin\Release\net5.0\win7-x64\publish\ *
tar.exe -a -c -f "OneNoteMdExporter.v%version%"-x86.zip -C bin\Release\net5.0\win7-x86\publish\ *