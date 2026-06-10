@echo off
REM Build script for 1wireHID
REM Requires .NET 8 SDK installed

echo Building 1wireHID...
dotnet build 1wireHID.csproj -c Release -r win-x64 --self-contained
if errorlevel 1 goto :fail

echo.
echo Build successful!
echo Output in: bin\Release\net8.0-windows\win-x64\publish\
echo.
goto :end

:fail
echo Build FAILED!
exit /b 1

:end
pause