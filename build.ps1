function Invoke-Environment {
  param
  (
    [Parameter(Mandatory=$true)]
    [string] $Command
  )

  $Command = "`"" + $Command + "`""
  cmd /c "$Command > nul 2>&1 && set" | . { process {
    if ($_ -match '^([^=]+)=(.*)') {
      [System.Environment]::SetEnvironmentVariable($matches[1], $matches[2])
    }
  }}
}

$origEnv = Get-ChildItem Env:*;
$vsInstallationPath = & 'vswhere.exe' -property installationPath

if (-not $vsInstallationPath) {
  Write-Host "No Visual Studio installation found."
  exit 1
}

if (-Not (Test-Path "$PSScriptRoot/libwnp")) {
  git clone https://github.com/keifufu/WebNowPlaying-Library "$PSScriptRoot/libwnp"
  & "$PSScriptRoot\libwnp\build.ps1"
}
$LIBWNP_DIR_WIN32 = "$PSScriptRoot\libwnp\build\win32\lib\cmake\libwnp"
$LIBWNP_DIR_WIN64 = "$PSScriptRoot\libwnp\build\win64\lib\cmake\libwnp"

Remove-Item -Path "$PSScriptRoot\build" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "$PSScriptRoot\dist" -Recurse -Force -ErrorAction SilentlyContinue

New-Item -Path "$PSScriptRoot\build" -ItemType Directory
New-Item -Path "$PSScriptRoot\build\win32" -ItemType Directory
New-Item -Path "$PSScriptRoot\build\win64" -ItemType Directory
New-Item -Path "$PSScriptRoot\dist" -ItemType Directory

Invoke-Environment "$vsInstallationPath\VC\Auxiliary\Build\vcvars32.bat"
cmake -S "$PSScriptRoot" -B "$PSScriptRoot\build\win32" -DCMAKE_BUILD_TYPE=Release -G "NMake Makefiles" -Dlibwnp_DIR="$LIBWNP_DIR_WIN32"
cmake --build "$PSScriptRoot\build\win32"
$origEnv | ForEach-Object { [System.Environment]::SetEnvironmentVariable($_.Name, $_.Value) }

Invoke-Environment "$vsInstallationPath\VC\Auxiliary\Build\vcvars64.bat"
cmake -S "$PSScriptRoot" -B "$PSScriptRoot\build\win64" -DCMAKE_BUILD_TYPE=Release -G "NMake Makefiles" -Dlibwnp_DIR="$LIBWNP_DIR_WIN64"
cmake --build "$PSScriptRoot\build\win64"
$origEnv | ForEach-Object { [System.Environment]::SetEnvironmentVariable($_.Name, $_.Value) }

$version = (Get-Content "$PSScriptRoot\VERSION" | Out-String).Trim()
Copy-Item -Path "$PSScriptRoot\build\win32\wnprainmeter.dll" "$PSScriptRoot\dist\WebNowPlaying-$version-x86.dll"
Copy-Item -Path "$PSScriptRoot\build\win64\wnprainmeter.dll" "$PSScriptRoot\dist\WebNowPlaying-$version-x64.dll"

$temp = "$PSScriptRoot\dist\temp"
New-Item -Path "$temp" -ItemType Directory
New-Item -Path "$temp\Skins" -ItemType Directory
New-Item -Path "$temp\Plugins" -ItemType Directory
New-Item -Path "$temp\Plugins\32bit" -ItemType Directory
New-Item -Path "$temp\Plugins\64bit" -ItemType Directory

Copy-Item -Path "$PSScriptRoot\examples/WebNowPlayer" -Destination "$temp\Skins\WebNowPlayer" -Recurse

Copy-Item -Path "$PSScriptRoot\dist\WebNowPlaying-$version-x86.dll" "$temp\Plugins\32bit\WebNowPlaying.dll"
Copy-Item -Path "$PSScriptRoot\dist\WebNowPlaying-$version-x64.dll" "$temp\Plugins\64bit\WebNowPlaying.dll"

$ini = @"
[rmskin]
Name=WebNowPlaying
Author=keifufu, tjhrulz
Version=${version}
LoadType=Skin
Load=WebNowPlayer\WebNowPlayer.ini
MinimumRainmeter=4.5.0
MinimumWindows=10.0
"@
$ini | Out-File -FilePath "$temp\RMSKIN.ini"

$rmskinpath = "$PSScriptRoot\dist\WebNowPlaying-${version}.zip"
Compress-Archive -CompressionLevel Optimal -Path "$temp\*" -DestinationPath $rmskinpath

function Add-RMfooter {
  param (
    [Parameter()]
    [string]
    $Target
  )
  $AsByteStream = $True
  if ($PSVersionTable.PSVersion.Major -lt 6) {
      $AsByteStream = $False
  }    
  $size = [long](Get-Item $Target).length
  $size_bytes = [System.BitConverter]::GetBytes($size)
  if ($AsByteStream) {
    Add-Content -Path $Target -Value $size_bytes -AsByteStream
  }
  else {
    Add-Content -Path $Target -Value $size_bytes -Encoding Byte
  }
  $flags = [byte]0
  if ($AsByteStream) {
    Add-Content -Path $Target -Value $flags -AsByteStream
  }
  else {
    Add-Content -Path $Target -Value $flags -Encoding Byte
  }
  $rmskin = [string]"RMSKIN`0"
  Add-Content -Path $Target -Value $rmskin -NoNewLine -Encoding ASCII
  Rename-Item -Path $Target -NewName ([io.path]::ChangeExtension($Target, '.rmskin'))
  $Target = $Target.Replace(".zip", ".rmskin")
}

Add-RMfooter -Target "$rmskinpath"

Remove-Item -Path "$temp" -Recurse

if ($env:USERNAME -eq 'keifufu') {
  Stop-Process -Name "rainmeter" -Force -ErrorAction SilentlyContinue
  Start-Sleep -Seconds 3
  Copy-Item -Path "$PSScriptRoot\dist\WebNowPlaying-$version-x64.dll" -Destination "C:\Users\keifufu\AppData\Roaming\Rainmeter\Plugins\WebNowPlaying.dll" -Force
  Start-Process -FilePath "C:\Program Files\Rainmeter\Rainmeter.exe"
}
