param([string]$Version=(Get-Content -Raw (Join-Path (Split-Path $PSScriptRoot -Parent) 'VERSION')).Trim())
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root=Split-Path $PSScriptRoot -Parent
$zip=Join-Path $root "dist\Music-Island-$Version-portable.zip"
$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try{
 $names=@($archive.Entries.FullName)
 foreach($needed in @('Music Island.exe','CoverBridge.dll','IslandView.dll','Island.ps1','model-worker.ps1','media-worker.ps1','lyrics-worker.ps1','local-worker.ps1','Uninstall.ps1','LICENSE','package-manifest.json')){if($needed -notin $names){throw "Missing $needed"}}
 if($names|Where-Object {$_ -match '(settings\.json|selftest|regression|\.log|\.tools|gh\.exe)'}){throw 'Local data leaked into release'}
 foreach($entry in $archive.Entries){
  if($entry.FullName -match '\.ps1$'){
   $reader=[IO.StreamReader]::new($entry.Open());try{$text=$reader.ReadToEnd()}finally{$reader.Dispose()}
   $errors=$null;$tokens=$null;[Management.Automation.Language.Parser]::ParseInput($text,[ref]$tokens,[ref]$errors)|Out-Null
   if($errors){throw ($errors|Out-String)}
  }
 }
 Write-Output "PASS: $($names.Count) declared files; PowerShell syntax valid; no personal configuration or playback logs."
}finally{$archive.Dispose()}
