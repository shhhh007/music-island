param($Shared)
$ErrorActionPreference='Stop'
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
. (Join-Path $Shared.Root 'lyrics-support.ps1')
$cache=@{}
while(-not $Shared.Stop){
    $s=$Shared.State
    if(-not $s){$Shared.Lyrics=$null; Start-Sleep -Milliseconds 500; continue}
    $data=$Shared.Local.State
    $matched=$data -and $data.track.title -eq $s.Title -and $data.track.artist -eq $s.Artist -and $data.lyrics.state -eq 'found'
    if(-not $matched){
        $entry=$cache[$s.Key]
        if(-not $entry -or [DateTime]::UtcNow -ge $entry.RetryAt -or [Math]::Abs($entry.Duration-($s.Duration-$s.Start)) -gt .5){
            $lines=@();$retry=[DateTime]::UtcNow.AddMinutes(10)
            try {$lines=@(Find-SyncedLyrics $s.Title $s.Artist ($s.Duration-$s.Start) { $Shared.Stop -or -not $Shared.State -or $Shared.State.Key -ne $s.Key });$Shared.LyricsError='';if($lines.Count){$retry=[DateTime]::MaxValue}}
            catch {$Shared.LyricsError=$_.Exception.Message;$retry=[DateTime]::UtcNow.AddSeconds(30)}
            if($cache.Count -ge 30){$cache.Clear()}
            $entry=@{Lines=$lines;RetryAt=$retry;Duration=($s.Duration-$s.Start)};$cache[$s.Key]=$entry
        }
        # A network request may finish after the user has already changed the song.
        if($Shared.State -and $Shared.State.Key -eq $s.Key -and (-not $Shared.Lyrics -or $Shared.Lyrics.Key -ne $s.Key -or -not [object]::ReferenceEquals($Shared.Lyrics.Lines,$entry.Lines))){$Shared.Lyrics=@{Key=$s.Key;Lines=$entry.Lines;Source='LRCLIB'}}
    }
    Start-Sleep -Milliseconds 500
}
