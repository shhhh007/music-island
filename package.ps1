param(
 [string]$Version=(Get-Content -Raw "$PSScriptRoot\VERSION").Trim(),
 [string]$SigningCertificateThumbprint,
 [string]$TimestampServer,
 [switch]$RequireSignature
)
$ErrorActionPreference='Stop'
if($Version -notmatch '^\d+\.\d+\.\d+(?:-[a-zA-Z0-9.-]+)?$'){throw 'Invalid version'}
. "$PSScriptRoot\tools\sign-release.ps1"
$certificate=$null
if($RequireSignature -and -not $SigningCertificateThumbprint){throw 'A trusted signing certificate is required. No release was built.'}
if($SigningCertificateThumbprint){
 if(-not $TimestampServer -or $TimestampServer -notmatch '^http://'){throw 'Supply the http:// Authenticode timestamp endpoint provided by your signing service.'}
 $certificate=Get-ReleaseCertificate $SigningCertificateThumbprint
}
$build=Join-Path $PSScriptRoot 'artifacts\build'
& "$PSScriptRoot\build.ps1" -OutputDirectory $build
$stage=Join-Path $PSScriptRoot "release-staging\$Version"
$out=Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $stage,$out|Out-Null
$files=@('Music Island.exe','IslandView.dll','CoverBridge.dll','Island.ps1','media-worker.ps1','lyrics-worker.ps1','lyrics-support.ps1','local-worker.ps1','model-worker.ps1','Uninstall.ps1','START-HERE.txt','README.md','LICENSE','VERSION')
foreach($file in $files){Copy-Item -LiteralPath (Join-Path $(if($file -match '\.(exe|dll)$'){$build}else{$PSScriptRoot}) $file) -Destination (Join-Path $stage $file) -Force}
if($certificate){
 $signable=@($files | Where-Object {$_ -match '\.(exe|dll|ps1)$'} | ForEach-Object {Join-Path $stage $_})
 Set-ReleaseSignature $signable $certificate $TimestampServer
}
@{version=$Version;files=$files}|ConvertTo-Json|Set-Content -Encoding UTF8 -LiteralPath (Join-Path $stage 'package-manifest.json')
$zip=Join-Path $out "Music-Island-$Version-portable.zip"
$payload=@($files|ForEach-Object {Join-Path $stage $_})+(Join-Path $stage 'package-manifest.json')
Compress-Archive -LiteralPath $payload -DestinationPath $zip -Force
$framework="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
$setup=Join-Path $out "Music-Island-$Version-Setup.exe"
& "$framework\csc.exe" /nologo /target:winexe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/win32icon:$PSScriptRoot\assets\music-island.ico" "/resource:$zip,Payload.zip" "/out:$setup" "$PSScriptRoot\Installer.cs"
if($LASTEXITCODE -ne 0){throw 'Installer build failed'}
if($certificate){Set-ReleaseSignature @($setup) $certificate $TimestampServer}
$process=Start-Process -FilePath $setup -ArgumentList '--verify-payload' -WindowStyle Hidden -Wait -PassThru
if($process.ExitCode -ne 0){throw 'Installer payload validation failed'}
@($setup,$zip)|ForEach-Object { $hash=Get-FileHash -LiteralPath $_ -Algorithm SHA256; '{0}  {1}' -f $hash.Hash.ToLowerInvariant(),[IO.Path]::GetFileName($_) }|Set-Content -Encoding ASCII (Join-Path $out 'SHA256SUMS.txt')
Write-Output "Release ready: $out"
if(-not $certificate){Write-Warning 'This is an unsigned development package. It may be blocked by Windows application control.'}
