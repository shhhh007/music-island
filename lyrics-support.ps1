function Get-Utf8Json([string]$Uri,[int]$TimeoutMs=6000) {
    $request=[Net.HttpWebRequest]::Create($Uri)
    $request.Timeout=$TimeoutMs; $request.ReadWriteTimeout=$TimeoutMs
    $request.UserAgent='MusicIsland/1.0 (Windows desktop music overlay)'
    $request.Accept='application/json'
    $response=$null; $reader=$null
    try {
        $response=$request.GetResponse()
        $reader=[IO.StreamReader]::new($response.GetResponseStream(),[Text.UTF8Encoding]::new($false,$true),$true)
        return ($reader.ReadToEnd() | ConvertFrom-Json)
    } finally {if($reader){$reader.Dispose()};if($response){$response.Dispose()}}
}
function Get-LyricsName([string]$Value) {
    $value=$Value.Normalize([Text.NormalizationForm]::FormKC).ToLowerInvariant()
    # Remove presentation labels, not remix/live/sped-up version identifiers.
    $value=[regex]::Replace($value,'(?i)[\[(]\s*(?:official(?:\s+(?:music|lyric))?\s*(?:video|audio)?|lyrics?|visuali[sz]er|все\s+площадки|на\s+всех\s+площадках|официальный\s+клип)\s*[\])]',' ')
    $value=[regex]::Replace($value,'[^\p{L}\p{N}]+',' ')
    return $value.Trim()
}
function Convert-Lrc([string]$Text,[double]$Duration) {
    $rows=[Collections.Generic.List[object]]::new()
    foreach($row in ($Text -split "`n")){
        $tags=[regex]::Matches($row,'\[(\d+):(\d+(?:\.\d+)?)\]')
        $text=([regex]::Replace($row,'\[[^\]]*\]','')).Trim()
        foreach($tag in $tags){$start=[double]$tag.Groups[1].Value*60+[double]::Parse($tag.Groups[2].Value,[Globalization.CultureInfo]::InvariantCulture);$rows.Add(@{start=$start;text=$text})}
    }
    $lines=@($rows | Sort-Object { $_.start })
    for($i=0;$i -lt $lines.Count;$i++){$lines[$i].end=$(if($i+1 -lt $lines.Count){$lines[$i+1].start}else{$Duration})}
    return $lines
}
function Test-LyricsMatch($Result,$Pair,[double]$Duration) {
    return ($Result -and $Result.syncedLyrics -and -not $Result.instrumental -and
        (Get-LyricsName $Result.trackName) -eq (Get-LyricsName $Pair.Title) -and
        (Get-LyricsName $Result.artistName) -eq (Get-LyricsName $Pair.Artist) -and
        [Math]::Abs([double]$Result.duration-$Duration) -le 3)
}
function Find-SyncedLyrics([string]$Title,[string]$Artist,[double]$Duration,[scriptblock]$IsCancelled) {
    if($Duration -le 0 -or [string]::IsNullOrWhiteSpace($Title)){return}
    $pairs=@(@{Title=$Title;Artist=$Artist})
    $cleanTitle=Get-LyricsName $Title; $cleanArtist=Get-LyricsName $Artist
    if($cleanTitle -ne $Title -or $cleanArtist -ne $Artist){$pairs+=@{Title=$cleanTitle;Artist=$cleanArtist}}
    # SoundCloud uploads may put the performer in the title and the uploader in artist.
    $split=[regex]::new('\s+[-–—]\s+').Split($Title,2)
    if($split.Count -eq 2){$pairs+=@{Title=(Get-LyricsName $split[1]);Artist=(Get-LyricsName $split[0])}}
    foreach($pair in $pairs){
        if($IsCancelled -and (& $IsCancelled)){return}
        if([string]::IsNullOrWhiteSpace($pair.Artist)){continue}
        $track=[Uri]::EscapeDataString($pair.Title);$artistName=[Uri]::EscapeDataString($pair.Artist)
        $seconds=([int][Math]::Round($Duration)).ToString([Globalization.CultureInfo]::InvariantCulture)
        try {$result=Get-Utf8Json "https://lrclib.net/api/get?track_name=$track&artist_name=$artistName&duration=$seconds"}
        catch {if($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 404){continue};throw}
        if($IsCancelled -and (& $IsCancelled)){return}
        if(Test-LyricsMatch $result $pair $Duration){
            $parsed=@(Convert-Lrc $result.syncedLyrics $Duration)
            if(@($parsed | Where-Object {-not [string]::IsNullOrWhiteSpace($_.text) -and $_.start -lt $_.end}).Count){return $parsed}
        }
    }
    # Search is deliberately conservative: never substitute a different recording.
    foreach($pair in @($pairs | Select-Object -Last 2)){
        if($IsCancelled -and (& $IsCancelled)){return}
        if([string]::IsNullOrWhiteSpace($pair.Artist)){continue}
        $query=[Uri]::EscapeDataString($pair.Artist+' '+$pair.Title)
        $results=@(Get-Utf8Json "https://lrclib.net/api/search?q=$query")
        if($IsCancelled -and (& $IsCancelled)){return}
        $matches=@($results | Where-Object {Test-LyricsMatch $_ $pair $Duration} | Sort-Object {[Math]::Abs([double]$_.duration-$Duration)})
        foreach($match in $matches){
            $parsed=@(Convert-Lrc $match.syncedLyrics $Duration)
            if(@($parsed | Where-Object {-not [string]::IsNullOrWhiteSpace($_.text) -and $_.start -lt $_.end}).Count){return $parsed}
        }
    }
}
