param($Shared)
$ErrorActionPreference='Stop'
. (Join-Path $Shared.Root 'lyrics-support.ps1')
$state=$null; $nextState=[DateTime]::MinValue; $artTrack=''
while(-not $Shared.Stop){
 try {
  $cmd=$null
  while($Shared.LocalCommands.TryDequeue([ref]$cmd)){
   if($cmd.Action -eq 'config'){$bands=[Math]::Max(3,[int]$cmd.Value);$enabled=[int]($cmd.Value -gt 0);$uri="http://127.0.0.1:8770/config?bands=$bands&music_only=1&equalizer=$enabled"}
   else{$value=([double]$cmd.Value).ToString('0.000',[Globalization.CultureInfo]::InvariantCulture);$uri="http://127.0.0.1:8770/control?action=volume&value=$value"}
   $null=Invoke-RestMethod $uri -TimeoutSec 2
  }
  if([DateTime]::Now -ge $nextState){
   $state=Get-Utf8Json 'http://127.0.0.1:8770/state' -TimeoutMs 2000
   if($state.track.track_id -and $state.track.track_id -ne $artTrack){
    $null=Invoke-WebRequest 'http://127.0.0.1:8770/art' -UseBasicParsing -TimeoutSec 2
    $artTrack=$state.track.track_id
    $state=Get-Utf8Json 'http://127.0.0.1:8770/state' -TimeoutMs 2000
   }
   $nextState=[DateTime]::Now.AddSeconds(1)
  }
  $tick=Get-Utf8Json 'http://127.0.0.1:8770/tick' -TimeoutMs 2000
  if($tick.v -ne $state.v -or ($state.lyrics.window.Count -gt 0 -and ($tick.line -ge $state.lyrics.window[-1].i-1 -or $tick.line -lt $state.lyrics.window[0].i))){$nextState=[DateTime]::MinValue}
  $Shared.Local=@{State=$state;Tick=$tick;Updated=[DateTime]::Now}
 }catch{$Shared.Local=$null;Start-Sleep -Milliseconds 1500}
 Start-Sleep -Milliseconds 67
}
