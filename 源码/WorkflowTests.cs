using System;
using System.Collections.Generic;
using System.Reflection;
using OfflineSubtitles;
using SubtitleStudio;
class WorkflowTests {
 static int passed,failed;
 static void Test(string name,Action action){try{action();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
 static void Check(bool ok){if(!ok)throw new Exception("assertion failed");}
 static MethodInfo Need(Type type,string name,Type[] args){var method=type.GetMethod(name,args);if(method==null)throw new Exception("missing multilingual API "+type.Name+"."+name);return method;}
 static string Key(string target){var m=Need(typeof(ModeRules),"CacheKey",new[]{typeof(string),typeof(string),typeof(string),typeof(string),typeof(string)});return (string)m.Invoke(null,new object[]{"online","v","hash","clip",target});}
 static Dictionary<string,string[]> Validate(Type type,string text,string target){var m=Need(type,"Validate",new[]{typeof(Srt),typeof(IList<Cue>),typeof(string),typeof(string)});var source=Srt.Parse("1\r\n00:00:00,000 --> 00:00:03,000\r\n<i>下雨了，快走。</i>\r\n");return (Dictionary<string,string[]>)m.Invoke(null,new object[]{source,source.Cues,text,target});}
 static void Main(){
  Test("target caches cannot collide",delegate{var seen=new HashSet<string>();foreach(var lang in new[]{"en","ja","ko","th","hi","es","it"})Check(seen.Add(Key(lang)));});
  Test("offline Japanese accepts kanji and kana",delegate{Check(Validate(typeof(Translator),"{\"rows\":[{\"id\":1,\"lines\":[\"⟦T000⟧雨だ。急ごう。⟦T001⟧\"]}]}","ja").Count==1);});
  Test("online Hindi glossary uses translation",delegate{Check(Validate(typeof(OnlineTranslator),"{\"rows\":[{\"id\":1,\"lines\":[\"⟦T000⟧बारिश हो रही है। जल्दी चलो।⟦T001⟧\"]}],\"glossary\":[{\"source\":\"下雨\",\"translation\":\"बारिश\"}]}","hi").Count==1);});
  Test("online Japanese glossary can contain kanji",delegate{Check(Validate(typeof(OnlineTranslator),"{\"rows\":[{\"id\":1,\"lines\":[\"⟦T000⟧雨だ。急ごう。⟦T001⟧\"]}],\"glossary\":[{\"source\":\"雨\",\"translation\":\"雨\"}]}","ja").Count==1);});
  Console.WriteLine("Passed="+passed+" Failed="+failed);Environment.ExitCode=failed==0?0:1;
 }
}
