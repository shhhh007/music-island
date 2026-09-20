param($Shared)
$ErrorActionPreference='Stop'
$epoch=[DateTime]::SpecifyKind([DateTime]'1970-01-01',[DateTimeKind]::Utc)
function Stamp($date){if($date){return ([DateTime]$date).ToUniversalTime().Subtract($epoch).TotalSeconds};return 0}
while(-not $Shared.Stop){
 try {
  $s=$Shared.State
  $m=@{Key='';Title='';Artist='';Source='';Playing=$false;Seek=$false;Play=$false;Prev=$false;Next=$false;Position=0;Start=0;Duration=0;Volume=-1;Bands=@();Accent=$null;Lines=@();LyricsLocal=$false;LyricsPosition=0;SampleUtc=0;LyricsSampleUtc=0;LyricsIndex=-1}
  $art=$null;$rows=@()
  if($s){foreach($name in @('Key','Title','Artist','Source','Playing','Seek','Play','Prev','Next','Position','Start','Duration')){$m[$name]=$s[$name]};$art=$s.Cover;$m.SampleUtc=Stamp $s.Updated}
  $local=$Shared.Local
  $matched=$s -and $local -and ($local.Tick.v -eq $local.State.v) -and $local.State.track.title -eq $s.Title -and $local.State.track.artist -eq $s.Artist
  if($matched){
   $m.Bands=@($local.Tick.bands);$m.Position=[double]$local.Tick.pos;$m.SampleUtc=Stamp $local.Updated;$m.Playing=[bool]$local.Tick.playing
   if($local.State.cover.colors.Count -gt 0){$m.Accent=@($local.State.cover.colors[0])}
   if($null -ne $local.State.volume.app -and $null -ne $local.State.volume.level){$m.Volume=[double]$local.State.volume.level}
  }
  if($matched -and $local.State.lyrics.state -eq 'found'){
   $rows=@($local.State.lyrics.window);$m.LyricsLocal=$true;$m.LyricsIndex=[int]$local.Tick.line
   $m.LyricsPosition=[double]$local.Tick.pos;$m.LyricsSampleUtc=Stamp $local.Updated
  }else{
   $ly=$Shared.Lyrics
   if($s -and $ly -and $ly.Key -eq $s.Key -and $ly.Source -eq 'LRCLIB'){$rows=@($ly.Lines)}
  }
  $lines=@();$index=0
  foreach($line in $rows){
   $words=@();foreach($word in $line.words){$words+=@{start=[double]$word[0];end=[double]$word[1];text=[string]$word[2]}}
   $lines+=@{start=[double]$line.start;end=[double]$line.end;index=$(if($m.LyricsLocal){[int]$line.i}else{$index});text=[string]$line.text;words=$words};$index++
  }
  $m.Lines=$lines
  if($Shared.PreviewLyrics){$m.Lines=@(@{start=0;end=99999;index=0;text='Синхронный текст песни · проверка интерфейса';words=@()});$m.LyricsLocal=$false}
  $Shared.Render=@{Json=($m|ConvertTo-Json -Depth 10 -Compress);Art=$art;Title=$m.Title}
 }catch{$Shared.ModelError=$_.Exception.Message}
 Start-Sleep -Milliseconds 65
}
