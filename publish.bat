@ECHO OFF

if exist dist (
  rd /S /Q dist
)

:: Build first so we can then get the version from the x64 dll
ECHO Building...
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" /p:Configuration=Release /p:Platform=x86 /t:Build /restore > nul
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" /p:Configuration=Release /p:Platform=x64 /t:Build /restore > nul

:: Get the version from the x64 dll
ECHO Getting version...
for /f "usebackq delims=" %%G in (`powershell -Command "[System.Reflection.Assembly]::LoadFrom('%~dp0\x64\Release\WebNowPlaying.dll').GetName().Version.ToString()"`) do (
  set "version=%%~G"
)
:: 1.2.3.0 -> 1.2.3
set "version=%version:~0,-2%"

:: Move files to /dist
mkdir dist
xcopy /Y /I x64\Release\WebNowPlaying.dll dist\WebNowPlaying-x64.dll* > nul
xcopy /Y /I x86\Release\WebNowPlaying.dll dist\WebNowPlaying-x86.dll* > nul

:: Package with MonD
ECHO Packaging...
mond package -Skin WebNowPlayingRedux -Author "keifufu, tjhrulz" -PackageVersion %version% -MinimumRainmeter 4.5.0 -MinimumWindows 10.0 -OutPath %~dp0\dist\WebNowPlayingRedux_%version%.rmskin > nul