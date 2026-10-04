using System;
using System.IO;
using System.Text;
using OfflineSubtitles;
class CoreTests {
 static int failures, passes;
 static string Sample = "1\r\n00:00:00,100 --> 00:00:01,200\r\n  <i>下大雨了</i>  \r\n\r\n3\n00:00:02,000 --> 00:00:04,000\n{\\an8}快走\n你带伞了吗";
 static void Check(bool ok) { if(!ok) throw new Exception("assertion failed"); }
 static void Test(string name, Action action) { try { action(); Console.WriteLine("PASS " + name); passes++; } catch(Exception ex) { Console.WriteLine("FAIL " + name + ": " + ex.Message); failures++; } }
 static void Reject(Action action) { bool failed=false; try { action(); } catch { failed=true; } Check(failed); }
 static void Main() {
  Test("parse mixed line endings and multi-line cue", delegate { var s=Srt.Parse(Sample); Check(s.Cues.Count==2 && s.Cues[1].Body.Count==2 && s.Cues[1].Id=="3"); });
  Test("tags whitespace timestamps and separators preserved", delegate { var s=Srt.Parse(Sample); s.Replace(s.Cues[0],new[]{"⟦T000⟧It's pouring.⟦T001⟧"}); s.Replace(s.Cues[1],new[]{"⟦T000⟧Let's go!","Did you bring an umbrella?"}); var expected=Sample.Replace("下大雨了","It's pouring.").Replace("快走","Let's go!").Replace("你带伞了吗","Did you bring an umbrella?"); Check(string.Join("",s.Parts)==expected); Srt.Parse(Sample).AssertStructure(s); });
  Test("UTF8 round trip", delegate { var b=Encoding.UTF8.GetBytes(Sample); Check(string.Join("",Srt.Read(b).Parts)==Sample); });
  Test("UTF8 BOM preserved", delegate { var b=new UTF8Encoding(true).GetPreamble(); var text=Encoding.UTF8.GetBytes(Sample); var all=new byte[b.Length+text.Length]; b.CopyTo(all,0); text.CopyTo(all,b.Length); var s=Srt.Read(all); Check(s.Bom && s.Bytes()[0]==239); });
  Test("UTF16 LE", delegate { var s=Srt.Read(Combine(Encoding.Unicode.GetPreamble(),Encoding.Unicode.GetBytes(Sample))); Check(string.Join("",s.Parts)==Sample); });
  Test("UTF16 BE", delegate { var s=Srt.Read(Combine(Encoding.BigEndianUnicode.GetPreamble(),Encoding.BigEndianUnicode.GetBytes(Sample))); Check(string.Join("",s.Parts)==Sample); });
  Test("GB18030", delegate { Check(string.Join("",Srt.Read(Encoding.GetEncoding(54936).GetBytes(Sample)).Parts)==Sample); });
  Test("empty rejected", delegate { Reject(delegate { Srt.Read(new byte[0]); }); });
  Test("NUL rejected", delegate { Reject(delegate { Srt.Parse(Sample+"\0"); }); });
  Test("duplicate id rejected", delegate { Reject(delegate { Srt.Parse(Sample.Replace("\n3\n","\n1\n")); }); });
  Test("backwards time rejected", delegate { Reject(delegate { Srt.Parse(Sample.Replace("00:00:02,000 --> 00:00:04,000","00:00:00,000 --> 00:00:04,000")); }); });
  Test("overlap rejected", delegate { Reject(delegate { Srt.Parse(Sample.Replace("00:00:02,000","00:00:01,000")); }); });
  Test("negative duration rejected", delegate { Reject(delegate { Srt.Parse(Sample.Replace("00:00:04,000","00:00:01,000")); }); });
  Test("bad minute rejected", delegate { Reject(delegate { Srt.Parse(Sample.Replace("00:00:04,000","00:60:04,000")); }); });
  Test("empty body rejected", delegate { Reject(delegate { Srt.Parse("1\n00:00:00,000 --> 00:00:01,000\n\n"); }); });
  Test("different translation line count rejected", delegate { var s=Srt.Parse(Sample); Reject(delegate { s.Replace(s.Cues[1],new[]{"Hello"}); }); });
  Test("Chinese residue rejected", delegate { var s=Srt.Parse(Sample); Reject(delegate { s.Replace(s.Cues[0],new[]{"Hello 下雨"}); }); });
  Test("punctuation-only output for Chinese words rejected", delegate { var s=Srt.Parse("1\n00:00:00,000 --> 00:00:01,000\n请把时间码改成零"); Reject(delegate { s.Replace(s.Cues[0],new[]{"[[]]"}); }); });
  Test("mixed Cyrillic place name rejected", delegate { var s=Srt.Parse("1\n00:00:00,000 --> 00:00:01,000\n芭提雅"); Reject(delegate { s.Replace(s.Cues[0],new[]{"Patтай"}); }); });
  Test("Thai residue rejected", delegate { var s=Srt.Parse("1\n00:00:00,000 --> 00:00:01,000\n泰国"); Reject(delegate { s.Replace(s.Cues[0],new[]{"Thailand ไทย"}); }); });
  Test("English name with Chinese ending particle", delegate { Check(Srt.EnglishLiteral("Terminal twenty one呀")=="Terminal twenty one!"); });
  Test("Chinese content never stripped as a particle", delegate { Check(Srt.EnglishLiteral("Terminal 21在这里呀")=="Terminal 21在这里呀"); });
  Test("injected newline rejected", delegate { var s=Srt.Parse(Sample); Reject(delegate { s.Replace(s.Cues[0],new[]{"Hi\nBye"}); }); });
  Test("timeline tampering detected", delegate { var s=Srt.Parse(Sample); var other=Srt.Parse(Sample.Replace("00:00:04,000","00:00:05,000")); Reject(delegate { s.AssertStructure(other); }); });
  Test("no-overwrite create new suffix", delegate { var dir=Path.Combine(Path.GetTempPath(),"subtitle-qa-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); var source=Path.Combine(dir,"字幕 & clip.srt"); File.WriteAllText(source,Sample); var b=Encoding.UTF8.GetBytes("English"); var a=Srt.SaveNew(source,b); var c=Srt.SaveNew(source,b); Check(a!=c && File.ReadAllText(source)==Sample && File.ReadAllBytes(c).Length==7); File.Delete(a); File.Delete(c); File.Delete(source); Directory.Delete(dir); });
  Console.WriteLine("Passed="+passes+" Failed="+failures); Environment.ExitCode=failures==0?0:1;
 }
 static byte[] Combine(byte[] a,byte[] b) { var r=new byte[a.Length+b.Length]; a.CopyTo(r,0); b.CopyTo(r,a.Length); return r; }
}
