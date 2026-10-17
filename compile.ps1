Stop-Process -Name native-browser -ErrorAction SilentlyContinue
dotnet build native-browser/native-browser.csproj --nologo -v minimal
Start-Process native-browser/bin/Debug/net10.0-windows/native-browser.exe