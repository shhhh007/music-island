$ErrorActionPreference='Stop'
. "$PSScriptRoot\lyrics-support.ps1"
. "$PSScriptRoot\lyrics-providers.ps1"
$script:LyricsTestDataRoot=Join-Path $env:TEMP ('MusicIsland-provider-test-'+[Guid]::NewGuid())
$null=[IO.Directory]::CreateDirectory($script:LyricsTestDataRoot)
function Assert($ok,$message){if(-not $ok){throw $message}}
$state=@{Key='artist|song';Title='Песня';Artist='Артист';Start=0;Duration=90}
$file=Join-Path $script:LyricsTestDataRoot 'input.lrc'
[IO.File]::WriteAllText($file,"[00:01.00]Привет, мир`n[00:05.50]Вторая строка",[Text.UTF8Encoding]::new($true))
Import-LocalLyrics $file $state
$result=Read-LocalLyrics $state
Assert ($result.Lines[0].text -eq 'Привет, мир') 'UTF-8 local lyrics failed'
Assert ($result.Lines[1].start -eq 5.5) 'LRC timing failed'
$other=@{Key=$state.Key;Start=0;Duration=120}
Assert (-not (Read-LocalLyrics $other)) 'Different recording reused lyrics'
$key='fake-test-key-not-a-real-secret'
Save-MusixmatchKey $key
Assert ((Get-MusixmatchKey) -eq $key) 'Encrypted key roundtrip failed'
Assert (-not ([IO.File]::ReadAllText((Join-Path $script:LyricsTestDataRoot 'musixmatch.credential.xml')).Contains($key))) 'Key stored as plaintext'
Save-MusixmatchKey ''
Assert (-not (Get-MusixmatchKey)) 'Key deletion failed'
$script:calls=0;$script:status=200;$script:duration=90
function Get-Utf8Json($Uri){
 $script:calls++
 if($script:status -ne 200){return @{message=@{header=@{status_code=$script:status}}}}
 $body=if($Uri -match 'matcher.track.get'){@{track=@{track_name='Песня';artist_name='Артист';track_length=$script:duration;has_subtitles=1;track_id=42}}}else{@{subtitle=@{subtitle_body="[00:01.00]Привет, мир";subtitle_length=90}}}
 return @{message=@{header=@{status_code=200};body=$body}}
}
$result=Find-MusixmatchLyrics $state $key {$false}
Assert ($result.Source -eq 'Musixmatch' -and $result.Lines[0].text -eq 'Привет, мир') 'Musixmatch parsing failed'
$script:duration=120
Assert (-not (Find-MusixmatchLyrics $state $key {$false})) 'Wrong recording accepted'
$before=$script:calls
Assert (-not (Find-MusixmatchLyrics $state '' {$false})) 'Missing key accepted'
Assert (-not (Find-MusixmatchLyrics $state $key {$true})) 'Cancellation ignored'
Assert ($before -eq $script:calls) 'Unexpected network call'
$script:status=401
try {Invoke-Musixmatch 'matcher.track.get' @{} $key;throw 'Expected API failure'}catch{Assert ($_.Exception.Message -match 'API key invalid' -and -not $_.Exception.Message.Contains($key)) 'Unsafe API error'}
'PASS: UTF-8, LRC timing, recording identity, Windows key encryption, provider matching, cancellation and safe errors'
