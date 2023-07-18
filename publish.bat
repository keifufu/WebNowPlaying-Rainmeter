@ECHO OFF

:: === publish.bat ===
:: Builds x86 and x64, copies them to rainmeter Vault (that's in .csproj),
:: copies plugins to /dist and packages the example skin and plugins with mond.
:: Usage: publish.bat
:: =====================
:: Changelog
:: 2023-07-18: Initial version

if exist dist (
  rd /S /Q dist
)
ECHO Getting version...
for /f "usebackq delims=" %%G in (`powershell -Command "[System.Reflection.Assembly]::LoadFrom('%~dp0\x64\Release\WebNowPlaying.dll').GetName().Version.ToString()"`) do (
  set "version=%%~G"
)
:: 1.2.3.0 -> 1.2.3
set "version=%version:~0,-2%"
ECHO Building...
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" /p:Configuration=Release /p:Platform=x86 /t:Build /restore > nul
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" /p:Configuration=Release /p:Platform=x64 /t:Build /restore > nul
mkdir dist
xcopy /Y /I x64\Release\WebNowPlaying.dll dist\WebNowPlaying-x64.dll* > nul
xcopy /Y /I x86\Release\WebNowPlaying.dll dist\WebNowPlaying-x86.dll* > nul
ECHO Packaging...
mond package -Skin WebNowPlayingRedux -Author "keifufu, tjhrulz" -PackageVersion %version% -MinimumRainmeter 4.5.0 -MinimumWindows 10.0 -OutPath %~dp0\dist\WebNowPlayingRedux_%version%.rmskin > nul