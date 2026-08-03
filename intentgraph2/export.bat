..\..\megadot-4.5.1-m.14-windows-x86_64-llvm-editor-csharp\MegaDot_v4.5.1-stable_mono_win64_console.exe --headless --export-pack "Windows Desktop" .\publish\intentgraph2.pck --verbose
copy .\intentgraph2.json .\publish\intentgraph2.json
dotnet msbuild .\intentgraph2core.csproj -t:DeployGodotFiles

cd ..\intentgraph2baselib
dotnet publish

cd ..\intentgraph2ritsulib
dotnet publish

cd ..\intentgraph2entry
dotnet publish
