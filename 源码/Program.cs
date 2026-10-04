using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Windows;
using System.Runtime.InteropServices;
using OfflineSubtitles;
using SubtitleStudio;
class Program {
 public static readonly string Root=AppDomain.CurrentDomain.BaseDirectory;
 [DllImport("user32.dll")]static extern bool SetProcessDPIAware();
 [STAThread]static int Main(string[] args){if(args.Contains("--check"))return Verify(args);if(args.Contains("--translate"))return Run(args);SetProcessDPIAware();var app=new Application();try{app.Run(new StudioWindow(Root,args.Where(x=>!x.StartsWith("--")).ToArray()));return 0;}catch(Exception ex){try{Directory.CreateDirectory(Path.Combine(Root,"data"));File.WriteAllText(Path.Combine(Root,"data","界面错误.log"),ex.ToString(),Encoding.UTF8);}catch{}MessageBox.Show("界面未能启动，请检查软件文件是否完整。\n"+ex.Message,"SUB Studio");return 1;}}
 static string Option(string[] args,string name,string fallback){int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
 static string Report(string[] args){return Path.GetFullPath(Option(args,"--report",Path.Combine(Root,"data","last-run.json")));}
 static int Run(string[] args){var reports=new List<object>();var errors=new List<object>();string report=Report(args),log=Path.ChangeExtension(report,"log");Directory.CreateDirectory(Path.GetDirectoryName(report));try{string mode=ModeRules.Normalize(Option(args,"--mode","offline")),target=Languages.Get(Option(args,"--target","en")).Code;var files=new List<string>();for(int i=0;i<args.Length;i++){if(args[i]=="--report"||args[i]=="--mode"||args[i]=="--target"){i++;continue;}if(!args[i].StartsWith("--"))files.Add(args[i]);}using(var worker=ModeRules.Create(Root,mode,delegate(string m){File.AppendAllText(log,DateTime.Now.ToString("HH:mm:ss ")+m+Environment.NewLine,Encoding.UTF8);},args.Contains("--cpu"),target)){foreach(string file in files)try{reports.Add(worker.TranslateFile(file));}catch(Exception ex){errors.Add(new{file=file,error=ex.Message});}}}catch(Exception ex){errors.Add(new{error=ex.Message});}bool success=errors.Count==0&&reports.Count>0;AtomicFile.Write(report,Json.Write(new{success=success,files=reports.ToArray(),errors=errors.ToArray()}));return success?0:1;}
 static int Verify(string[] args){bool success=true;var checks=new List<object>();try{var map=Json.Serializer().Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(Root,"manifest-sha256.json"),Encoding.UTF8));foreach(var entry in map){string path=Path.GetFullPath(Path.Combine(Root,entry.Key));bool ok=path.StartsWith(Root,StringComparison.OrdinalIgnoreCase)&&File.Exists(path)&&Srt.FileHash(path).Equals(entry.Value,StringComparison.OrdinalIgnoreCase);checks.Add(new{file=entry.Key,pass=ok});success&=ok;}}catch(Exception ex){success=false;checks.Add(new{error=ex.Message});}AtomicFile.Write(Report(args),Json.Write(new{success=success,checks=checks}));return success?0:1;}
}
