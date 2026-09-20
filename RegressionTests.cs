using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using MusicIsland;

class RegressionTests {
 [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h,int msg,IntPtr w,IntPtr l);
 [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h,int index);
 static Window window;static IslandView view;static Stopwatch clock=new Stopwatch();
 static JavaScriptSerializer json=new JavaScriptSerializer();static Model model;
 static HashSet<int> phases=new HashSet<int>();static List<string> failures=new List<string>(),commands=new List<string>();
 static double previousPosition=-1,maxBackstep,maxCenterError,maxLyricWidth;static int baseline,updates,toggles;static double nextToggle=1;
 static double Utc(){return DateTime.UtcNow.Subtract(new DateTime(1970,1,1)).TotalSeconds;}
 static void Check(bool value,string message){if(!value&&!failures.Contains(message))failures.Add(message);}
 static void Click(double px,double py){view.PointerDown(new Point(px,py));view.PointerUp(new Point(px,py));}
 [STAThread] static int Main(){
  try {
   window=new Window{Title="Music Island — regression test",WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,Topmost=true,ShowActivated=false,ShowInTaskbar=false,Width=300,Height=54};
   view=new IslandView(window,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-settings-unused.json"));view.Diagnostics=true;view.Options.AutoCollapse=false;view.Options.Scale=1;view.Options.Top=12;view.Options.X=50;view.Options.LyricsWidth=320;view.IsHitTestVisible=false;
   window.SourceInitialized+=delegate{view.SetClickThrough(true);};view.Command=(a,v)=>commands.Add(a+":"+v.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture));
   model=new Model{Key="fixture",Title="Проверка плавности — тестовый трек",Artist="Тест интерфейса",Playing=true,Play=true,Prev=true,Next=true,Seek=true,Duration=180,Position=30,SampleUtc=Utc(),LyricsPosition=30,LyricsSampleUtc=Utc(),LyricsLocal=true,LyricsIndex=0,Accent=new[]{82,174,206}};
   model.Lines=new[]{new Line{index=0,start=0,end=60,text="Это того стоит (skrrt), это того стоит (стоит)",words=new Word[0]}};
   view.Update(json.Serialize(model),null);
   var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(40)};
   timer.Tick+=delegate{try{Tick();}catch(Exception ex){failures.Add(ex.ToString());Finish();}};
   window.Closed+=delegate{timer.Stop();};window.Loaded+=delegate{clock.Start();timer.Start();};var app=new Application{ShutdownMode=ShutdownMode.OnMainWindowClose};app.Run(window);
   return failures.Count==0?0:1;
  }catch(Exception ex){File.WriteAllText("regression-error.txt",ex.ToString());return 2;}
 }
 static void Tick(){
  double t=clock.Elapsed.TotalSeconds;updates++;
  // Repeat an unchanged backend sample for 400ms. The frontend must not rewind each time.
  if(updates%10==0&&model.Playing){model.Position=30+t;model.SampleUtc=Utc();model.LyricsPosition=30+t;model.LyricsSampleUtc=model.SampleUtc;}
  view.Update(json.Serialize(model),null);
  if(t>.8&&baseline==0)baseline=view.SurfaceChanges;
  if(t>1&&t<7&&t>=nextToggle){view.Toggle();toggles++;nextToggle+=.36;}
  if(t>=7&&phases.Add(7)){if(!view.IsExpanded)view.Toggle();}
  if(t>=8&&phases.Add(8)){
   Check(Math.Abs(view.PanelWidth-380*SystemParameters.PrimaryScreenHeight/1080)<.1,"expanded width");
   Check(Math.Abs(view.PanelHeight-164*SystemParameters.PrimaryScreenHeight/1080)<.1,"expanded height");
   view.Capture("regression-expanded.png");
   double k=SystemParameters.PrimaryScreenHeight/1080,center=view.PanelBounds.X+view.PanelWidth/2,y=view.PanelHeight-27*k;
   Click(center-58*k,y);Click(center,y);Click(center+58*k,y);
   Check(commands.Count(a=>a.StartsWith("prev:"))==1&&commands.Count(a=>a.StartsWith("play:"))==1&&commands.Count(a=>a.StartsWith("next:"))==1,"media button routing");
   Check(view.IsExpanded,"media buttons collapse the island");
   Click(view.PanelBounds.X+16*k+(view.PanelWidth-32*k)*.5,view.PanelHeight-71*k);
   Check(commands.Any(a=>a=="seek:90.000"),"seek geometry / command");
   Check(view.IsExpanded,"seek collapses the island");
   model.Position=90;model.SampleUtc=Utc();previousPosition=-1;
  }
  if(t>=9&&phases.Add(9)){
   string text="Проверка длинной строки: подсветка каждого слова движется вместе с текстом, без прыжков и обрезанного начала";
   var words=text.Split(' ');double start=30+t;model.Lines=new[]{new Line{index=1,start=start,end=180,text=text,words=words.Select((word,i)=>new Word{start=start+i*.6,end=start+(i+1)*.6,text=word}).ToArray()}};model.LyricsIndex=1;
   // Independent lyric clock to test word reveal while the main media clock was seeked.
  }
  if(t>=8.2&&phases.Add(82)){model.Volume=.4;view.Update(json.Serialize(model),null);double k=SystemParameters.PrimaryScreenHeight/1080;view.PointerMove(new Point(view.PanelBounds.Right-36*k,view.PanelHeight-27*k));}
  if(t>=8.8&&phases.Add(88)){double k=SystemParameters.PrimaryScreenHeight/1080,px=view.PanelBounds.X+16*k+(view.PanelWidth-32*k)*.75,py=view.PanelHeight-58*k;Click(px,py);Check(commands.Any(a=>a=="volume:0.750"),"volume slider command / geometry");Check(view.IsExpanded,"volume slider collapses the island");model.Volume=-1;view.PointerMove(new Point(-100,-100));}
  if(t>=10&&phases.Add(10))view.Capture("regression-words.png");
  if(t>=12&&phases.Add(12)){
   view.Capture("regression-scrolled-words.png");Check(view.LyricScroll>0,"long lyric did not scroll");
   Check(view.CurrentLine.StartsWith("Проверка длинной"),"local indexed lyric selection");
   model.LyricsIndex=0; // Lua ignores a one-line backward glitch for one second only.
  }
  if(t>=12.2&&phases.Add(122)){model.LyricsIndex=1;}
  if(t>=13&&phases.Add(13)){model.Playing=false;model.Position=60;model.SampleUtc=Utc();model.LyricsSampleUtc=Utc();previousPosition=-1;}
  if(t>=13.5&&phases.Add(135)){double pos=view.PlaybackPosition;Check(Math.Abs(pos-60)<.05,"pause does not freeze progress");model.Play=false;view.Update(json.Serialize(model),null);int n=commands.Count;Click(view.PanelBounds.X+view.PanelWidth/2,view.PanelHeight-27*SystemParameters.PrimaryScreenHeight/1080);Check(commands.Count==n,"disabled media control dispatched");}
  if(t>=14&&phases.Add(14)){model.Playing=true;model.Play=true;model.Position=30+t;model.SampleUtc=Utc();model.LyricsIndex=2;model.Lines=new[]{new Line{index=2,start=0,end=180,text="Короткая строка",words=new Word[0]}};previousPosition=-1;}
  if(t>=14.7&&phases.Add(147)){view.Capture("regression-short-lyric.png");Check(view.LyricsBounds.Width<320*SystemParameters.PrimaryScreenHeight/1080,"short lyric pill not sized to text");}
  if(t>=15&&phases.Add(15)){model.Key="next";model.Title="Другой трек";model.LyricsIndex=-1;model.Lines=new Line[0];}
  if(t>=15.3&&phases.Add(153)){Check(view.CurrentLine==""&&view.LyricsBounds.Width==0,"stale lyric persists after track change");view.Toggle();}
  if(t>=16&&phases.Add(16)){Check(Math.Abs(view.PanelWidth-220*SystemParameters.PrimaryScreenHeight/1080)<.1,"compact width");view.Capture("regression-compact.png");model.Title="";model.Key="idle";model.Playing=false;}
  if(t>=17&&phases.Add(17)){
   Check(Math.Abs(view.PanelWidth-96*SystemParameters.PrimaryScreenHeight/1080)<.2,"idle clock width");model.Title="Тест";model.Key="again";view.Toggle();
   IntPtr handle=new System.Windows.Interop.WindowInteropHelper(window).Handle;
   Check((GetWindowLong(handle,-20)&0x08000000)!=0,"overlay can steal focus");Check(window.Topmost,"window is not topmost");
   SendMessage(handle,0x312,new IntPtr(1),IntPtr.Zero);Check(!window.IsVisible,"hotkey hide failed");SendMessage(handle,0x312,new IntPtr(1),IntPtr.Zero);Check(window.IsVisible,"hotkey show failed");
   Check(view.InputHitTest(new Point(1,window.Height-1))==null,"transparent surface captures clicks outside island");
  }
  if(t>=18){Finish();return;}
  if(t>.9&&t<15.9){Check(view.SurfaceChanges==baseline,"native window resized or moved during animation / lyric change");double center=view.PanelBounds.X+view.PanelBounds.Width/2+window.Left;maxCenterError=Math.Max(maxCenterError,Math.Abs(center-SystemParameters.PrimaryScreenWidth/2));Check(maxCenterError<.6,"island center drift");}
  if(t>1&&t<7){double pos=view.PlaybackPosition;if(previousPosition>=0)maxBackstep=Math.Max(maxBackstep,previousPosition-pos);previousPosition=pos;Check(maxBackstep<.02,"repeated backend sample rewinds progress");}
  maxLyricWidth=Math.Max(maxLyricWidth,view.LyricsBounds.Width);Check(maxLyricWidth<=320*SystemParameters.PrimaryScreenHeight/1080+.1,"lyric maximum width");
 }
 static void Finish(){
  if(!window.IsVisible)return;
  var frames=view.FrameDurations.Skip(5).OrderBy(x=>x).ToArray();
  File.WriteAllText("regression-results.json",json.Serialize(new{Passed=failures.Count==0,Failures=failures,ToggleCount=toggles,NativeSurfaceChangesDuringStress=view.SurfaceChanges-baseline,MaxCenterError=maxCenterError,MaxProgressBackstep=maxBackstep,FrameCount=frames.Length,MedianFrameMs=frames.Length==0?0:frames[frames.Length/2]*1000,P95FrameMs=frames.Length==0?0:frames[(int)(frames.Length*.95)]*1000,Commands=commands}));window.Close();
 }
}
