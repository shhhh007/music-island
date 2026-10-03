param($Shared)
$ErrorActionPreference='Stop'
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
. (Join-Path $Shared.Root 'lyrics-support.ps1')
. (Join-Path $Shared.Root 'lyrics-providers.ps1')
$cache=@{};$revision=-1
while(-not $Shared.Stop){
 if($revision -ne [int]$Shared.LyricsRevision){$cache.Clear();$revision=[int]$Shared.LyricsRevision}
 $s=$Shared.State
 if(-not $s){$Shared.Lyrics=$null;Start-Sleep -Milliseconds 500;continue}
 $duration=$s.Duration-$s.Start;$entry=$cache[$s.Key]
 if(-not $entry -or [DateTime]::UtcNow -ge $entry.RetryAt -or [Math]::Abs($entry.Duration-$duration) -gt .5){
  $lookupRevision=$revision
  $cancel={ $Shared.Stop -or -not $Shared.State -or $Shared.State.Key -ne $s.Key -or [int]$Shared.LyricsRevision -ne $lookupRevision }
  $result=$null;$notice='';$retry=[DateTime]::UtcNow.AddMinutes(10)
  try {$result=Read-LocalLyrics $s}catch{$notice='Local LRC could not be read. Import a valid UTF-8 LRC file.'}
  if(-not $result -and -not (& $cancel)){
   try {$lines=@(Find-SyncedLyrics $s.Title $s.Artist $duration $cancel);if($lines.Count){$result=@{Lines=$lines;Source='LRCLIB';Copyright='';Link='https://lrclib.net'}}}
   catch {$notice='LRCLIB is temporarily unavailable.';$retry=[DateTime]::UtcNow.AddSeconds(60)}
  }
  if(-not $result -and -not (& $cancel)){
   try {$key=Get-MusixmatchKey;if($key){$result=Find-MusixmatchLyrics $s $key $cancel}else{if(-not $notice){$notice='No synchronized lyrics found. Musixmatch API key is not configured.'}}}
   catch {$notice=$_.Exception.Message;$retry=[DateTime]::UtcNow.AddMinutes(10)}
   finally {$key=$null}
  }
  if(& $cancel){Start-Sleep -Milliseconds 100;continue}
  if(-not $result){$result=@{Lines=@();Source='None';Copyright='';Link=''}}
  if($result.Lines.Count){$notice='';$retry=if($result.Source -eq 'Musixmatch'){[DateTime]::UtcNow.AddMinutes(5)}else{[DateTime]::MaxValue}}
  $result.Key=$s.Key
  if($cache.Count -ge 30){$cache.Clear()}
  $entry=@{Result=$result;Duration=$duration;RetryAt=$retry;Notice=$notice};$cache[$s.Key]=$entry
 }
 if($Shared.State -and $Shared.State.Key -eq $s.Key){
  $Shared.Lyrics=$entry.Result;$Shared.LyricsError=$entry.Notice
  if($entry.Result.Source -eq 'Musixmatch' -and -not $entry.Tracked){
   $entry.Tracked=$true
   try {$tracking=[Uri]$entry.Result.Tracking;if($tracking.Scheme -eq 'https' -and $tracking.Host -eq 'tracking.musixmatch.com'){$null=Invoke-WebRequest -Uri $tracking.AbsoluteUri -UseBasicParsing -TimeoutSec 2 -MaximumRedirection 0}}catch{}
  }
 }
 Start-Sleep -Milliseconds 500
}
