using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OfflineSubtitles;

namespace SubtitleStudio {
 public sealed class TraditionalJob : ITranslationJob {
  public const string Version="studio-opencc-s2t-v1-20261004";
  readonly string root; readonly Action<string> log; readonly object gate=new object();
  volatile bool cancelled; int active; Process child; IntPtr processJob;
  public Action<int,int> Progress {get;set;}
  public TraditionalJob(string directory,Action<string> message){root=Path.GetFullPath(directory);log=message??delegate{};}
  void CheckCancel(){if(cancelled)throw new OperationCanceledException("繁体转换已取消，原字幕未覆盖。");}
  public void Cancel(){cancelled=true;lock(gate){StopOwnedProcess();}}
  public void Dispose(){Cancel();}
  void StopOwnedProcess(){
   if(processJob!=IntPtr.Zero){Native.CloseHandle(processJob);processJob=IntPtr.Zero;}
   if(child!=null)try{if(!child.HasExited)child.Kill();}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
  }
  string[] Convert(string[] lines){
   CheckCancel();string exe=Path.Combine(root,"opencc","bin","opencc.exe"),config=Path.Combine(root,"opencc","share","opencc","s2t.json");
   if(!File.Exists(exe) || !File.Exists(config))throw new FileNotFoundException("随包 OpenCC 引擎或 s2t 词典配置缺失，请复制整个工具文件夹。");
   string work=Path.Combine(Path.GetTempPath(),"SUB-OpenCC-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
   Process process=null;
   try{
    string input=Path.Combine(work,"input.txt"),output=Path.Combine(work,"output.txt");File.WriteAllText(input,string.Join("\n",lines)+"\n",new UTF8Encoding(false,true));CheckCancel();
    var info=new ProcessStartInfo(exe){WorkingDirectory=Path.GetDirectoryName(config),UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
    info.Arguments="-i "+WindowsArgs.Quote(input)+" -o "+WindowsArgs.Quote(output)+" -c "+WindowsArgs.Quote(config);
    process=new Process{StartInfo=info};
    lock(gate){CheckCancel();child=process;process.Start();try{processJob=Native.CreateKillJob(process.Handle);}catch{StopOwnedProcess();throw;}}
    Task<string> stdout=process.StandardOutput.ReadToEndAsync(),stderr=process.StandardError.ReadToEndAsync();var watch=Stopwatch.StartNew();
    while(!process.WaitForExit(100)){CheckCancel();if(watch.Elapsed.TotalSeconds>=120)throw new TimeoutException("OpenCC 转换超过 2 分钟，已停止本次转换。");}
    CheckCancel();if(!Task.WaitAll(new Task[]{stdout,stderr},5000))throw new TimeoutException("OpenCC 输出读取超时。");CheckCancel();
    if(process.ExitCode!=0){string error=stderr.Result.Trim();if(error.Length>2000)error=error.Substring(0,2000);throw new Exception("OpenCC 转换失败（"+process.ExitCode+"）："+error);}
    if(!File.Exists(output))throw new IOException("OpenCC 未生成转换结果。");string converted=File.ReadAllText(output,new UTF8Encoding(false,true));
    if(converted.IndexOfAny(new[]{'\r','\0','\uFFFD'})>=0 || !converted.EndsWith("\n",StringComparison.Ordinal))throw new Exception("OpenCC 返回无效正文格式。");
    string[] result=converted.Substring(0,converted.Length-1).Split('\n');if(result.Length!=lines.Length)throw new Exception("OpenCC 转换改变正文行数。");return result;
   }finally{
    lock(gate){StopOwnedProcess();if(child==process)child=null;}
    if(process!=null)process.Dispose();
    if(Directory.Exists(work))Directory.Delete(work,true);
   }
  }
  static string Restore(Srt source,int index,string converted){
   string original=source.Parts[index];var tags=Srt.Tags.Matches(original);int found=0;
   if(converted==null || converted.IndexOfAny(new[]{'\r','\n','\0','\uFFFD'})>=0 || Srt.Tags.IsMatch(converted))throw new Exception("OpenCC 正文或格式标签校验失败。");
   string restored=Regex.Replace(converted,@"⟦T\d{3}⟧",delegate(Match m){if(found>=tags.Count || m.Value!="⟦T"+found.ToString("D3")+"⟧")throw new Exception("OpenCC 格式标签丢失或顺序改变。");return tags[found++].Value;});
   if(found!=tags.Count || restored.Contains("⟦") || restored.Contains("⟧"))throw new Exception("OpenCC 格式标签丢失或保护标记无效。");
   string leading=Regex.Match(original,@"^[^\S\r\n]*").Value,trailing=Regex.Match(original,@"[^\S\r\n]*$").Value;
   return leading+restored.Trim()+trailing;
  }
  static byte[] Render(Srt output,byte[] sourceBytes){
   if(sourceBytes.Length>=2 && ((sourceBytes[0]==255 && sourceBytes[1]==254) || (sourceBytes[0]==254 && sourceBytes[1]==255))){
    var encoding=new UnicodeEncoding(sourceBytes[0]==254,true,true);byte[] body=encoding.GetBytes(string.Join("",output.Parts)),preamble=encoding.GetPreamble();var result=new byte[preamble.Length+body.Length];preamble.CopyTo(result,0);body.CopyTo(result,preamble.Length);return result;
   }
   return output.Bytes();
  }
  public object TranslateFile(string path){
   CheckCancel();if(Interlocked.CompareExchange(ref active,1,0)!=0)throw new InvalidOperationException("本地繁体任务正在处理另一份字幕。");
   try{
    path=Path.GetFullPath(path);if(!File.Exists(path) || !string.Equals(Path.GetExtension(path),".srt",StringComparison.OrdinalIgnoreCase))throw new Exception("请选择存在的 .srt 字幕文件。");
    var watch=Stopwatch.StartNew();byte[] bytes=File.ReadAllBytes(path);var source=Srt.Read(bytes);var output=Srt.Read(bytes);string hash=Srt.Hash(bytes);
    log(Path.GetFileName(path)+"：本地 OpenCC 简转繁，共 "+source.Cues.Count+" 条字幕。");
    int[] indices=source.Cues.SelectMany(c=>c.Body).ToArray();string[] converted=Convert(indices.Select(i=>source.ProtectedLine(i)).ToArray());int cursor=0,done=0;
    foreach(var cue in source.Cues){CheckCancel();foreach(int index in cue.Body)output.Parts[index]=Restore(source,index,converted[cursor++]);done++;if(Progress!=null)Progress(done,source.Cues.Count);}
    CheckCancel();source.AssertStructure(output);byte[] rendered=Render(output,bytes);source.AssertStructure(Srt.Read(rendered));
    if(Srt.FileHash(path)!=hash)throw new Exception("转换期间原字幕发生变化，本次不保存。请重新选择新字幕。");CheckCancel();
    string result=Srt.SaveNew(path,rendered,"_繁体");
    try{byte[] saved=File.ReadAllBytes(result);if(Srt.Hash(saved)!=Srt.Hash(rendered))throw new IOException("繁体输出读取校验失败。");source.AssertStructure(Srt.Read(saved));}catch{if(File.Exists(result))File.Delete(result);throw;}
    var report=new{source=path,output=result,mode="local",target_language_code="zh-Hant",target_language="繁体中文",source_sha256=hash,output_sha256=Srt.Hash(rendered),cues=source.Cues.Count,cache_hits=0,model_requests=0,seconds=Math.Round(watch.Elapsed.TotalSeconds,2),backend="OpenCC",offline=true,timeline_exact=true,ids_exact=true,line_counts_exact=true,source_unchanged=true,source_format_tags_preserved=true,reading_speed_review=new object[0],audio_meaning_check="not-tested",version=Version,provider="Bundled local OpenCC s2t dictionary"};
    try{AtomicFile.Write(Path.ChangeExtension(result,"检查.json"),Json.Write(report));}catch(IOException){log("繁体字幕已保存，但检查报告未能写入。");}catch(UnauthorizedAccessException){log("繁体字幕已保存，但检查报告未能写入。");}
    log("完成："+result);return report;
   }finally{Interlocked.Exchange(ref active,0);}
  }
 }
}