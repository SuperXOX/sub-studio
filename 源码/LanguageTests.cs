using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections;
using OfflineSubtitles;

class LanguageTests {
 static int passed, failed;
 static readonly string Sample = "01\r\n00:00:00,100 --> 00:00:01,200\r\n  <i>你好</i>  \r\n\r\n3\n00:00:02,000 --> 00:00:04,000\n{\\an8}快走\n你带伞了吗";
 static void Check(bool ok, string message) { if(!ok) throw new Exception(message); }
 static void Test(string name, Action action) { try { action(); passed++; Console.WriteLine("PASS "+name); } catch(Exception e) { failed++; while(e is TargetInvocationException && e.InnerException!=null) e=e.InnerException; Console.WriteLine("FAIL "+name+": "+e.Message); } }
 static void Reject(Action action) { bool rejected=false; try { action(); } catch { rejected=true; } Check(rejected,"expected rejection"); }
 static MethodInfo Method(string name, Type[] arguments) { var method=typeof(Srt).GetMethod(name,arguments); Check(method!=null,"missing Srt."+name+" multilingual overload"); return method; }
 static string Restore(Srt s, int i, string text, string code) { return (string)Method("Restore",new[]{typeof(int),typeof(string),typeof(string)}).Invoke(s,new object[]{i,text,code}); }
 static void Replace(Srt s, Cue cue, string[] lines, string code) { Method("Replace",new[]{typeof(Cue),typeof(string[]),typeof(string)}).Invoke(s,new object[]{cue,lines,code}); }
 static object Language(string code) { Type type=typeof(Srt).Assembly.GetType("OfflineSubtitles.Languages"); Check(type!=null,"missing language registry"); return type.GetMethod("Get").Invoke(null,new object[]{code}); }
 static string Value(object spec, string field) { return (string)spec.GetType().GetField(field).GetValue(spec); }
 static string Suffix(object spec, string mode) { return (string)spec.GetType().GetMethod("OutputSuffix").Invoke(spec,new object[]{mode}); }
 static Srt Plain(string text) { return Srt.Parse("1\n00:00:00,000 --> 00:00:01,000\n"+text); }
 static void Main() {
  Test("Japanese Kanji accepted through new overload",delegate { var s=Plain("东京"); Check(Restore(s,4,"東京","ja")=="東京","Kanji output changed"); });
  Test("registry contains exactly eight immutable language specs",delegate {
   Type registry=typeof(Srt).Assembly.GetType("OfflineSubtitles.Languages"); Check(registry!=null,"missing language registry");
   var all=(IEnumerable)registry.GetField("All").GetValue(null); int count=0;
   foreach(object language in all) { count++; foreach(string field in new[]{"Code","Name","PromptName","IsTraditional"}) Check(language.GetType().GetField(field).IsInitOnly,"mutable spec: "+field); }
   Check(count==8,"wrong registry count");
   string[] codes={"en","zh-Hant","ja","ko","th","hi","es","it"}; string[] names={"英文","繁体中文","日语","韩语","泰语","印地语","西班牙语","意大利语"};
   for(int i=0;i<codes.Length;i++) Check(Value(Language(codes[i]),"Code")==codes[i] && Value(Language(codes[i]),"Name")==names[i],"registry mapping differs");
  });
  Test("strict target allowlist",delegate { foreach(string code in new[]{null,"","JA","fr","zh","../ja"}) Reject(delegate { Language(code); }); });
  Test("language suffixes and strict modes",delegate {
   string[] codes={"en","ja","ko","th","hi","es","it"}; string[] suffixes={"英文","日语","韩语","泰语","印地语","西班牙语","意大利语"};
   for(int i=0;i<codes.Length;i++) { Check(Suffix(Language(codes[i]),"offline")=="_"+suffixes[i]+"_离线","offline suffix"); Check(Suffix(Language(codes[i]),"online")=="_"+suffixes[i]+"_联网","online suffix"); }
   Check(Suffix(Language("zh-Hant"),"offline")=="_繁体" && Suffix(Language("zh-Hant"),"online")=="_繁体","traditional suffix");
   Reject(delegate { Suffix(Language("en"),"auto"); }); Reject(delegate { Suffix(Language("zh-Hant"),null); });
  });
  foreach(string code in new[]{"en","zh-Hant","ja","ko","th","hi","es","it"}) {
   string target=code;
   Test(target+" numeric and Latin proper names accepted",delegate { var s=Plain("纽约"); Check(Restore(s,4,"Terminal 21",""+target)=="Terminal 21","proper name changed"); Check(Restore(s,4,"21",target)=="21","number rejected"); });
   Test(target+" unsafe content and punctuation only rejected",delegate {
    var s=Plain("你好"); foreach(string text in new[]{"!?!"," ","Hello\nBye","Hello\rBye","Hello\0","Hello\uFFFD","00:00:00,000 --> 00:00:01,000","<i>Hello</i>","{\\an8}Hello","Hello\u2028Bye","Hello\u2029Bye","Hello\u0085Bye"}) Reject(delegate { Restore(s,4,text,target); });
   });
   Test(target+" format markers remain ordered and complete",delegate {
    var s=Srt.Parse(Sample); foreach(string text in new[]{"Hello","⟦T001⟧Hello⟦T000⟧","⟦T000⟧Hello","⟦T000⟧Hello⟦T001⟧⟦T001⟧","⟦T000⟧Hello⟦T999⟧","⟦T000⟧Hello⟦T001⟧⟦bad⟧","⟦T000⟧⟦T001⟧"}) Reject(delegate { Restore(s,4,text,target); });
    Check(Restore(s,4,"⟦T000⟧Hello⟦T001⟧",target)=="  <i>Hello</i>  ","tag or padding changed");
   });
  }
  Test("Japanese kana and kanji with Latin names",delegate { var s=Plain("你好"); Check(Restore(s,4,"東京でJohnと会った。カフェへ行こう。","ja").Contains("John"),"Japanese rejected"); });
  Test("Japanese Han iteration and ideographic zero",delegate { Check(Restore(Plain("人人 二零二六"),4,"人々、二〇二六年。","ja")=="人々、二〇二六年。","Japanese Han symbols rejected"); });
  Test("Korean Hangul",delegate { Check(Restore(Plain("你好"),4,"John, 안녕하세요.","ko").Contains("안녕"),"Korean rejected"); });
  Test("Thai letters and combining marks",delegate { Check(Restore(Plain("你好"),4,"สวัสดี John ที่นี่", "th").StartsWith("สวัสดี"),"Thai rejected"); });
  Test("Hindi Devanagari and vowel marks",delegate { Check(Restore(Plain("你好"),4,"John, नमस्ते दुनिया।", "hi").Contains("नमस्ते"),"Hindi rejected"); });
  Test("Spanish accented Latin",delegate { Check(Restore(Plain("你好"),4,"¡Hola! ¿Dónde está José?", "es").Contains("José"),"Spanish rejected"); });
  Test("Italian accented Latin",delegate { Check(Restore(Plain("你好"),4,"È già qui, perché sì.", "it").Contains("perché"),"Italian rejected"); });
  Test("decomposed Latin accents",delegate { Check(Restore(Plain("你好"),4,"Jose\u0301", "es")=="Jose\u0301","combining accent changed"); });
  Test("clearly foreign scripts rejected for each translation target",delegate {
   string[] codes={"en","es","it","ja","ko","th","hi"}; string[] wrong={"你好","Здравствуйте","Καλημέρα","안녕하세요","สวัสดี","नमस्ते","こんにちは"};
   foreach(string code in codes) foreach(string text in wrong) { bool permitted=(code=="ja" && (text=="你好" || text=="こんにちは")) || (code=="ko" && text=="안녕하세요") || (code=="th" && text=="สวัสดี") || (code=="hi" && text=="नमस्ते"); if(!permitted) Reject(delegate { Restore(Plain("你好"),4,text,code); }); }
  });
  Test("supplementary Chinese cannot bypass Latin target validation",delegate { foreach(string code in new[]{"en","es","it","ko","th","hi"}) Reject(delegate { Restore(Plain("你好"),4,"Hello\U0002FFFF",code); }); });
  Test("Traditional conversion allows preserved source words",delegate { Check(Restore(Plain("这里 John ไทย नमस्ते"),4,"這裡 John ไทย नमस्ते","zh-Hant")=="這裡 John ไทย नमस्ते","preserved source words rejected"); });
  Test("shared glossary validator checks content and scripts",delegate {
   var spec=Language("es"); var method=spec.GetType().GetMethod("ValidateText",new[]{typeof(string),typeof(string)}); Check(method!=null,"missing shared text validator");
   method.Invoke(spec,new object[]{"José","小何"});
   foreach(string text in new[]{"!!!","中文","<i>José</i>","José\nBye","José\0","José\uFFFD"}) Reject(delegate { method.Invoke(spec,new object[]{text,"小何"}); });
  });
  Test("invalid target rejected before replacement mutation",delegate { var s=Srt.Parse(Sample); string before=string.Join("",s.Parts); Reject(delegate { Replace(s,s.Cues[0],new[]{"⟦T000⟧Hello⟦T001⟧"},"fr"); }); Check(string.Join("",s.Parts)==before,"partial mutation"); });
  Test("multiline failure is atomic",delegate { var s=Srt.Parse(Sample); string before=string.Join("",s.Parts); Reject(delegate { Replace(s,s.Cues[1],new[]{"⟦T000⟧東京","สวัสดี"},"ja"); }); Check(string.Join("",s.Parts)==before,"partial mutation"); });
  Test("null line wrong line count and invalid UTF16 rejected",delegate {
   var s=Srt.Parse(Sample); string before=string.Join("",s.Parts);
   Reject(delegate { Replace(s,s.Cues[1],new[]{"Hello"},"es"); }); Reject(delegate { Replace(s,s.Cues[1],null,"es"); });
   Reject(delegate { Restore(Plain("你好"),4,null,"ja"); }); Reject(delegate { Restore(Plain("你好"),4,"Hello\uD800","ja"); }); Reject(delegate { Restore(Plain("你好"),4,"Hello\uDC00","ja"); });
   Check(string.Join("",s.Parts)==before,"partial mutation");
  });
  Test("multiline translation retains BOM complete layout IDs and timeline",delegate {
   byte[] text=Encoding.UTF8.GetBytes(Sample); byte[] bytes=new byte[text.Length+3]; bytes[0]=239; bytes[1]=187; bytes[2]=191; text.CopyTo(bytes,3);
   var s=Srt.Read(bytes); Replace(s,s.Cues[0],new[]{"⟦T000⟧こんにちは⟦T001⟧"},"ja"); Replace(s,s.Cues[1],new[]{"⟦T000⟧急ごう","傘を持った？"},"ja");
   string expected=Sample.Replace("你好","こんにちは").Replace("快走","急ごう").Replace("你带伞了吗","傘を持った？"); var saved=s.Bytes(); Check(saved[0]==239 && saved[1]==187 && saved[2]==191,"BOM lost"); Check(Encoding.UTF8.GetString(saved,3,saved.Length-3)==expected,"layout differs"); var reread=Srt.Read(saved); Srt.Parse(Sample).AssertStructure(reread);
  });
  Test("nonbreaking source padding preserved exactly",delegate {
   var s=Plain("\u00A0你好\u00A0"); Check(Restore(s,4,"Hello","en")=="\u00A0Hello\u00A0","nonbreaking padding lost");
  });
  Test("ideographic and mixed Unicode source padding preserved exactly",delegate {
   string leading="\u3000\t\u00A0 ",trailing=" \u2002\u202F\u3000\t"; var s=Plain(leading+"你好"+trailing);
   Check(Restore(s,4,"こんにちは","ja")==leading+"こんにちは"+trailing,"Unicode padding lost");
  });
  foreach(string code in new[]{"en","zh-Hant","ja","ko","th","hi","es","it"}) {
   string target=code;
   Test(target+" tag-only cue line retains tags and every whitespace",delegate {
    string line="\u3000 \t<i> \u00A0</i>\u3000\t";
    var s=Srt.Parse("1\r\n00:00:00,000 --> 00:00:01,000\r\n你好\r\n"+line); int index=s.Cues[0].Body[1];
    string protectedLine=s.ProtectedLine(index);
    Check(Restore(s,index,protectedLine,target)==line,"tag-only line changed");
    var empty=Plain("<i></i>"); Check(Restore(empty,4,"⟦T000⟧⟦T001⟧",target)=="<i></i>","empty tags rejected");
    foreach(string text in new[]{"⟦T000⟧Invented⟦T001⟧","⟦T001⟧⟦T000⟧","⟦T000⟧","⟦T000⟧⟦T001⟧⟦T001⟧","<i></i>","⟦T000⟧\n⟦T001⟧","⟦T000⟧\0⟦T001⟧"}) Reject(delegate { Restore(empty,4,text,target); });
    Reject(delegate { Restore(s,index,"⟦T000⟧⟦T001⟧",target); });
   });
  }
  Test("new suffix whitelist collision avoidance and no overwrite",delegate {
   string dir=Path.Combine(Path.GetTempPath(),"subtitle-language-fixture-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
   try { string source=Path.Combine(dir,"字幕 & clip.srt"); File.WriteAllText(source,Sample); byte[] bytes=Encoding.UTF8.GetBytes("東京");
    foreach(string code in new[]{"en","zh-Hant","ja","ko","th","hi","es","it"}) foreach(string mode in new[]{"offline","online"}) { string suffix=Suffix(Language(code),mode); string a=Srt.SaveNew(source,bytes,suffix),b=Srt.SaveNew(source,bytes,suffix); Check(a!=b && File.ReadAllText(source)==Sample && Srt.FileHash(a)==Srt.Hash(bytes) && Srt.FileHash(b)==Srt.Hash(bytes),"collision or content mismatch"); }
    foreach(string bad in new[]{null,"_日语","_繁体_联网","_法语_联网","../escape","_日语_联网/evil","_英文_自动"}) Reject(delegate { Srt.SaveNew(source,bytes,bad); });
   } finally { foreach(string file in Directory.GetFiles(dir)) File.Delete(file); Directory.Delete(dir); }
  });
  Console.WriteLine("Passed="+passed+" Failed="+failed); Environment.ExitCode=failed==0?0:1;
 }
}
