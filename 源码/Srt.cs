using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
namespace OfflineSubtitles {
public class Cue { public string Id; public string Time; public long Start, End; public List<int> Body = new List<int>(); }
public class Srt {
 public string[] Parts = new string[0]; public List<Cue> Cues = new List<Cue>(); public bool Bom;
 public static readonly Regex Tags = new Regex(@"<[^>\r\n]*>|\{\\[^}\r\n]*\}");
 public static readonly Regex Chinese = new Regex(@"[\u3400-\u4DBF\u4E00-\u9FFF\uF900-\uFAFF]|[\uD840-\uD87F][\uDC00-\uDFFF]");
 static readonly Regex TimeRx = new Regex(@"^(?<h1>\d{2,}):(?<m1>\d{2}):(?<s1>\d{2}),(?<f1>\d{3})[ \t]+-->[ \t]+(?<h2>\d{2,}):(?<m2>\d{2}):(?<s2>\d{2}),(?<f2>\d{3})(?:[ \t].*)?$");
 public static string Hash(byte[] bytes) { using(var h=SHA256.Create()) return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-",""); }
 public static string FileHash(string path) { using(var f=File.OpenRead(path)) using(var h=SHA256.Create()) return BitConverter.ToString(h.ComputeHash(f)).Replace("-",""); }
 public static string EnglishLiteral(string text) {
  string plain=Regex.Replace(text,@"⟦T\d{3}⟧","");
  if(!Regex.IsMatch(plain,@"[A-Za-z]") || Chinese.IsMatch(Regex.Replace(plain,@"[啊呀啦哦噢呐]",""))) return text;
  var m=Regex.Match(text,@"[啊呀啦哦噢呐]+(?<tags>(?:⟦T\d{3}⟧)*)$");if(!m.Success)return text;
  string prefix=text.Substring(0,m.Index);return prefix+(Regex.IsMatch(prefix,@"[.!?]$")?"":"!")+m.Groups["tags"].Value;
 }
 public static Srt Parse(string text) {
  if(string.IsNullOrEmpty(text) || text.IndexOf('\0')>=0 || text.IndexOf('\uFFFD')>=0) throw new Exception("字幕为空或含乱码、无效字符。");
  var s=new Srt(); s.Parts=Regex.Split(text,@"(\r\n|\n|\r)"); int state=0; Cue cue=null; long lastId=0,lastEnd=-1;
  for(int i=0;i<s.Parts.Length;i+=2) {
   string line=s.Parts[i];
   if(line.Trim().Length==0) {
    if(state==1) throw new Exception("字幕缺少时间码。");
    if(state==2) { if(cue.Body.Count==0) throw new Exception("字幕缺少正文。"); s.Cues.Add(cue); cue=null; state=0; }
    continue;
   }
   if(state==0) {
    long id; if(!Regex.IsMatch(line,@"^[0-9]+$") || !long.TryParse(line,out id) || id<=lastId) throw new Exception("第 "+(i/2+1)+" 行序号无效、重复或倒退。");
    cue=new Cue { Id=line }; lastId=id; state=1;
   } else if(state==1) {
    var m=TimeRx.Match(line); if(!m.Success) throw new Exception("第 "+(i/2+1)+" 行时间码格式错误。");
    foreach(string name in new[]{"m1","m2","s1","s2"}) if(int.Parse(m.Groups[name].Value)>=60) throw new Exception("时间码分钟或秒超出范围。");
    long a=Millis(m,"1"),b=Millis(m,"2");
    if(b<=a || a<lastEnd) throw new Exception("第 "+cue.Id+" 条字幕时间倒退、重叠或时长无效，请先检查原字幕。");
    cue.Time=line; cue.Start=a; cue.End=b; lastEnd=b; state=2;
   } else cue.Body.Add(i);
  }
  if(state==1) throw new Exception("最后一条字幕缺少时间码。");
  if(state==2) { if(cue.Body.Count==0) throw new Exception("最后一条字幕缺少正文。"); s.Cues.Add(cue); }
  if(s.Cues.Count==0) throw new Exception("未找到有效 SRT 字幕。");
  return s;
 }
 static long Millis(Match m,string n) { checked { return long.Parse(m.Groups["h"+n].Value)*3600000+long.Parse(m.Groups["m"+n].Value)*60000+long.Parse(m.Groups["s"+n].Value)*1000+long.Parse(m.Groups["f"+n].Value); } }
 public static Srt Read(byte[] bytes) {
  if(bytes.Length==0) throw new Exception("字幕文件为空。"); string text; bool bom=false;
  if(bytes.Length>=3 && bytes[0]==239 && bytes[1]==187 && bytes[2]==191) { text=new UTF8Encoding(false,true).GetString(bytes,3,bytes.Length-3); bom=true; }
  else if(bytes.Length>=2 && bytes[0]==255 && bytes[1]==254) text=new UnicodeEncoding(false,false,true).GetString(bytes,2,bytes.Length-2);
  else if(bytes.Length>=2 && bytes[0]==254 && bytes[1]==255) text=new UnicodeEncoding(true,false,true).GetString(bytes,2,bytes.Length-2);
  else { try { text=new UTF8Encoding(false,true).GetString(bytes); } catch(DecoderFallbackException) {
    var enc=Encoding.GetEncoding(54936,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback); text=enc.GetString(bytes);
    if(Hash(enc.GetBytes(text))!=Hash(bytes)) throw new Exception("字幕编码无法无损读取，请保存为 UTF-8。");
   } }
  var result=Parse(text); result.Bom=bom; return result;
 }
 public string ProtectedLine(int index) {
  int n=0; string line=Parts[index].Trim();
  if(line.IndexOf("⟦T",StringComparison.Ordinal)>=0) throw new Exception("正文含保留标记 ⟦T，请先修改该正文。");
  return Tags.Replace(line, delegate(Match m) { return "⟦T"+(n++).ToString("D3")+"⟧"; });
 }
 public string Restore(int index,string translated) { return Restore(index,translated,"en"); }
 public string Restore(int index,string translated,string targetCode) {
  var language=Languages.Get(targetCode);
  if(translated==null) throw new Exception("译文为空。");
  string original=Parts[index]; var tags=Tags.Matches(original); int found=0;
  string sourcePlain=Tags.Replace(original,"");
  bool tagOnly=tags.Count>0 && string.IsNullOrWhiteSpace(sourcePlain);
  if(!tagOnly) language.ValidateText(Regex.Replace(translated,@"⟦T\d{3}⟧",""),sourcePlain);
  translated=Regex.Replace(translated,@"⟦T\d{3}⟧", delegate(Match m) {
   if(found>=tags.Count || m.Value!="⟦T"+found.ToString("D3")+"⟧") throw new Exception("字幕格式标签丢失或顺序改变。");
   return tags[found++].Value;
  });
  if(found!=tags.Count || translated.Contains("⟦") || translated.Contains("⟧")) throw new Exception("字幕格式标签丢失或标记无效。");
  // Empty formatting lines have no text to translate; their protected content must be exact.
  if(tagOnly && !string.Equals(translated,original.Trim(),StringComparison.Ordinal)) throw new Exception("仅含格式标签的正文行不得添加文字或改变空白。");
  string leading=Regex.Match(original,@"^[^\S\r\n]*").Value, trailing=Regex.Match(original,@"[^\S\r\n]*$").Value;
  return leading+translated.Trim()+trailing;
 }
 public void Replace(Cue cue, string[] lines) { Replace(cue,lines,"en"); }
 public void Replace(Cue cue, string[] lines,string targetCode) {
  Languages.Get(targetCode);
  if(lines==null || lines.Length!=cue.Body.Count) throw new Exception("译文正文行数改变。");
  var values=new string[lines.Length]; for(int i=0;i<lines.Length;i++) values[i]=Restore(cue.Body[i],lines[i],targetCode);
  for(int i=0;i<values.Length;i++) Parts[cue.Body[i]]=values[i];
 }
 public void AssertStructure(Srt other) {
  if(Cues.Count!=other.Cues.Count || Parts.Length!=other.Parts.Length) throw new Exception("校验失败：字幕条数或行数改变。");
  var body=new HashSet<int>(); foreach(var cue in Cues) foreach(int n in cue.Body) body.Add(n);
  for(int i=0;i<Cues.Count;i++) if(Cues[i].Id!=other.Cues[i].Id || Cues[i].Time!=other.Cues[i].Time || Cues[i].Body.Count!=other.Cues[i].Body.Count) throw new Exception("校验失败：序号、时间码或正文行数改变。");
  for(int i=0;i<Parts.Length;i++) if(!body.Contains(i) && Parts[i]!=other.Parts[i]) throw new Exception("校验失败：换行、空行或时间轴改变。");
 }
 public byte[] Bytes() {
  var b=new UTF8Encoding(false).GetBytes(string.Join("",Parts)); if(!Bom) return b;
  var all=new byte[b.Length+3]; all[0]=239;all[1]=187;all[2]=191;b.CopyTo(all,3);return all;
 }
 public static string SaveNew(string path,byte[] bytes) { return SaveNew(path,bytes,"_英文_离线"); }
 public static string SaveNew(string path,byte[] bytes,string suffix) {
  if(!Languages.IsOutputSuffix(suffix)) throw new ArgumentException("输出模式无效。");
  string dir=Path.GetDirectoryName(Path.GetFullPath(path)),stem=Path.GetFileNameWithoutExtension(path); string result;
  for(int n=1;;n++) {
   result=Path.Combine(dir,stem+suffix+(n==1?"":"_"+n)+".srt");
   FileStream file; try { file=new FileStream(result,FileMode.CreateNew,FileAccess.Write,FileShare.None); } catch(IOException) { if(File.Exists(result)) continue; throw; }
   try { using(file) { file.Write(bytes,0,bytes.Length);file.Flush(true); } if(Hash(File.ReadAllBytes(result))!=Hash(bytes)) throw new IOException("输出校验失败。"); }
   catch { file.Dispose(); if(File.Exists(result)) File.Delete(result); throw; }
   return result;
  }
 }
}
}
