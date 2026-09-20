param($Shared)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Runtime.WindowsRuntime
Add-Type -Path (Join-Path $Shared.Root "CoverBridge.dll")
$ManagerType = [Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager,Windows.Media.Control,ContentType=WindowsRuntime]
$PropertiesType = [Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties,Windows.Media.Control,ContentType=WindowsRuntime]
$StreamType = [Windows.Storage.Streams.IRandomAccessStreamWithContentType,Windows.Storage.Streams,ContentType=WindowsRuntime]
$AsTask = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' } | Select-Object -First 1
function Await($Operation, [Type]$Type) {
    $task = $AsTask.MakeGenericMethod($Type).Invoke($null, @($Operation))
    if (-not $task.Wait(3000)) { throw 'Media request timed out' }
    return $task.Result
}
$manager = $null
$lastKey = ''
$cover = $null
while (-not $Shared.Stop) {
    try {
        if (-not $manager) { $manager = Await ($ManagerType::RequestAsync()) $ManagerType }
        $session = $manager.GetCurrentSession()
        foreach ($candidate in $manager.GetSessions()) {
            if ($candidate.GetPlaybackInfo().PlaybackStatus.ToString() -eq 'Playing') { $session = $candidate; break }
        }
        $command = $null
        while ($Shared.Commands.TryDequeue([ref]$command)) {
            if (-not $session) { continue }
            $operation = switch ($command.Action) {
                'play' { $session.TryTogglePlayPauseAsync() }
                'prev' { $session.TrySkipPreviousAsync() }
                'next' { $session.TrySkipNextAsync() }
                'seek' { $session.TryChangePlaybackPositionAsync([long]($command.Value * 10000000)) }
            }
            if ($operation) { $ok = Await $operation ([bool]); if (-not $ok) { $Shared.Notice = 'Плеер не поддерживает эту команду' } }
        }
        if ($session) {
            $properties = Await ($session.TryGetMediaPropertiesAsync()) $PropertiesType
            $playback = $session.GetPlaybackInfo()
            $timeline = $session.GetTimelineProperties()
            $key = $session.SourceAppUserModelId + '|' + $properties.Title + '|' + $properties.Artist
            if ($key -ne $lastKey) {
                $cover = $null
                if ($properties.Thumbnail) {
                    $stream = $null; $reader = $null
                    try {
                        $stream = Await ($properties.Thumbnail.OpenReadAsync()) $StreamType
                        $cover = [CoverBridge]::Read($stream)
                    } catch { $cover = $null; $_.Exception.Message | Set-Content (Join-Path $Shared.Root "cover-error.log") } finally { foreach($resource in @($reader,$stream)) { if($resource -and [Runtime.InteropServices.Marshal]::IsComObject($resource)){ $null=[Runtime.InteropServices.Marshal]::ReleaseComObject($resource) } } }
                }
                $lastKey = $key
            }
            $position = $timeline.Position.TotalSeconds
            $playing = $playback.PlaybackStatus.ToString() -eq 'Playing'
            if ($playing) { $position += [Math]::Max(0, ([DateTimeOffset]::Now - $timeline.LastUpdatedTime).TotalSeconds) * $(if ($playback.PlaybackRate) { $playback.PlaybackRate } else { 1 }) }
            $Shared.State = @{ Updated=[DateTime]::UtcNow; Title=$properties.Title; Artist=$properties.Artist; Playing=$playing; Position=$position; Start=$timeline.StartTime.TotalSeconds; Duration=$timeline.EndTime.TotalSeconds; Cover=$cover; Key=$key; Seek=$playback.Controls.IsPlaybackPositionEnabled; Play=$playback.Controls.IsPlayPauseToggleEnabled; Prev=$playback.Controls.IsPreviousEnabled; Next=$playback.Controls.IsNextEnabled; Source=$session.SourceAppUserModelId }
            $Shared.Error = ''
        } else { $Shared.State = $null; $lastKey = ''; $Shared.Error = '' }
    } catch { $Shared.Error = $_.Exception.Message; $manager = $null }
    Start-Sleep -Milliseconds 450
}
