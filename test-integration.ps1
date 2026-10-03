$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
. "$PSScriptRoot\lyrics-support.ps1"
. "$PSScriptRoot\lyrics-providers.ps1"
. "$PSScriptRoot\lyrics-options.ps1"
$script:LyricsTestDataRoot=Join-Path $env:TEMP ('MusicIsland-integration-'+[Guid]::NewGuid())
$null=[IO.Directory]::CreateDirectory($script:LyricsTestDataRoot)
$state=@{Key='integration';Title='Проверка';Artist='Тест';Duration=90;Start=0;Updated=[DateTime]::UtcNow;Playing=$true;Position=2}
$file=Join-Path $script:LyricsTestDataRoot 'test.lrc'
[IO.File]::WriteAllText($file,'[00:01.00]Русский текст',[Text.UTF8Encoding]::new($true))
Import-LocalLyrics $file $state
$shared=[hashtable]::Synchronized(@{Root=$PSScriptRoot;State=$state;Stop=$false;LyricsRevision=0})
$worker=[PowerShell]::Create()
$code=[IO.File]::ReadAllText("$PSScriptRoot\lyrics-worker.ps1").Replace('$cache=@{};',('$script:LyricsTestDataRoot='''+$script:LyricsTestDataRoot+''';$cache=@{};'))
$null=$worker.AddScript($code).AddArgument($shared);$task=$worker.BeginInvoke()
$model=[PowerShell]::Create();$null=$model.AddScript([IO.File]::ReadAllText("$PSScriptRoot\model-worker.ps1")).AddArgument($shared);$modelTask=$model.BeginInvoke()
try {
 $deadline=[DateTime]::UtcNow.AddSeconds(8)
 do {Start-Sleep -Milliseconds 100;if($shared.Render){$render=$shared.Render.Json|ConvertFrom-Json}}while(-not $render.Lines.Count -and [DateTime]::UtcNow -lt $deadline)
 if($render.Lines[0].text -ne 'Русский текст' -or $shared.Lyrics.Source -ne 'LocalLRC'){throw 'Worker to renderer integration failed'}
 if($worker.Streams.Error.Count -or $shared.ModelError){throw 'Worker error'}
 $script:uiPassed=$false
 $timer=[Windows.Forms.Timer]::new();$timer.Interval=500
 $timer.Add_Tick({
  $timer.Stop()
  foreach($form in @([Windows.Forms.Application]::OpenForms)){
   if($form.Text -eq 'Текст песни — Music Island'){
    $bitmap=[Drawing.Bitmap]::new($form.Width,$form.Height);$form.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$form.Width,$form.Height));$bitmap.Save((Join-Path $PSScriptRoot 'lyrics-options-preview.png'));$bitmap.Dispose()
    $script:uiPassed=$true;$form.Close()
   }
  }
 })
 $timer.Start();Show-LyricsOptions $shared;$timer.Dispose()
 if(-not $script:uiPassed){throw 'Options window did not render'}
 'PASS: local lyrics worker -> model -> Cyrillic rendering data; options dialog rendered'
}finally{$shared.Stop=$true;$worker.Stop();$worker.Dispose();$model.Stop();$model.Dispose()}
