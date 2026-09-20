param($Shared)
$ErrorActionPreference='Stop'
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
$cache=@{}
$nextLocal=[DateTime]::MinValue
while(-not $Shared.Stop){
    $s=$Shared.State
    if(-not $s){$Shared.Lyrics=$null; Start-Sleep -Milliseconds 500; continue}
    $matched=$false
    if([DateTime]::Now -ge $nextLocal){
        try {
            $data=$Shared.Local.State
            if($data.track.title -eq $s.Title -and $data.track.artist -eq $s.Artist -and $data.lyrics.state -eq 'found'){
                $Shared.Lyrics=@{Key=$s.Key; Lines=@($data.lyrics.window); Source='MusicUI'; Position=[double]$data.pos; Updated=[DateTime]::Now; Playing=[bool]$data.playing}
                $matched=$true
            }
        } catch { $nextLocal=[DateTime]::Now.AddSeconds(10) }
    }
    if(-not $matched){
        if(-not $cache.ContainsKey($s.Key)){
            $lines=@()
            try {
                $title=[Uri]::EscapeDataString($s.Title); $artist=[Uri]::EscapeDataString($s.Artist)
                $duration=[int][Math]::Round($s.Duration-$s.Start)
                $result=Invoke-RestMethod "https://lrclib.net/api/get?track_name=$title&artist_name=$artist&duration=$duration" -Headers @{'User-Agent'='MusicIsland/1.0 (Windows desktop music overlay)'} -TimeoutSec 6
                foreach($row in ($result.syncedLyrics -split "`n")){
                    $tags=[regex]::Matches($row,'\[(\d+):(\d+(?:\.\d+)?)\]')
                    $text=([regex]::Replace($row,'\[[^\]]*\]','')).Trim()
                    foreach($tag in $tags){$lines+=@{start=([double]$tag.Groups[1].Value*60+[double]::Parse($tag.Groups[2].Value,[Globalization.CultureInfo]::InvariantCulture));text=$text}}
                }
                $lines=@($lines | Sort-Object { $_.start })
                for($i=0;$i -lt $lines.Count;$i++){$lines[$i].end=$(if($i+1 -lt $lines.Count){$lines[$i+1].start}else{$s.Duration})}
            } catch { }
            if($cache.Count -ge 30){$cache.Clear()}
            $cache[$s.Key]=$lines
        }
        $Shared.Lyrics=@{Key=$s.Key;Lines=$cache[$s.Key];Source='LRCLIB'}
    }
    Start-Sleep -Milliseconds 500
}
