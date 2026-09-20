param([switch]$SelfTest,[switch]$PreviewLyrics)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Windows.Forms,System.Drawing,System.Web.Extensions
Add-Type -Path "$PSScriptRoot\IslandView.dll"
$mutex=[Threading.Mutex]::new($false,'Local\MusicIsland.Desktop')
if(-not $mutex.WaitOne(0)){exit}
$shared=[hashtable]::Synchronized(@{Root=$PSScriptRoot;Render=$null;ModelError='';PreviewLyrics=($SelfTest -and $PreviewLyrics);Stop=$false;State=$null;Local=$null;Lyrics=$null;Error='';Notice='';Commands=[Collections.Concurrent.ConcurrentQueue[object]]::new();LocalCommands=[Collections.Concurrent.ConcurrentQueue[object]]::new()})
$workers=@()
foreach($file in @('media-worker.ps1','lyrics-worker.ps1','local-worker.ps1','model-worker.ps1')){
 $worker=[PowerShell]::Create();$null=$worker.AddScript((Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $PSScriptRoot $file))).AddArgument($shared);$null=$worker.BeginInvoke();$workers+=,$worker
}
$window=[Windows.Window]::new();$window.Title='Music Island';$window.WindowStyle='None';$window.ResizeMode='NoResize';$window.AllowsTransparency=$true;$window.Background=[Windows.Media.Brushes]::Transparent;$window.Topmost=$true;$window.ShowInTaskbar=$false;$window.ShowActivated=$false;$window.Width=300;$window.Height=52
$view=[MusicIsland.IslandView]::new($window,"$PSScriptRoot\settings.json")
$view.Command=[Action[string,double]]{
 param($action,$value)
 if($action -eq 'volume' -or $action -eq 'config'){$shared.LocalCommands.Enqueue(@{Action=$action;Value=$value})}
 else{$shared.Commands.Enqueue(@{Action=$action;Value=$value})}
}
$view.SendConfig()
if($SelfTest){$view.Options.AutoCollapse=$false;$view.Diagnostics=$true;$view.IsHitTestVisible=$false;$window.Add_SourceInitialized({$view.SetClickThrough($true)})}
$tray=[Windows.Forms.NotifyIcon]::new();$tray.Icon=[Drawing.Icon]::ExtractAssociatedIcon((Join-Path $PSScriptRoot 'Music Island.exe'));$tray.Text='Music Island';$tray.Visible=$true
$trayMenu=[Windows.Forms.ContextMenuStrip]::new()
$null=$trayMenu.Items.Add('Показать / скрыть',$null,{if($window.IsVisible){$window.Hide()}else{$window.Show()}})
$null=$trayMenu.Items.Add('Вернуть управление мышью',$null,{$view.SetClickThrough($false);$window.Show()})
$null=$trayMenu.Items.Add('Настройки',$null,{$view.SetClickThrough($false);$window.Show();$view.ContextMenu.IsOpen=$true})
$null=$trayMenu.Items.Add('Выход',$null,{$window.Close()})
$tray.ContextMenuStrip=$trayMenu;$tray.Add_DoubleClick({if($window.IsVisible){$window.Hide()}else{$window.Show()}})
$timer=[Windows.Threading.DispatcherTimer]::new();$timer.Interval=[TimeSpan]::FromMilliseconds(100)
$script:ticks=0;$script:samples=[Collections.Generic.List[double]]::new()
$timer.Add_Tick({
 try {
  $script:ticks++
  if($script:ticks%10 -eq 0 -and (Test-Path -LiteralPath "$PSScriptRoot\exit.request")){$window.Close();return}
  $snapshot=$shared.Render
  if($snapshot){$view.Update($snapshot.Json,[byte[]]$snapshot.Art)}
  if($SelfTest){
   if($script:ticks -eq 10){$view.Capture("$PSScriptRoot\preview-compact.png");$view.Toggle()}
   if($script:ticks -ge 10 -and $script:ticks -le 20){$script:samples.Add($view.OpenAmount)}
   if($script:ticks -eq 40){
    $view.Capture("$PSScriptRoot\preview.png")
    @{Title=$snapshot.Title;ModelError=$shared.ModelError;SurfaceChanges=$view.SurfaceChanges;MediaError=$shared.Error;Width=$window.Width;Height=$window.Height;Left=$window.Left;Top=$window.Top;Scale=$view.Options.Scale;Lyric=$view.CurrentLine;Animation=@($script:samples.ToArray());FrameTimes=@($view.MotionTimes.ToArray());FrameValues=@($view.MotionValues.ToArray());PanelWidth=$view.PanelWidth;PanelHeight=$view.PanelHeight;ServerConnected=($null -ne $shared.Local)}|ConvertTo-Json -Depth 4|Set-Content -Encoding UTF8 "$PSScriptRoot\selftest.json"
    $window.Close()
   }
  }
 }catch{$_|Out-String|Add-Content -Encoding UTF8 "$PSScriptRoot\error.log";if($SelfTest){$window.Close()}}
})
$window.Add_Closed({$timer.Stop();$shared.Stop=$true;$tray.Visible=$false;$tray.Dispose()})
try{$app=[Windows.Application]::new();$app.ShutdownMode='OnMainWindowClose';$timer.Start();$null=$app.Run($window)}finally{$shared.Stop=$true;foreach($worker in $workers){$worker.Stop();$worker.Dispose()};$mutex.ReleaseMutex();$mutex.Dispose()}
