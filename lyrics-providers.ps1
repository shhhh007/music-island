function Get-LyricsDataRoot {
    if($script:LyricsTestDataRoot){return $script:LyricsTestDataRoot}
    return (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'MusicIsland\Lyrics')
}
function Get-LocalLyricsPath($State) {
    $identity=[string]$State.Key+'|'+[Math]::Round(($State.Duration-$State.Start),0).ToString([Globalization.CultureInfo]::InvariantCulture)
    $hash=[Security.Cryptography.SHA256]::Create()
    try {$name=([BitConverter]::ToString($hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($identity)))).Replace('-','').ToLowerInvariant()}finally{$hash.Dispose()}
    return (Join-Path (Get-LyricsDataRoot) ($name+'.lrc'))
}
function Read-LocalLyrics($State) {
    $path=Get-LocalLyricsPath $State
    if(-not [IO.File]::Exists($path)){return}
    $text=[IO.File]::ReadAllText($path,[Text.UTF8Encoding]::new($false,$true))
    $lines=@(Convert-Lrc $text ($State.Duration-$State.Start))
    if(-not @($lines|Where-Object {$_.text -and $_.end -gt $_.start}).Count){throw 'Local LRC has no usable timed lines.'}
    return @{Lines=$lines;Source='LocalLRC';Copyright='';Link=''}
}
function Import-LocalLyrics([string]$File,$State) {
    if(-not $State -or $State.Duration -le $State.Start){throw 'Wait for a track with a known duration.'}
    if((Get-Item -LiteralPath $File).Length -gt 2MB){throw 'LRC file is larger than 2 MB.'}
    $text=[IO.File]::ReadAllText($File,[Text.UTF8Encoding]::new($false,$true))
    $lines=@(Convert-Lrc $text ($State.Duration-$State.Start))
    if(-not @($lines|Where-Object {$_.text -and $_.end -gt $_.start}).Count){throw 'Select a UTF-8 .lrc file with timestamps such as [00:12.30].'}
    if(@($lines|Where-Object {$_.start -gt ($State.Duration-$State.Start)+3}).Count){throw 'LRC timestamps exceed this track duration. Check the song version.'}
    $null=[IO.Directory]::CreateDirectory((Get-LyricsDataRoot))
    [IO.File]::WriteAllText((Get-LocalLyricsPath $State),$text,[Text.UTF8Encoding]::new($true))
}
function Get-MusixmatchKey {
    $path=Join-Path (Get-LyricsDataRoot) 'musixmatch.credential.xml'
    if(-not [IO.File]::Exists($path)){return ''}
    try {$credential=Import-Clixml -LiteralPath $path;return $credential.GetNetworkCredential().Password}
    catch {throw 'Unable to read the Musixmatch key for this Windows account. Save it again in Lyrics sources.'}
}
function Save-MusixmatchKey([string]$Key) {
    $path=Join-Path (Get-LyricsDataRoot) 'musixmatch.credential.xml'
    if([string]::IsNullOrWhiteSpace($Key)){if([IO.File]::Exists($path)){[IO.File]::Delete($path)};return}
    if($Key.Trim().Length -lt 12 -or $Key -match '\s'){throw 'Enter a valid API key without spaces.'}
    $null=[IO.Directory]::CreateDirectory((Get-LyricsDataRoot))
    $credential=[Management.Automation.PSCredential]::new('Musixmatch',(ConvertTo-SecureString $Key.Trim() -AsPlainText -Force))
    $credential | Export-Clixml -LiteralPath $path
}
function Invoke-Musixmatch([string]$Method,[hashtable]$Parameters,[string]$ApiKey) {
    $query=@($Parameters.GetEnumerator() | ForEach-Object {[Uri]::EscapeDataString([string]$_.Key)+'='+[Uri]::EscapeDataString([string]$_.Value)})
    $uri='https://api.musixmatch.com/ws/1.1/'+$Method+'?'+($query -join '&')+'&apikey='+[Uri]::EscapeDataString($ApiKey)
    try {$response=Get-Utf8Json $uri}
    catch {throw [InvalidOperationException]::new('Musixmatch request failed. Check connectivity or API access.')}
    $code=[int]$response.message.header.status_code
    if($code -eq 404){return $null}
    if($code -eq 401 -or $code -eq 403){throw [InvalidOperationException]::new('Musixmatch: API key invalid, or synchronized lyrics are not enabled for this plan.')}
    if($code -eq 402 -or $code -eq 429){throw [InvalidOperationException]::new('Musixmatch: quota exhausted. Retrying later.')}
    if($code -ne 200){throw [InvalidOperationException]::new('Musixmatch returned an unavailable response.')}
    return $response.message.body
}
function Find-MusixmatchLyrics($State,[string]$ApiKey,[scriptblock]$IsCancelled) {
    if([string]::IsNullOrWhiteSpace($ApiKey)){return}
    $duration=$State.Duration-$State.Start
    if($duration -le 0){return}
    $pairs=@(@{Title=$State.Title;Artist=$State.Artist})
    $split=[regex]::new('\s+[-\u2013\u2014]\s+').Split($State.Title,2)
    if($split.Count -eq 2){$pairs+=@{Title=$split[1];Artist=$split[0]}}
    foreach($pair in $pairs){
        if($IsCancelled -and (& $IsCancelled)){return}
        $trackBody=Invoke-Musixmatch 'matcher.track.get' @{q_track=(Get-LyricsName $pair.Title);q_artist=(Get-LyricsName $pair.Artist)} $ApiKey
        $track=$trackBody.track
        if(-not $track -or $track.restricted -or $track.instrumental -or -not $track.has_subtitles){continue}
        if((Get-LyricsName $track.track_name) -ne (Get-LyricsName $pair.Title) -or (Get-LyricsName $track.artist_name) -ne (Get-LyricsName $pair.Artist)){continue}
        if($track.track_length -gt 0 -and [Math]::Abs([double]$track.track_length-$duration) -gt 3){continue}
        if($IsCancelled -and (& $IsCancelled)){return}
        $body=Invoke-Musixmatch 'track.subtitle.get' @{track_id=$track.track_id;subtitle_format='lrc';f_subtitle_length=[int][Math]::Round($duration);f_subtitle_length_max_deviation=2} $ApiKey
        $subtitle=$body.subtitle
        if(-not $subtitle -or $subtitle.restricted -or -not $subtitle.subtitle_body -or [Math]::Abs([double]$subtitle.subtitle_length-$duration) -gt 3){continue}
        $lines=@(Convert-Lrc $subtitle.subtitle_body $duration)
        if(@($lines|Where-Object {$_.text -and $_.end -gt $_.start}).Count){return @{Lines=$lines;Source='Musixmatch';Copyright=[string]$subtitle.lyrics_copyright;Link=[string]$track.track_share_url;Tracking=[string]$subtitle.pixel_tracking_url}}
    }
}
