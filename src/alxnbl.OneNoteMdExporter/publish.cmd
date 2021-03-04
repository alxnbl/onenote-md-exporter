set version=%1

REM DotNet build do not support COM REFERENCES : https://github.com/dotnet/msbuild/issues/3986
REM dotnet publish --configuration Release --self-contained true -r win7-x64 /p:Version="%version%"

"C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe" alxnbl.OneNoteMdExporter.csproj -property:Configuration=Release  -property:Version="%version%" -restore 

tar.exe -a -c -f "OneNoteMdExporter.v%version%".zip -C bin\Release\netcoreapp3.1\win7-x86\ *