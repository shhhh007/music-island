$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'lyrics-support.ps1')
function Assert($Value,$Message){if(-not $Value){throw $Message}}
# Serve UTF-8 JSON without charset, reproducing the response Windows PowerShell misreads.
$probe=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0);$probe.Start();$port=$probe.LocalEndpoint.Port;$probe.Stop()
$listener=[Net.HttpListener]::new();$listener.Prefixes.Add("http://127.0.0.1:$port/");$listener.Start()
$worker=[PowerShell]::Create();$null=$worker.AddScript({param($listener)for($i=0;$i -lt 2;$i++){$context=$listener.GetContext();$bytes=[Text.Encoding]::UTF8.GetBytes('{"text":"Привет, мир — музыка 🎵"}');$context.Response.ContentType='application/json';$context.Response.ContentLength64=$bytes.Length;$context.Response.OutputStream.Write($bytes,0,$bytes.Length);$context.Response.Close()}}).AddArgument($listener);$task=$worker.BeginInvoke()
try {$legacy=Invoke-RestMethod "http://127.0.0.1:$port/";$fixed=Get-Utf8Json "http://127.0.0.1:$port/";Assert ($fixed.text -ceq 'Привет, мир — музыка 🎵') 'UTF-8 response corrupted';Write-Output "UTF-8 pass; old decoder corrupts fixture: $($legacy.text -cne $fixed.text)"}finally{$listener.Stop();$worker.Stop();$worker.Dispose();$listener.Close()}
Assert ((Get-LyricsName 'Песня [все площадки]') -eq 'песня') 'Upload label not removed'
Assert ((Get-LyricsName 'Song (Remix)') -ne (Get-LyricsName 'Song')) 'Remix identity lost'
$rows=@(Convert-Lrc "[00:01.20]Привет`n[00:02.30][00:04.00]Мир`n[00:05.00]" 10)
Assert ($rows.Count -eq 4 -and $rows[0].text -ceq 'Привет' -and $rows[0].end -eq 2.3 -and $rows[3].text -eq '') 'LRC timing or Unicode failure'
$script:requests=@()
function Get-Utf8Json([string]$Uri){$script:requests+=$Uri;if($Uri -match '/search'){return @(@{trackName='Song';artistName='Artist';duration=200;syncedLyrics='[00:01.00]Wrong version'},@{trackName='Song';artistName='Artist';duration=100;syncedLyrics='[00:01.00]Correct version'})};return @{duration=100;syncedLyrics=$null}}
$found=@(Find-SyncedLyrics 'Song [official audio]' 'Artist' 100)
Assert ($found.Count -eq 1 -and $found[0].text -eq 'Correct version') 'Fallback matching failed'
$wrong=@(Find-SyncedLyrics 'Song (Remix)' 'Artist' 100);Assert ($wrong.Count -eq 0) 'Wrong version accepted'
$script:requests=@();$found=@(Find-SyncedLyrics 'Artist — Song' 'Uploader' 100)
Assert ($found.Count -eq 1 -and $found[0].text -eq 'Correct version') 'SoundCloud artist/title fallback failed'
function Get-Utf8Json([string]$Uri){throw 'Temporary outage'}
$threw=$false;try{$null=Find-SyncedLyrics 'Song' 'Artist' 100}catch{$threw=$true};Assert $threw 'Transient failure swallowed instead of being retried'
function Get-Utf8Json([string]$Uri){return @{trackName='Wrong song';artistName='Artist';duration=100;syncedLyrics='[00:01.00]Wrong exact endpoint response'}}
Assert (@(Find-SyncedLyrics 'Song' 'Artist' 100).Count -eq 0) 'Exact endpoint accepted unrelated lyrics'
function Get-Utf8Json([string]$Uri){return @{trackName='Song';artistName='Artist';duration=100;syncedLyrics='[00:01.00]';instrumental=$false}}
Assert (@(Find-SyncedLyrics 'Song' 'Artist' 100).Count -eq 0) 'Blank-only lyrics accepted'
function Get-Utf8Json([string]$Uri){return @{trackName='Song';artistName='Artist';duration=100;syncedLyrics='[00:01.00]Unexpected';instrumental=$true}}
Assert (@(Find-SyncedLyrics 'Song' 'Artist' 100).Count -eq 0) 'Instrumental result accepted'
$script:cancelled=$false;$script:requestCount=0
function Get-Utf8Json([string]$Uri){$script:requestCount++;$script:cancelled=$true;return @{syncedLyrics=$null}}
$cancelResult=@(Find-SyncedLyrics 'Song' 'Artist' 100 {$script:cancelled})
Assert ($cancelResult.Count -eq 0 -and $script:requestCount -eq 1) 'Old song lookup continued after cancellation'
Write-Output 'PASS: UTF-8, Cyrillic, emoji, LRC timestamps, empty lines, title cleanup, conservative search, upload metadata and retry propagation.'
Write-Output 'PASS: exact endpoint identity, empty or instrumental results, track-change cancellation.'
