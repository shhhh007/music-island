using System;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: AssemblyTitle("Music Island Setup")]
[assembly: AssemblyDescription("Music Island per-user installer")]
[assembly: AssemblyVersion("1.0.0.0")]
class Installer : Form {
 readonly Color ink=Color.FromArgb(15,18,23),muted=Color.FromArgb(155,168,182),mint=Color.FromArgb(151,235,204);
 Button install;CheckBox desktop,launch;Label status;bool installed;
 string destination=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Music Island");
 [STAThread] static int Main(string[] args){
  // Read-only payload verification used by packaging/CI; no files or shortcuts are installed.
  if(args.Length==1&&args[0]=="--verify-payload"){
   try{using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.zip"))using(var zip=new ZipArchive(stream,ZipArchiveMode.Read)){if(zip.GetEntry("Music Island.exe")==null||zip.GetEntry("model-worker.ps1")==null)return 2;foreach(var entry in zip.Entries){if(entry.FullName.Contains("..")||Path.IsPathRooted(entry.FullName))return 3;using(var input=entry.Open())input.CopyTo(Stream.Null);}return 0;}}catch{return 1;}
  }
  if(args.Length==2&&args[0]=="--preview"){
   Application.EnableVisualStyles();using(var form=new Installer()){form.Show();form.Update();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);}form.Close();}return 0;
  }
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new Installer());return 0;
 }
 Installer(){
  Text="Music Island · Установка";ClientSize=new Size(620,460);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;BackColor=ink;ForeColor=Color.White;Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;
  try{Icon=Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);}catch{}
  var mark=new Label{Text="●  ●  ●",ForeColor=mint,Font=new Font("Segoe UI",17,FontStyle.Bold),Location=new Point(38,26),AutoSize=true};Controls.Add(mark);
  AddLabel("Music Island",38,77,30,Color.White,FontStyle.Bold);
  AddLabel("Музыка всегда перед глазами.",40,137,13,muted,FontStyle.Regular);
  AddLabel("Обложка, управление и текст песни — в одном острове.",40,172,10,muted,FontStyle.Regular);
  var rule=new Panel{BackColor=Color.FromArgb(37,45,54),Location=new Point(40,219),Size=new Size(540,1)};Controls.Add(rule);
  desktop=new CheckBox{Text="Создать ярлык на рабочем столе",Checked=true,Location=new Point(40,243),Size=new Size(510,27)};Controls.Add(desktop);
  launch=new CheckBox{Text="Запустить после установки",Checked=true,Location=new Point(40,278),Size=new Size(510,27)};Controls.Add(launch);
  status=AddLabel("Для текущего пользователя · Без прав администратора",40,327,9,muted,FontStyle.Regular);status.Size=new Size(530,36);status.AutoSize=false;
  install=new Button{Text="Установить  →",Location=new Point(360,386),Size=new Size(220,42),BackColor=mint,ForeColor=ink,FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",11,FontStyle.Bold)};install.FlatAppearance.BorderSize=0;install.Click+=Install;Controls.Add(install);
  var cancel=new Button{Text="Закрыть",Location=new Point(40,386),Size=new Size(130,42),BackColor=ink,ForeColor=muted,FlatStyle=FlatStyle.Flat};cancel.FlatAppearance.BorderColor=Color.FromArgb(49,60,70);cancel.Click+=delegate{Close();};Controls.Add(cancel);
 }
 Label AddLabel(string text,int x,int y,float size,Color color,FontStyle style){var label=new Label{Text=text,Location=new Point(x,y),AutoSize=true,ForeColor=color,Font=new Font("Segoe UI",size,style)};Controls.Add(label);return label;}
 void Install(object sender,EventArgs args){
  if(installed){Close();return;}install.Enabled=false;status.Text="Устанавливаем Music Island…";Refresh();
  try{
   Directory.CreateDirectory(destination);
   if(File.Exists(Path.Combine(destination,"Island.ps1"))){File.WriteAllText(Path.Combine(destination,"exit.request"),"");System.Threading.Thread.Sleep(1400);}
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.zip"))using(var zip=new ZipArchive(stream,ZipArchiveMode.Read)){
    foreach(var entry in zip.Entries){string output=Path.GetFullPath(Path.Combine(destination,entry.FullName));if(!output.StartsWith(Path.GetFullPath(destination)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Invalid package path.");if(entry.Name.Length==0)continue;Directory.CreateDirectory(Path.GetDirectoryName(output));using(var input=entry.Open())using(var target=File.Create(output))input.CopyTo(target);}
   }
   string stop=Path.Combine(destination,"exit.request");if(File.Exists(stop))File.Delete(stop);
   string menu=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"Music Island");Directory.CreateDirectory(menu);
   Shortcut(Path.Combine(menu,"Music Island.lnk"),Path.Combine(destination,"Music Island.exe"),"");
   Shortcut(Path.Combine(menu,"Удалить Music Island.lnk"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe"),"-NoProfile -ExecutionPolicy Bypass -File \""+Path.Combine(destination,"Uninstall.ps1")+"\"");
   if(desktop.Checked)Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Music Island.lnk"),Path.Combine(destination,"Music Island.exe"),"");
   installed=true;status.Text="Готово! Ярлык появился в меню «Пуск».";install.Text="Готово ✓";install.Enabled=true;
   if(launch.Checked)Process.Start(new ProcessStartInfo(Path.Combine(destination,"Music Island.exe")){UseShellExecute=true,WorkingDirectory=destination});
  }catch(Exception ex){status.Text="Не удалось завершить установку.";install.Enabled=true;MessageBox.Show("Закройте Music Island и повторите установку.\n\n"+ex.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Error);}
 }
 void Shortcut(string path,string target,string args){
  Type type=Type.GetTypeFromProgID("WScript.Shell");object shell=Activator.CreateInstance(type),link=null;
  try{link=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path});Type t=link.GetType();t.InvokeMember("TargetPath",BindingFlags.SetProperty,null,link,new object[]{target});t.InvokeMember("Arguments",BindingFlags.SetProperty,null,link,new object[]{args});t.InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,link,new object[]{destination});t.InvokeMember("IconLocation",BindingFlags.SetProperty,null,link,new object[]{Path.Combine(destination,"Music Island.exe")+",0"});t.InvokeMember("Save",BindingFlags.InvokeMethod,null,link,null);}finally{if(link!=null)Marshal.FinalReleaseComObject(link);Marshal.FinalReleaseComObject(shell);}
 }
}
