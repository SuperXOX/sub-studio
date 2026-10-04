using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using OfflineSubtitles;
using SubtitleStudio;
class StudioTests {
 static int passed,failed;
 static void Check(bool b) {if(!b)throw new Exception("assertion failed");}
 static void Test(string name,Action fn) {try{fn();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
 static void Reject(Action fn) {bool hit=false;try{fn();}catch{hit=true;}Check(hit);}
 static readonly string Text="1\r\n00:00:00,000 --> 00:00:01,000\r\n<i>下雨了</i>\r\n\r\n2\r\n00:00:01,100 --> 00:00:03,000\r\n带伞了吗\r\n快走";
 static void Main() {
  Test("manual offline and online modes",delegate{Check(ModeRules.Normalize("offline")=="offline" && ModeRules.Normalize("online")=="online");});
  Test("unknown mode cannot silently choose another engine",delegate{Reject(delegate{ModeRules.Normalize("auto");});});
  Test("cache namespaces independent",delegate{Check(ModeRules.CacheKey("online","v","hash","clip")!=ModeRules.CacheKey("offline","v","hash","clip"));});
  Test("Windows path with spaces ampersand quoted literally",delegate{Check(WindowsArgs.Quote(@"C:\字幕 & clip\a.srt")=="\"C:\\字幕 & clip\\a.srt\"");});
  Test("Windows trailing backslash safe",delegate{Check(WindowsArgs.Quote(@"C:\clip\")=="\"C:\\clip\\\\\"");});
  Test("online complete tagged multiline response",delegate{var s=Srt.Parse(Text);var r=OnlineTranslator.Validate(s,s.Cues,"{\"rows\":[{\"id\":2,\"lines\":[\"Did you bring an umbrella?\",\"Let's go.\"]},{\"id\":1,\"lines\":[\"⟦T000⟧It's raining.⟦T001⟧\"]}],\"glossary\":[]}");Check(r.Count==2);});
  Test("online missing glossary refused",delegate{var s=Srt.Parse(Text);Reject(delegate{OnlineTranslator.Validate(s,s.Cues,"{\"rows\":[{\"id\":1,\"lines\":[\"⟦T000⟧Rain⟦T001⟧\"]},{\"id\":2,\"lines\":[\"Hi\",\"Bye\"]}]}");});});
  Test("online omitted cue refused",delegate{var s=Srt.Parse(Text);Reject(delegate{OnlineTranslator.Validate(s,s.Cues,"{\"rows\":[{\"id\":1,\"lines\":[\"⟦T000⟧Rain⟦T001⟧\"]}],\"glossary\":[]}");});});
  Test("online duplicate cue refused",delegate{var s=Srt.Parse(Text);Reject(delegate{OnlineTranslator.Validate(s,s.Cues,"{\"rows\":[{\"id\":1,\"lines\":[\"⟦T000⟧Rain⟦T001⟧\"]},{\"id\":1,\"lines\":[\"Hi\"]}],\"glossary\":[]}");});});
  Test("online format marker lost refused",delegate{var s=Srt.Parse(Text);Reject(delegate{OnlineTranslator.Validate(s,s.Cues,"{\"rows\":[{\"id\":1,\"lines\":[\"Rain\"]},{\"id\":2,\"lines\":[\"Hi\",\"Bye\"]}],\"glossary\":[]}");});});
  Test("online mode suffix and collision avoid overwriting",delegate{string dir=Path.Combine(Path.GetTempPath(),"studio-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);try{string file=Path.Combine(dir,"字幕 & clip.srt");File.WriteAllText(file,Text);byte[] b=Encoding.UTF8.GetBytes("English");string a=Srt.SaveNew(file,b,"_英文_联网"),c=Srt.SaveNew(file,b,"_英文_联网");Check(Path.GetFileName(a)=="字幕 & clip_英文_联网.srt" && Path.GetFileName(c)=="字幕 & clip_英文_联网_2.srt" && File.ReadAllText(file)==Text);}finally{Directory.Delete(dir,true);}});
  Test("damaged login component cannot crash account status check",delegate{string dir=Path.Combine(Path.GetTempPath(),"studio-login-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(dir,"online"));try{File.WriteAllText(Path.Combine(dir,"online","codex.exe"),"invalid executable");Check(!CodexClient.LoginStatus(dir));}finally{Directory.Delete(dir,true);}});
  Console.WriteLine("Passed="+passed+" Failed="+failed);Environment.ExitCode=failed==0?0:1;
 }
}
