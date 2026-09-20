$ErrorActionPreference='Stop'
& "$PSScriptRoot\test-lyrics.ps1"
$output=Join-Path $PSScriptRoot 'artifacts\tests'
New-Item -ItemType Directory -Force -Path $output|Out-Null
$framework="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
$exe=Join-Path $output 'RegressionTests.exe'
& "$framework\csc.exe" /nologo /target:winexe "/out:$exe" "/r:$framework\WPF\PresentationFramework.dll" "/r:$framework\WPF\PresentationCore.dll" "/r:$framework\WPF\WindowsBase.dll" /r:System.Xaml.dll /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll "$PSScriptRoot\IslandView.cs" "$PSScriptRoot\RegressionTests.cs"
if($LASTEXITCODE -ne 0){throw 'Test compilation failed'}
$process=Start-Process -FilePath $exe -WorkingDirectory $output -WindowStyle Hidden -Wait -PassThru
if($process.ExitCode -ne 0){throw "Tests failed: $output"}
$report=Get-Content -LiteralPath (Join-Path $output 'regression-results.json') -Raw|ConvertFrom-Json
if(-not $report.Passed){throw ($report.Failures -join '; ')}
$report|ConvertTo-Json -Depth 5
