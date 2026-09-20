using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
class Launcher {
 [STAThread] static void Main() {
  string root=AppDomain.CurrentDomain.BaseDirectory;
  string script=Path.Combine(root,"Island.ps1");
  foreach(string file in new[]{"Island.ps1","media-worker.ps1","lyrics-worker.ps1","local-worker.ps1","model-worker.ps1","CoverBridge.dll","IslandView.dll"})
   if(!File.Exists(Path.Combine(root,file))){MessageBox.Show("Missing "+file+". Extract all Music Island files into the same folder.","Music Island");return;}
  ProcessStartInfo p=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe"),"-NoProfile -Sta -ExecutionPolicy Bypass -WindowStyle Hidden -File \""+script+"\"");
  p.UseShellExecute=false; p.CreateNoWindow=true; p.WorkingDirectory=root;
  Process.Start(p);
 }
}
