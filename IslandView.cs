using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;

namespace MusicIsland {
 public class Settings {
  public double Scale=1, X=50, Top=12, Sensitivity=1;
  public bool AutoCollapse=true, Clock=true, HideIdle=true, Bounce=true, Lyrics=true, LyricsInside=false, WordSync=true, Equalizer=true, Wave=false, AutoAccent=true, Blur=false;
  public int Bars=5, LyricsWidth=520;
  public string Accent="#7ABEFF";
 }
 public class Word { public double start,end; public string text; }
 public class Line { public double start,end; public int index=-1; public string text; public Word[] words; }
 public class Model {
  public string Key="",Title="",Artist="",Source="";
  public bool Playing, Seek, Play, Prev, Next;
  public double Position,Start,Duration,Volume=-1;
  public double[] Bands=new double[0];
  public int[] Accent;
  public Line[] Lines=new Line[0];
  public double LyricsPosition;
  public bool LyricsLocal;
  public double SampleUtc, LyricsSampleUtc;
  public int LyricsIndex=-1;
 }
 public class IslandView : FrameworkElement {
  [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int ht,uint flags);
  [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h,int n);
  [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h,int n,int v);
  [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h,int id,uint mod,uint key);
  [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h,int id);
  [DllImport("user32.dll")] static extern int SetWindowCompositionAttribute(IntPtr h,ref CompositionData d);
  [StructLayout(LayoutKind.Sequential)] struct AccentPolicy { public int State,Flags,Color,Animation; }
  [StructLayout(LayoutKind.Sequential)] struct CompositionData { public int Attribute; public IntPtr Data; public int Size; }
  Window host; IntPtr handle; HwndSource source; Settings settings; string settingsPath;
  Model model=new Model(); BitmapSource cover; string artKey="";
  readonly Stopwatch watch=Stopwatch.StartNew(); double lastFrame,lastUpdate,lastTouch,lastRaise,lastSwap,lyricChanged,transitionAt,frameDt;
  double positionShown,positionTarget,lyricPositionShown,lyricPositionTarget,lastMediaSample,lastLyricSample,lastLineChange;
  int lineIndex=-1; string lineIdentity=""; TimeSpan lastRenderingTime=TimeSpan.MinValue;
  double open,velocity,fill,compactWidth=220,extraHeight,pillWidth=40,pillFrom=40;
  bool target,through,dragSeek,dragVolume,mouseDown,registered;
  double seekAt,seekUntil,volumeUntil,volumeOpen,volumeLevel=-1,volumeSend;
  double ar=122,ag=190,ab=255; Color fallback=Color.FromRgb(122,190,255);
  double[] levels=new double[12]; double k=1,sy=1,w=220,h=38,x,lyricY,lyricW,lyricH;
  double paintW,paintH; Point mouse=new Point(-1000,-1000); Rect panelRect,lyricRect,seekRect,volumeRect;
  string oldTitle="",oldArtist="",oldLine="",currentLine=""; Line activeLine,previousLine;
  class Marquee { public string Key; public double Phase,Offset,Over,Raw,Calm,Updated; }
  Dictionary<string,Marquee> marquees=new Dictionary<string,Marquee>();
  Dictionary<string,double> measurements=new Dictionary<string,double>();
  readonly JavaScriptSerializer serializer=new JavaScriptSerializer();
  readonly Dictionary<string,FormattedText> textCache=new Dictionary<string,FormattedText>();
  readonly Dictionary<FormattedText,DrawingGroup> textDrawings=new Dictionary<FormattedText,DrawingGroup>();
  readonly Dictionary<Color,Brush> brushCache=new Dictionary<Color,Brush>();
  static readonly Typeface normalFace=new Typeface("Segoe UI");
  static readonly Typeface semiFace=new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal);
  static readonly Typeface boldFace=new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,FontWeights.Bold,FontStretches.Normal);
  string lastJson;
  public bool Diagnostics;
  public int SurfaceChanges;
  public List<double> FrameDurations=new List<double>();
  public Action<string,double> Command;
  public Action Changed;
  public Settings Options {get{return settings;}}
  public string CurrentLine {get{return currentLine;}}
  public double OpenAmount {get{return open;}}
  public bool IsExpanded {get{return target;}}
  public bool ClickThrough {get{return through;}}
  public List<double> MotionTimes=new List<double>();
  public List<double> MotionValues=new List<double>();
  public double PanelWidth {get{return w;}}
  public double PanelHeight {get{return h;}}
  public double PlaybackPosition {get{return Position;}}
  public double LyricScroll {get;private set;}
  public Rect PanelBounds {get{return panelRect;}}
  public Rect LyricsBounds {get{return lyricRect;}}
  public IslandView(Window window,string path) {
   host=window; settingsPath=path; settings=new Settings();
   try { if(File.Exists(path)) settings=new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(path)); } catch {}
   settings.Scale=Clamp(settings.Scale,.7,1.5);settings.Bars=(int)Clamp(settings.Bars,3,12);
   settings.X=Clamp(settings.X,0,100);settings.Top=Clamp(settings.Top,0,400);
   settings.LyricsWidth=(int)Clamp(settings.LyricsWidth,320,900);
   Focusable=false; host.Content=this;
   host.SourceInitialized+=delegate {
    handle=new WindowInteropHelper(host).Handle;
    SetWindowLong(handle,-20,GetWindowLong(handle,-20)|0x08000080);
    source=HwndSource.FromHwnd(handle);source.AddHook(Hook);
    registered=RegisterHotKey(handle,1,0x4003,0x49); ApplyBlur();
   };
   host.Closed+=delegate {CompositionTarget.Rendering-=Frame;if(registered)UnregisterHotKey(handle,1);if(source!=null)source.RemoveHook(Hook);};
   host.IsVisibleChanged+=delegate {lastFrame=watch.Elapsed.TotalSeconds;};
   CompositionTarget.Rendering+=Frame;
   MouseMove+=Move; MouseLeave+=delegate {if(!IsMouseCaptured)mouse=new Point(-1000,-1000);};
   MouseLeftButtonDown+=Down;MouseLeftButtonUp+=Up;
   MouseRightButtonUp+=delegate {BuildMenu();ContextMenu.IsOpen=true;};
   MouseWheel+=delegate(object o,MouseWheelEventArgs e){if(model.Volume>=0){volumeLevel=Clamp((volumeLevel<0?model.Volume:volumeLevel)+e.Delta/120.0*.05,0,1);Send("volume",volumeLevel);volumeUntil=watch.Elapsed.TotalSeconds+3;}e.Handled=true;};
   BuildMenu();
  }
  IntPtr Hook(IntPtr hwnd,int msg,IntPtr wp,IntPtr lp,ref bool handled){
   if(msg==0x312&&wp.ToInt32()==1){if(host.IsVisible)host.Hide();else host.Show();handled=true;}
   if(msg==0x21){handled=true;return new IntPtr(3);} // MA_NOACTIVATE
   return IntPtr.Zero;
  }
  public void SetClickThrough(bool value){through=value;if(handle!=IntPtr.Zero){int style=GetWindowLong(handle,-20);SetWindowLong(handle,-20,value?style|0x20:style&~0x20);}BuildMenu();}
  void Save(){try{File.WriteAllText(settingsPath,new JavaScriptSerializer().Serialize(settings));}catch{}if(Changed!=null)Changed();}
  void ApplyBlur(){
   if(handle==IntPtr.Zero)return;
   try{AccentPolicy p=new AccentPolicy{State=settings.Blur?3:0};int size=Marshal.SizeOf(p);IntPtr ptr=Marshal.AllocHGlobal(size);try{Marshal.StructureToPtr(p,ptr,false);CompositionData data=new CompositionData{Attribute=19,Data=ptr,Size=size};SetWindowCompositionAttribute(handle,ref data);}finally{Marshal.FreeHGlobal(ptr);}}catch{}
  }
  MenuItem Item(string label,Action action){var m=new MenuItem{Header=label};m.Click+=delegate{action();};return m;}
  MenuItem Check(string label,bool value,Action<bool> action){var m=new MenuItem{Header=label,IsCheckable=true,IsChecked=value};m.Click+=delegate{action(m.IsChecked);Save();};return m;}
  MenuItem Choices(string title,string[] labels,Action<int> choose){var m=new MenuItem{Header=title};for(int i=0;i<labels.Length;i++){int j=i;m.Items.Add(Item(labels[i],delegate{choose(j);Save();}));}return m;}
  void BuildMenu(){
   var menu=new ContextMenu();menu.Items.Add(Item("Развернуть / свернуть",Toggle));
   menu.Items.Add(Check("Автосворачивание через 6 секунд",settings.AutoCollapse,v=>settings.AutoCollapse=v));
   menu.Items.Add(Check("Пружинка раскрытия",settings.Bounce,v=>settings.Bounce=v));
   menu.Items.Add(Check("Часы без музыки",settings.Clock,v=>settings.Clock=v));
   menu.Items.Add(Check("Скрывать без музыки",settings.HideIdle,v=>settings.HideIdle=v));
   menu.Items.Add(Check("Размытый фон",settings.Blur,v=>{settings.Blur=v;ApplyBlur();}));
   menu.Items.Add(Choices("Масштаб",new[]{"70%","85%","100% — как в Lua","115%","130%","150%"},i=>settings.Scale=new[]{.7,.85,1,1.15,1.3,1.5}[i]));
   menu.Items.Add(Choices("Отступ сверху",new[]{"0","6","12 — как в Lua","24","48","96"},i=>settings.Top=new[]{0,6,12,24,48,96}[i]));
   menu.Items.Add(Choices("Положение по горизонтали",new[]{"25%","40%","50% — центр","60%","75%"},i=>settings.X=new[]{25,40,50,60,75}[i]));
   menu.Items.Add(new Separator());
   menu.Items.Add(Check("Цвет из обложки",settings.AutoAccent,v=>settings.AutoAccent=v));
   menu.Items.Add(Item("Выбрать цвет…",delegate{using(var d=new System.Windows.Forms.ColorDialog()){if(d.ShowDialog()==System.Windows.Forms.DialogResult.OK){settings.Accent=String.Format("#{0:X2}{1:X2}{2:X2}",d.Color.R,d.Color.G,d.Color.B);settings.AutoAccent=false;Save();}}}));
   menu.Items.Add(Check("Текст песни",settings.Lyrics,v=>settings.Lyrics=v));
   menu.Items.Add(Check("Текст внутри острова",settings.LyricsInside,v=>settings.LyricsInside=v));
   menu.Items.Add(Check("Подсветка по словам",settings.WordSync,v=>settings.WordSync=v));
   menu.Items.Add(Choices("Максимальная ширина текста",new[]{"320","420","520 — как в Lua","700","900"},i=>settings.LyricsWidth=new[]{320,420,520,700,900}[i]));
   menu.Items.Add(Check("Эквалайзер",settings.Equalizer,v=>{settings.Equalizer=v;SendConfig();}));
   menu.Items.Add(Check("Эквалайзер волной",settings.Wave,v=>settings.Wave=v));
   menu.Items.Add(Choices("Количество полос",new[]{"3","4","5","6","8","10","12"},i=>{settings.Bars=new[]{3,4,5,6,8,10,12}[i];SendConfig();}));
   menu.Items.Add(Choices("Чувствительность",new[]{"0.5×","1×","1.5×","2×"},i=>settings.Sensitivity=new[]{.5,1,1.5,2}[i]));
   menu.Items.Add(new Separator());
   menu.Items.Add(Check("Пропускать клики",through,SetClickThrough));
   menu.Items.Add(Item("Скрыть / показать: Ctrl+Alt+I",delegate{host.Hide();}));
   menu.Items.Add(Item("Выход",delegate{host.Close();}));ContextMenu=menu;
  }
  public void SendConfig(){Send("config",settings.Equalizer?settings.Bars:0);}
  public void Update(string json,byte[] bytes){
   if(String.Equals(json,lastJson,StringComparison.Ordinal))return;
   Model next;try{next=serializer.Deserialize<Model>(json);}catch{return;}
   if(next==null)return;
   lastJson=json;
   bool changed=next.Key!=model.Key;
   if(changed){oldTitle=model.Title;oldArtist=model.Artist;lastSwap=watch.Elapsed.TotalSeconds;marquees.Clear();measurements.Clear();lineIndex=-1;lineIdentity="";currentLine=oldLine="";activeLine=previousLine=null;lastMediaSample=lastLyricSample=0;}
   double utc=DateTime.UtcNow.Subtract(new DateTime(1970,1,1)).TotalSeconds;
   if(changed||next.SampleUtc!=lastMediaSample||next.Playing!=model.Playing){
    double p=next.Position+(next.Playing&&next.SampleUtc>0?Math.Max(0,utc-next.SampleUtc):0);
    if(!dragSeek&&(watch.Elapsed.TotalSeconds>=seekUntil||Math.Abs(p-seekAt)<=2.5)){if(Math.Abs(p-seekAt)<=2.5)seekUntil=0;positionTarget=p;if(changed||Math.Abs(p-positionShown)>1)positionShown=p;}
    lastMediaSample=next.SampleUtc;
   }
   if(changed||next.LyricsSampleUtc!=lastLyricSample){double p=next.LyricsPosition+(next.Playing&&next.LyricsSampleUtc>0?Math.Max(0,utc-next.LyricsSampleUtc):0);lyricPositionTarget=p;if(changed||Math.Abs(p-lyricPositionShown)>1)lyricPositionShown=p;lastLyricSample=next.LyricsSampleUtc;}
   model=next;lastUpdate=watch.Elapsed.TotalSeconds;
   if(!dragVolume&&lastUpdate>volumeUntil)volumeLevel=model.Volume;
   if(artKey!=model.Key){artKey=model.Key;cover=null;
    if(bytes!=null&&bytes.Length>0)try{using(var ms=new MemoryStream(bytes)){var b=new BitmapImage();b.BeginInit();b.CacheOption=BitmapCacheOption.OnLoad;b.StreamSource=ms;b.EndInit();b.Freeze();cover=b;fallback=ExtractColor(b);}}catch{}
   }
  }
  static Color ExtractColor(BitmapSource b){
   var small=new TransformedBitmap(b,new ScaleTransform(16.0/b.PixelWidth,16.0/b.PixelHeight));var bg=new FormatConvertedBitmap(small,PixelFormats.Bgra32,null,0);
   byte[] pixels=new byte[bg.PixelWidth*bg.PixelHeight*4];bg.CopyPixels(pixels,bg.PixelWidth*4,0);double r=0,g=0,bl=0,total=0;
   for(int i=0;i<pixels.Length;i+=4){double hi=Math.Max(pixels[i],Math.Max(pixels[i+1],pixels[i+2])),lo=Math.Min(pixels[i],Math.Min(pixels[i+1],pixels[i+2]));if(hi<40||lo>235)continue;double weight=1+(hi-lo)/30;r+=pixels[i+2]*weight;g+=pixels[i+1]*weight;bl+=pixels[i]*weight;total+=weight;}
   if(total==0)return Color.FromRgb(122,190,255);return Color.FromRgb((byte)(r/total),(byte)(g/total),(byte)(bl/total));
  }
  public void Toggle(){target=!target;lastTouch=transitionAt=watch.Elapsed.TotalSeconds;MotionTimes.Clear();MotionValues.Clear();}
  void Send(string action,double value){if(Command!=null)Command(action,value);lastTouch=watch.Elapsed.TotalSeconds;}
  static double Clamp(double v,double a,double b){return Math.Max(a,Math.Min(b,v));}
  static double Ease(double v){v=Clamp(v,0,1);return 1-Math.Pow(1-v,3);}
  static double Approach(double a,double b,double rate,double dt){return a+(b-a)*(1-Math.Exp(-rate*dt));}
  double Position {get{return dragSeek?seekAt:Clamp(positionShown,model.Start,Math.Max(model.Start,model.Duration));}}
  double LyricPosition {get{return model.LyricsLocal?lyricPositionShown:Position;}}
  void Frame(object sender,EventArgs args){
   if(!host.IsVisible)return;
   var rendering=args as RenderingEventArgs;if(rendering!=null){if(rendering.RenderingTime==lastRenderingTime)return;lastRenderingTime=rendering.RenderingTime;}
   double now=watch.Elapsed.TotalSeconds,elapsed=now-lastFrame;if(elapsed<1.0/240)return;double dt=Math.Min(elapsed,.1);lastFrame=now;frameDt=dt;
   if(Diagnostics&&FrameDurations.Count<20000)FrameDurations.Add(elapsed);
   if(model.Playing){positionShown+=elapsed;positionTarget+=elapsed;lyricPositionShown+=elapsed;lyricPositionTarget+=elapsed;}
   positionShown+=(positionTarget-positionShown)*Math.Min(1,3*dt);lyricPositionShown+=(lyricPositionTarget-lyricPositionShown)*Math.Min(1,3*dt);
   bool idle=String.IsNullOrEmpty(model.Title);
   if(settings.AutoCollapse&&target&&!IsMouseOver&&now-lastTouch>6)target=false;
   if(ContextMenu!=null&&ContextMenu.IsOpen)lastTouch=now;
   double stiffness=settings.Bounce?320:380,damping=settings.Bounce?24:40;
   double remaining=dt;while(remaining>0){double step=Math.Min(remaining,1.0/240);velocity+=(((target?1:0)-open)*stiffness-velocity*damping)*step;open=Clamp(open+velocity*step,-.12,1.14);remaining-=step;}
   if(Math.Abs(open-(target?1:0))<.0001&&Math.Abs(velocity)<.002){open=target?1:0;velocity=0;}
   if(Diagnostics&&now-transitionAt<.8&&MotionTimes.Count<240){MotionTimes.Add(now-transitionAt);MotionValues.Add(open);}
   double reach=Clamp(open,0,1);fill=target?Math.Max(fill,reach):Math.Min(fill,reach);
   compactWidth=Approach(compactWidth,idle&&settings.Clock?96:220,11,dt);
   Line previousActive=activeLine,candidate=null;double at=LyricPosition;
   int wantedIndex=model.LyricsIndex;
   if(model.LyricsLocal&&wantedIndex==lineIndex-1&&now-lastLineChange<1)wantedIndex=lineIndex;
   if(settings.Lyrics&&model.Lines!=null)foreach(var line in model.Lines)if(model.LyricsLocal?line.index==wantedIndex&&wantedIndex>=0:at>=line.start&&at<line.end){candidate=line;break;}
   activeLine=candidate;
   string text=activeLine==null?"":activeLine.text??"";
   if(activeLine!=null&&activeLine.words!=null&&activeLine.words.Length>0)text=String.Join(" ",activeLine.words.Select(word=>word.text));
   string identity=activeLine==null?"":activeLine.start.ToString("R",CultureInfo.InvariantCulture)+"|"+text;
   if(identity!=lineIdentity){previousLine=previousActive;oldLine=currentLine;currentLine=text;lyricChanged=lastLineChange=now;pillFrom=pillWidth;lineIdentity=identity;lineIndex=activeLine==null?-1:activeLine.index;}
   extraHeight=Approach(extraHeight,settings.LyricsInside&&currentLine.Length>0?34:0,9,dt);
   sy=SystemParameters.PrimaryScreenHeight/1080.0;k=sy*settings.Scale;
   w=(compactWidth+(380-compactWidth)*fill)*k;h=(38+(164+extraHeight-38)*fill)*k;
   paintW=(compactWidth+(380-compactWidth)*open)*k;paintH=(38+(164+extraHeight-38)*open)*k;
   double maxLyric=settings.LyricsWidth*sy;
   double wanted=currentLine.Length==0?0:Clamp(Measure(currentLine,14*k,true),20*k,Math.Max(20*k,maxLyric-28*k));
   pillWidth=pillFrom+(wanted-pillFrom)*Ease((now-lyricChanged)/.28);
   lyricW=currentLine.Length>0&&!settings.LyricsInside?(pillWidth+28*k):0;lyricH=30*k;
   // Keep the layered HWND fixed: resizing/repositioning it during each spring or lyric frame
   // makes DWM resize its surface and causes visible shaking. Only the drawing changes.
   double width=Math.Ceiling(Math.Max(440*k,settings.LyricsWidth*sy)+8*k),height=Math.Ceiling(280*k);
   double dpi=source==null?1:source.CompositionTarget.TransformToDevice.M11;
   width=Math.Min(width,SystemParameters.PrimaryScreenWidth);
   double left=Math.Round(Clamp(SystemParameters.PrimaryScreenWidth*settings.X/100-width/2,0,SystemParameters.PrimaryScreenWidth-width)*dpi)/dpi,top=Math.Round((settings.Top*sy+2/dpi)*dpi)/dpi;
   if(host.Width!=width||host.Height!=height||host.Left!=left||host.Top!=top){host.Width=width;host.Height=height;host.Left=left;host.Top=top;SurfaceChanges++;}
   double center=SystemParameters.PrimaryScreenWidth*settings.X/100-left;
   x=Clamp(center-w/2,4/dpi,width-w-4/dpi);lyricY=h+8*k;
   panelRect=new Rect(x,0,w,h);lyricRect=new Rect(Clamp(x+w/2-lyricW/2,4/dpi,width-lyricW-4/dpi),lyricY,Math.Max(0,lyricW),lyricH);
   Color accent=fallback;
   if(settings.AutoAccent&&model.Accent!=null&&model.Accent.Length>=3)accent=Color.FromRgb((byte)model.Accent[0],(byte)model.Accent[1],(byte)model.Accent[2]);
   if(!settings.AutoAccent)try{accent=(Color)ColorConverter.ConvertFromString(settings.Accent);}catch{}
   double peak=Math.Max(accent.R,Math.Max(accent.G,accent.B)),boost=peak>0&&peak<140?140/peak:1;
   ar=Approach(ar,accent.R*boost,6,dt);ag=Approach(ag,accent.G*boost,6,dt);ab=Approach(ab,accent.B*boost,6,dt);
   for(int i=0;i<levels.Length;i++){double level=!settings.Equalizer||!model.Playing?0:Clamp(model.Bands!=null&&i<model.Bands.Length?model.Bands[i]*settings.Sensitivity:0,0,1);levels[i]=Approach(levels[i],level,level>levels[i]?34:9,dt);}
   bool volumeHover=new Rect(x+w-56*k,h-47*k,40*k,40*k).Contains(mouse);
   bool overControls=new Rect(x+10*k,h-74*k,Math.Max(1,w-20*k),74*k).Contains(mouse);
   volumeOpen=Approach(volumeOpen,model.Volume>=0&&(dragVolume||now<volumeUntil||volumeHover||(volumeOpen>.25&&overControls))&&!dragSeek?1:0,16,dt);
   if(fill<.37){volumeOpen=0;dragVolume=false;}
   Opacity=idle&&!settings.Clock&&settings.HideIdle?0:1;
   if(now-lastRaise>1&&handle!=IntPtr.Zero){SetWindowPos(handle,new IntPtr(-1),0,0,0,0,0x13);lastRaise=now;}
   InvalidateVisual();
  }
  Brush B(double r,double g,double b,double a){Color color=Color.FromArgb((byte)Clamp(a,0,255),(byte)Clamp(r,0,255),(byte)Clamp(g,0,255),(byte)Clamp(b,0,255));Brush cached;if(brushCache.TryGetValue(color,out cached))return cached;var brush=new SolidColorBrush(color);brush.Freeze();if(brushCache.Count>=512)brushCache.Clear();brushCache[color]=brush;return brush;}
  Brush Accent(double alpha){return B(ar,ag,ab,255*alpha);}
  FormattedText FT(string text,double size,bool bold,Brush brush){
   double dpi=source==null?1:source.CompositionTarget.TransformToDevice.M11;
   int weight=bold?(size>=15.9*k?2:1):0;
   string key=size.ToString("R",CultureInfo.InvariantCulture)+"|"+dpi.ToString("R",CultureInfo.InvariantCulture)+"|"+weight+"|"+text;
   FormattedText result;if(textCache.TryGetValue(key,out result))return result;
   result=new FormattedText(text??"",CultureInfo.CurrentCulture,FlowDirection.LeftToRight,weight==2?boldFace:weight==1?semiFace:normalFace,size,Brushes.White,dpi);
   if(textCache.Count>=512){textCache.Clear();textDrawings.Clear();}textCache[key]=result;return result;
  }
  double Measure(string text,double size,bool bold){string key=size.ToString("R",CultureInfo.InvariantCulture)+"|"+bold+"|"+text;double value;if(measurements.TryGetValue(key,out value))return value;value=FT(text,size,bold,Brushes.White).WidthIncludingTrailingWhitespace;if(measurements.Count>=800)measurements.Clear();measurements[key]=value;return value;}
  void Text(DrawingContext d,string text,double px,double py,double size,bool bold,Brush brush){
   // All labels are white; apply their fade separately so animation reuses glyph layout.
   var formatted=FT(text,size,bold,brush);DrawingGroup drawing;
   if(!textDrawings.TryGetValue(formatted,out drawing)){drawing=new DrawingGroup();using(var context=drawing.Open())context.DrawText(formatted,new Point(0,0));drawing.Freeze();textDrawings[formatted]=drawing;}
   var solid=brush as SolidColorBrush;d.PushOpacity(solid==null?1:solid.Color.A/255.0);d.PushTransform(new TranslateTransform(px,py));d.DrawDrawing(drawing);d.Pop();d.Pop();
  }
  void Panel(DrawingContext d,Rect rect,double radius,double opacity){
   d.DrawRoundedRectangle(B(settings.Blur?14:10,settings.Blur?14:10,settings.Blur?18:12,(settings.Blur?150:238)*opacity),null,rect,radius,radius);
   var gradient=new LinearGradientBrush(Color.FromArgb((byte)(30*opacity),(byte)ar,(byte)ag,(byte)ab),Color.FromArgb(0,(byte)ar,(byte)ag,(byte)ab),new Point(0,0),new Point(0,1));gradient.Freeze();d.DrawRoundedRectangle(gradient,null,rect,radius,radius);
  }
  static double Ramp(double t,double ease){t=Clamp(t,0,1);double e=Clamp(ease,.02,.5),span=1-e;if(t<e)return t*t/(2*e*span);if(t>1-e){double rest=1-t;return 1-rest*rest/(2*e*span);}return (t-e*.5)/span;}
  double MarqueeOffset(string id,string text,double overflow,bool advance){
   double over=Math.Max(0,overflow);Marquee m;
   if(!marquees.TryGetValue(id,out m)||m.Key!=text){m=new Marquee{Key=text,Over=over,Raw=over};marquees[id]=m;}
   if(!advance||m.Updated==lastFrame)return m.Offset;m.Updated=lastFrame;
   double step=Math.Abs(over-m.Raw);m.Raw=over;m.Over=Approach(m.Over,over,9,frameDt);if(Math.Abs(m.Over-over)<.2)m.Over=over;
   bool live=over>5*k&&step<1.6;m.Calm=Approach(m.Calm,live?1:0,live?4:12,frameDt);if(m.Calm<.02)m.Phase=0;
   double goal=0;if(m.Over>.5){double travel=m.Over/Math.Max(1,34*k),period=(1.15+travel)*2,ease=Clamp(.14/Math.Max(travel,.05),.03,.2);m.Phase=(m.Phase+frameDt*m.Calm/period)%1;double t=m.Phase*period;
    if(t<1.15)goal=0;else if(t<1.15+travel)goal=m.Over*Ramp((t-1.15)/travel,ease);else if(t<2.3+travel)goal=m.Over;else goal=m.Over*(1-Ramp((t-2.3-travel)/travel,ease));goal=Math.Min(goal*m.Calm,over);}
   m.Offset=Approach(m.Offset,goal,16,frameDt);if(Math.Abs(m.Offset-goal)<.12)m.Offset=goal;return m.Offset;
  }
  void PaintFaded(DrawingContext d,double px,double py,double width,double size,double off,double over,Action paint){
   double edge=Math.Min(16*k,width*.35),left=edge*Clamp(off/Math.Max(.001,edge),0,1),right=edge*Clamp((over-off)/Math.Max(.001,edge),0,1);left=Math.Min(left,width*.45);right=Math.Min(right,width*.45);
   Action<double,double,double> slice=(a,b,alpha)=>{if(b-a<=.25)return;d.PushClip(new RectangleGeometry(new Rect(a,py-size*.35,b-a,size*1.9)));d.PushOpacity(alpha);paint();d.Pop();d.Pop();};
   slice(px+left,px+width-right,1);
   for(int i=1;i<=6;i++){double a=(i-1)/6.0,b=i/6.0,t=(a+b)/2;if(left>.25)slice(px+left*a,px+left*b,t);if(right>.25)slice(px+width-right*b,px+width-right*a,t);}
  }
  void ScrollText(DrawingContext d,string id,string text,double px,double py,double width,double size,bool bold,Brush brush){
   if(String.IsNullOrEmpty(text)||width<=0)return;double over=Math.Max(0,Measure(text,size,bold)-width),off=MarqueeOffset(id,text,over,true);
   PaintFaded(d,px,py,width,size,off,over,()=>Text(d,text,px-off,py,size,bold,brush));
  }
  static double Smooth(double t){t=Clamp(t,0,1);return t*t*(3-2*t);}
  void Eq(DrawingContext d,double px,double py,double width,double height,double alpha){
   if(!settings.Equalizer)return;int n=settings.Bars;
   if(settings.Wave){
    var geometry=new StreamGeometry();using(var c=geometry.Open()){c.BeginFigure(new Point(px,py+height-1*k),false,false);for(int j=0;j<=n*8;j++){double fi=(double)j/8,level=0;int i=(int)fi;double t=fi-i;double a=i==0?0:levels[Math.Min(n-1,i-1)],b=i>=n?0:levels[i];level=a+(b-a)*Smooth(t);c.LineTo(new Point(px+width*j/(n*8),py+height-2*k-level*(height-2*k)),true,false);}}geometry.Freeze();d.DrawGeometry(null,new Pen(Accent(alpha),2*k),geometry);
   }else{double pitch=width/n,bw=Math.Max(2*k,pitch*.56);for(int i=0;i<n;i++){double bh=Math.Max(bw,height*levels[i]);d.DrawRoundedRectangle(Accent(alpha*.95),null,new Rect(px+i*pitch+(pitch-bw)/2,py+(height-bh)/2,bw,bh),bw/2,bw/2);}}
  }
  void Cover(DrawingContext d,double px,double py,double size,double radius,double opacity){
   d.PushOpacity(opacity);var rect=new Rect(px,py,size,size);var clip=new RectangleGeometry(rect,radius,radius);d.PushClip(clip);
   if(cover!=null){double ratio=Math.Max(size/cover.PixelWidth,size/cover.PixelHeight);double iw=cover.PixelWidth*ratio,ih=cover.PixelHeight*ratio;d.DrawImage(cover,new Rect(px+(size-iw)/2,py+(size-ih)/2,iw,ih));}
   else{d.DrawRectangle(new LinearGradientBrush(Color.FromArgb(215,(byte)ar,(byte)ag,(byte)ab),Color.FromArgb(215,(byte)(ar*.42),(byte)(ag*.42),(byte)(ab*.42)),90),null,rect);d.DrawEllipse(null,new Pen(B(255,255,255,46),Math.Max(1,size*.045)),new Point(px+size/2,py+size/2),size*.3,size*.3);d.DrawEllipse(B(255,255,255,70),null,new Point(px+size/2,py+size/2),size*.09,size*.09);}
   d.Pop();d.DrawRoundedRectangle(null,new Pen(B(255,255,255,25),k*.7),rect,radius,radius);d.Pop();
  }
  void Track(DrawingContext d,double px,double py,double width,double height,double level,double opacity){d.DrawRoundedRectangle(B(255,255,255,38*opacity),null,new Rect(px,py,width,height),height/2,height/2);if(level>0)d.DrawRoundedRectangle(Accent(opacity),null,new Rect(px,py,width*Clamp(level,0,1),height),height/2,height/2);}
  string Time(double s){int n=(int)Math.Max(0,Math.Floor(s+.5));return (n/60)+":"+(n%60).ToString("00");}
  void Glyph(DrawingContext d,string id,double cx,double cy,double size,Brush color){
   if(id=="pause"){double bw=size*.27;d.DrawRoundedRectangle(color,null,new Rect(cx-size*.38,cy-size*.58,bw,size*1.16),bw*.35,bw*.35);d.DrawRoundedRectangle(color,null,new Rect(cx+size*.11,cy-size*.58,bw,size*1.16),bw*.35,bw*.35);return;}
   if(id=="vol"){var geo=Geometry.Parse("M 0,5 L 4,5 L 10,0 L 10,18 L 4,13 L 0,13 Z M 13,4 Q 18,9 13,14");d.PushTransform(new TranslateTransform(cx-size*.45,cy-size*.45));d.PushTransform(new ScaleTransform(size/19,size/19));d.DrawGeometry(color,new Pen(color,1),geo);d.Pop();d.Pop();return;}
   double direction=id=="prev"?-1:1;var g=new StreamGeometry();using(var c=g.Open()){c.BeginFigure(new Point(cx-direction*size*.40,cy-size*.58),true,true);c.LineTo(new Point(cx-direction*size*.40,cy+size*.58),true,false);c.LineTo(new Point(cx+direction*size*.55,cy),true,false);}g.Freeze();d.DrawGeometry(color,null,g);if(id=="prev"||id=="next")d.DrawRoundedRectangle(color,null,new Rect(cx+direction*size*.74-size*.09,cy-size*.66,size*.18,size*1.32),size*.09,size*.09);
  }
  protected override void OnRender(DrawingContext d){
   double now=watch.Elapsed.TotalSeconds,compactA=Clamp(1.06-fill*2.8,0,1),expandedA=Clamp((fill-.36)/.44,0,1);
   bool idle=String.IsNullOrEmpty(model.Title);Panel(d,new Rect(x+w/2-paintW/2,0,Math.Max(1,paintW),Math.Max(1,paintH)),(19+9*Clamp(open,0,1))*k,1);
   d.PushClip(new RectangleGeometry(panelRect,(19+9*fill)*k,(19+9*fill)*k));
   if(!idle||expandedA>.01){double pad=(4+12*fill)*k,size=(30+42*fill)*k;double pulse=1+.06*Ease(Clamp(1-(now-lastSwap)/.45,0,1));double grow=size*(pulse-1)/2;Cover(d,x+pad-grow,pad-grow,size*pulse,(10+6*fill)*k,1);}
   if(compactA>.01){d.PushOpacity(compactA);if(idle&&settings.Clock){string clock=DateTime.Now.ToString("HH:mm");Text(d,clock,x+(w-Measure(clock,15*k,true))/2,(38*k-19*k)/2,15*k,true,B(255,255,255,234));}else{double size=(30+42*fill)*k,pad=(4+12*fill)*k,eqw=settings.Equalizer?46*k:0,eqx=x+compactWidth*k-4*k-eqw,tx=x+pad+size+10*k;Eq(d,eqx,pad+(size-38*k*.46)/2,eqw,38*k*.46,1);ScrollText(d,"compact",model.Title,tx,pad+(size-15.6*k)/2,eqx-10*k-tx,12.5*k,true,B(255,255,255,235));}d.Pop();}
   if(expandedA>.01){d.PushOpacity(expandedA);double right=x+w-16*k,tx=x+102*k,box=Math.Max(20*k,right-tx-52*k),swap=Ease((now-lastSwap)/.34);
    if(swap<1){ScrollText(d,"oldTitle",oldTitle,tx,22*k-14*k*swap,box,16*k,true,B(255,255,255,235*(1-swap)));ScrollText(d,"oldArtist",oldArtist,tx,45*k-14*k*swap,box,13*k,false,B(255,255,255,150*(1-swap)));}
    ScrollText(d,"title",idle?"Нет трека":model.Title,tx,22*k+14*k*(1-swap),box,16*k,true,B(255,255,255,242*swap));ScrollText(d,"artist",model.Artist,tx,45*k+14*k*(1-swap),box,13*k,false,B(255,255,255,155*swap));Eq(d,right-42*k,24*k,42*k,18*k,.9);
    if(settings.LyricsInside&&extraHeight>1){DrawLyric(d,activeLine,currentLine,tx,68*k,right-tx,13*k,1);if(activeLine!=null&&model.Lines!=null){int index=Array.IndexOf(model.Lines,activeLine);if(index>=0&&index+1<model.Lines.Length){var next=model.Lines[index+1];DrawLyric(d,next,next.text,tx,(68+13*1.55)*k,right-tx,13*k,.6);}}}
    double barX=x+16*k,span=w-32*k,barY=h-74*k,pos=dragSeek?seekAt:Position,length=model.Duration-model.Start,done=length>0?(pos-model.Start)/length:0;
    Track(d,barX,barY,span,5*k,done,1);seekRect=new Rect(barX,barY-9*k,span,23*k);
    if(model.Seek&&(dragSeek||seekRect.Contains(mouse)))d.DrawEllipse(B(255,255,255,238),null,new Point(barX+span*Clamp(done,0,1),barY+2.5*k),(dragSeek?6.5:5)*k,(dragSeek?6.5:5)*k);
    double v=Ease(volumeOpen);if(v<.99){Text(d,Time(pos-model.Start),barX,h-64*k,11*k,false,B(255,255,255,140*(1-v)));string remain=length>0?"-"+Time(model.Duration-pos):"—";Text(d,remain,right-Measure(remain,11*k,false),h-64*k,11*k,false,B(255,255,255,140*(1-v)));}
    if(model.Volume>=0){volumeRect=new Rect(barX,h-66*k,span,17*k);if(v>.01){Track(d,right-span*v,h-60*k,span*v,5*k,volumeLevel,v);d.DrawEllipse(B(255,255,255,238*v),null,new Point(right-span*v+span*v*volumeLevel,h-57.5*k),5*k,5*k);}Glyph(d,"vol",right-20*k,h-27*k,19*k,v>.5?Accent(1):B(255,255,255,214));}
    for(int i=0;i<3;i++){double cx=x+w*.5+(i-1)*58*k,cy=h-27*k;bool hover=new Rect(cx-20*k,cy-20*k,40*k,40*k).Contains(mouse);if(hover)d.DrawEllipse(B(255,255,255,22),null,new Point(cx,cy),16.8*k,16.8*k);Glyph(d,i==0?"prev":i==2?"next":model.Playing?"pause":"play",cx,cy,(i==1?16:13)*k,B(255,255,255,hover?255:214));}d.Pop();
   }
   d.Pop();
   if(settings.Lyrics&&!settings.LyricsInside&&lyricW>0){double t=Ease((now-lyricChanged)/.28);Panel(d,lyricRect,lyricH/2,.4+.6*t);d.PushClip(new RectangleGeometry(lyricRect,lyricH/2,lyricH/2));double py=lyricY+(30-17.5)*k/2;if(t<1)DrawLyric(d,previousLine,oldLine,lyricRect.X+14*k,py-14*k*.8*t,pillWidth,14*k,1-t,false);DrawLyric(d,activeLine,currentLine,lyricRect.X+14*k,py+14*k*.8*(1-t),pillWidth,14*k,t);d.Pop();}
  }
  void DrawLyric(DrawingContext d,Line line,string text,double px,double py,double width,double size,double alpha,bool advance=true){
   if(String.IsNullOrEmpty(text)||width<=0||alpha<=0)return;
   bool words=settings.WordSync&&line!=null&&line.words!=null&&line.words.Length>0;
   string slot="lyric"+(line==null?0:line.index%3);double over=Math.Max(0,Measure(text,size,true)-width),off=MarqueeOffset(slot,text,over,advance),reveal=0;
   if(advance)LyricScroll=off;
   if(words){double chars=0,at=LyricPosition;for(int i=0;i<line.words.Length;i++){var word=line.words[i];int[] indices=StringInfo.ParseCombiningCharacters(word.text??"");int n=indices.Length;if(at>=word.end)chars+=n+(i+1<line.words.Length?1:0);else if(at<=word.start)break;else{chars+=n*Clamp((at-word.start)/Math.Max(.001,word.end-word.start),0,1);break;}}int[] offsets=StringInfo.ParseCombiningCharacters(text);int count=(int)Math.Min(chars,offsets.Length);int end=count<offsets.Length?offsets[count]:text.Length;reveal=Measure(text.Substring(0,end),size,true);if(count<offsets.Length){int next=count+1<offsets.Length?offsets[count+1]:text.Length;reveal+=(Measure(text.Substring(0,next),size,true)-reveal)*(chars-count);}}
   PaintFaded(d,px,py,width,size,off,over,()=>{Text(d,text,px-off,py,size,true,B(255,255,255,(words?112:240)*alpha));if(words&&reveal>0){d.PushClip(new RectangleGeometry(new Rect(px-off,py-size*.35,reveal,size*1.9)));Text(d,text,px-off,py,size,true,B(255,255,255,240*alpha));d.Pop();}});
  }
  protected override HitTestResult HitTestCore(PointHitTestParameters p){if(Opacity>.1&&(panelRect.Contains(p.HitPoint)||lyricRect.Contains(p.HitPoint)&&lyricW>0))return new PointHitTestResult(this,p.HitPoint);return null;}
  void Move(object o,MouseEventArgs e){PointerMove(e.GetPosition(this));}
  internal void PointerMove(Point point){mouse=point;lastTouch=watch.Elapsed.TotalSeconds;if(dragSeek){seekAt=model.Start+Clamp((mouse.X-seekRect.X)/seekRect.Width,0,1)*(model.Duration-model.Start);}if(dragVolume){volumeLevel=Clamp((mouse.X-volumeRect.X)/volumeRect.Width,0,1);double now=watch.Elapsed.TotalSeconds;if(now-volumeSend>.075){Send("volume",volumeLevel);volumeSend=now;}}}
  void Down(object o,MouseButtonEventArgs e){PointerDown(e.GetPosition(this));e.Handled=true;}
  internal void PointerDown(Point point){mouse=point;lastTouch=watch.Elapsed.TotalSeconds;mouseDown=true;
   if(fill>.6&&model.Seek&&model.Duration>model.Start&&seekRect.Contains(mouse)){dragSeek=true;seekAt=model.Start+Clamp((mouse.X-seekRect.X)/seekRect.Width,0,1)*(model.Duration-model.Start);if(IsHitTestVisible)CaptureMouse();}
   else if(fill>.6&&model.Volume>=0&&volumeOpen>.85&&volumeRect.Contains(mouse)){dragVolume=true;volumeLevel=Clamp((mouse.X-volumeRect.X)/volumeRect.Width,0,1);if(IsHitTestVisible)CaptureMouse();}
  }
  void Up(object o,MouseButtonEventArgs e){PointerUp(e.GetPosition(this));e.Handled=true;}
  internal void PointerUp(Point point){mouse=point;double now=watch.Elapsed.TotalSeconds;
   if(dragSeek){dragSeek=false;seekUntil=now+1.5;positionShown=positionTarget=seekAt;Send("seek",seekAt);ReleaseMouseCapture();mouseDown=false;return;}
   if(dragVolume){dragVolume=false;volumeUntil=now+1.5;Send("volume",volumeLevel);ReleaseMouseCapture();mouseDown=false;return;}
   if(!mouseDown)return;mouseDown=false;
   if(fill>.6){for(int i=0;i<3;i++){double cx=x+w/2+(i-1)*58*k;if(new Rect(cx-20*k,h-47*k,40*k,40*k).Contains(mouse)){if(i==0?model.Prev:i==2?model.Next:model.Play)Send(i==0?"prev":i==2?"next":"play",0);return;}}if(model.Volume>=0&&new Rect(x+w-56*k,h-47*k,40*k,40*k).Contains(mouse)){volumeUntil=now+4;return;}}
   Toggle();
  }
  public void Capture(string file){UpdateLayout();var bmp=new RenderTargetBitmap((int)Math.Ceiling(host.Width),(int)Math.Ceiling(host.Height),96,96,PixelFormats.Pbgra32);bmp.Render(this);Rect bounds=new Rect(x+w/2-paintW/2,0,paintW,paintH);if(lyricW>0)bounds.Union(lyricRect);bounds.Inflate(2*k,2*k);bounds.Intersect(new Rect(0,0,bmp.PixelWidth,bmp.PixelHeight));var crop=new CroppedBitmap(bmp,new Int32Rect((int)Math.Floor(bounds.X),(int)Math.Floor(bounds.Y),(int)Math.Ceiling(bounds.Width),(int)Math.Ceiling(bounds.Height)));var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(crop));using(var s=File.Create(file))encoder.Save(s);}
 }
}
