using System;
using System.Text;
using System.IO;
using OfflineSubtitles;
namespace SubtitleStudio {
 public interface ITranslationJob : IDisposable {
  Action<int,int> Progress { get;set; }
  object TranslateFile(string path);
  void Cancel();
 }
 public sealed class OfflineJob : ITranslationJob {
  readonly Translator inner;
  public OfflineJob(string root,Action<string> log,bool cpu):this(root,log,cpu,"en"){}
  public OfflineJob(string root,Action<string> log,bool cpu,string targetCode) {inner=new Translator(root,log,cpu,targetCode);}
  public Action<int,int> Progress {get{return inner.Progress;}set{inner.Progress=value;}}
  public object TranslateFile(string path) {var r=Json.Object(Json.Write(inner.TranslateFile(path)));r["mode"]="offline";return r;}
  public void Cancel(){inner.Cancel();}
  public void Dispose(){inner.Dispose();}
 }
 public static class ModeRules {
  public static string Normalize(string mode) {if(mode=="offline" || mode=="online")return mode;throw new ArgumentException("模式只能是 offline 或 online。");}
  public static string CacheKey(string mode,string version,string hash,string topic) {return Srt.Hash(Encoding.UTF8.GetBytes(Normalize(mode)+"|"+version+"|"+hash+"|"+topic));}
  public static string CacheKey(string mode,string version,string hash,string topic,string targetCode) {var target=Languages.Get(targetCode);return Srt.Hash(Encoding.UTF8.GetBytes(Normalize(mode)+"|"+version+"|"+target.Code+"|"+hash+"|"+topic));}
  public static ITranslationJob Create(string root,string mode,Action<string> log,bool cpu) {return Create(root,mode,log,cpu,"en");}
  public static ITranslationJob Create(string root,string mode,Action<string> log,bool cpu,string targetCode) {var target=Languages.Get(targetCode);string selected=Normalize(mode);if(target.IsTraditional)return new TraditionalJob(root,log);return selected=="offline"?(ITranslationJob)new OfflineJob(root,log,cpu,target.Code):new OnlineTranslator(root,log,target.Code);}
 }
 public static class AtomicFile {
  public static void Write(string path,string content) {
   Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
   try {File.WriteAllText(temp,content,new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}
  }
 }
 public static class WindowsArgs {
  public static string Quote(string text) {
   var b=new StringBuilder("\"");int slashes=0;
   foreach(char ch in text) {if(ch=='\\'){slashes++;continue;}if(ch=='\"'){b.Append('\\',slashes*2+1);b.Append(ch);}else{b.Append('\\',slashes);b.Append(ch);}slashes=0;}
   b.Append('\\',slashes*2);return b.Append('"').ToString();
  }
 }
}
