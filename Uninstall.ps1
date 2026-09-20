$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
if([Windows.Forms.MessageBox]::Show('Удалить Music Island? Личные настройки останутся в папке приложения.','Music Island','YesNo','Question') -ne 'Yes'){exit}
$root=[IO.Path]::GetFullPath($PSScriptRoot)
$expected=[IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\Music Island'))
if($root -ne $expected){throw 'Uninstall is only available for the installed copy.'}
Set-Content -LiteralPath (Join-Path $root 'exit.request') -Value ''
Start-Sleep -Seconds 2
# Delete only files declared in the installer manifest; never recurse through user folders.
$manifest=Get-Content -Raw -LiteralPath (Join-Path $root 'package-manifest.json')|ConvertFrom-Json
foreach($name in $manifest.files){
 $file=[IO.Path]::GetFullPath((Join-Path $root $name))
 if(-not $file.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Invalid uninstall manifest'}
 if(Test-Path -LiteralPath $file -PathType Leaf){Remove-Item -LiteralPath $file -Force}
}
$shell=New-Object -ComObject WScript.Shell
$links=@((Join-Path ([Environment]::GetFolderPath('Desktop')) 'Music Island.lnk'),(Join-Path ([Environment]::GetFolderPath('Programs')) 'Music Island\Music Island.lnk'),(Join-Path ([Environment]::GetFolderPath('Programs')) 'Music Island\Удалить Music Island.lnk'))
foreach($file in $links){if(Test-Path -LiteralPath $file){$link=$shell.CreateShortcut($file);if($link.WorkingDirectory -eq $root){Remove-Item -LiteralPath $file}}}
foreach($name in @('package-manifest.json','exit.request')){$file=Join-Path $root $name;if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file}}
[Windows.Forms.MessageBox]::Show('Music Island удалён.','Music Island')|Out-Null
