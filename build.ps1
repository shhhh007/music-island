param([string]$OutputDirectory=$PSScriptRoot)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Force -Path $OutputDirectory|Out-Null
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /reference:System.Windows.Forms.dll "/win32icon:$PSScriptRoot\assets\music-island.ico" "/out:$OutputDirectory\Music Island.exe" "$PSScriptRoot\Launcher.cs"
if($LASTEXITCODE -ne 0){throw 'Build failed'}
$framework="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
& "$framework\csc.exe" /nologo /target:library "/out:$OutputDirectory\CoverBridge.dll" "/r:$framework\System.Runtime.dll" "/r:$env:WINDIR\System32\WinMetadata\Windows.Storage.winmd" "/r:$env:WINDIR\System32\WinMetadata\Windows.Foundation.winmd" "$PSScriptRoot\CoverBridge.cs"
if($LASTEXITCODE -ne 0){throw 'Cover bridge build failed'}
& "$framework\csc.exe" /nologo /target:library "/out:$OutputDirectory\IslandView.dll" "/r:$framework\WPF\PresentationFramework.dll" "/r:$framework\WPF\PresentationCore.dll" "/r:$framework\WPF\WindowsBase.dll" /r:System.Xaml.dll /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll "$PSScriptRoot\IslandView.cs"
if($LASTEXITCODE -ne 0){throw 'Island view build failed'}
