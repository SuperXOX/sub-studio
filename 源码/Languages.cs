using System;
using System.Collections.ObjectModel;
using System.Globalization;

namespace OfflineSubtitles {
public sealed class TargetLanguage {
 public readonly string Code, Name, PromptName;
 public readonly bool IsTraditional;
 internal TargetLanguage(string code, string name, string promptName) {
  Code=code; Name=name; PromptName=promptName; IsTraditional=code=="zh-Hant";
 }
 public string OutputSuffix(string mode) {
  if(mode!="offline" && mode!="online") throw new ArgumentException("输出模式无效。", "mode");
  return IsTraditional ? "_繁体" : "_"+Name+(mode=="offline" ? "_离线" : "_联网");
 }
 // This checks script plausibility, not semantic language accuracy. Japanese may be entirely Han.
 // Traditional conversion retains non-Chinese source words; OpenCC conversion belongs to the engine.
 public void ValidateText(string plainText, string sourcePlainText) {
  if(string.IsNullOrWhiteSpace(plainText) || plainText.Contains("-->") || plainText.IndexOfAny(new[]{'<','>','\uFFFD','\u2028','\u2029'})>=0 || Srt.Tags.IsMatch(plainText))
   throw new Exception(Name+"译文为空、含乱码或不安全的格式。");
  // Check Han ranges independently of the older framework's Unicode category tables.
  if(!IsTraditional && Code!="ja" && Srt.Chinese.IsMatch(plainText)) throw new Exception(Name+"译文混入中文，必须重新翻译。");
  bool meaningful=false;
  for(int i=0;i<plainText.Length;i++) {
   char ch=plainText[i];
   if(char.IsControl(ch) && ch!='\t') throw new Exception(Name+"译文含换行或无效控制字符。");
   if(char.IsSurrogate(ch) && !(char.IsHighSurrogate(ch) && i+1<plainText.Length && char.IsLowSurrogate(plainText[i+1]))) throw new Exception(Name+"译文含无效字符。");
   int scalar=char.ConvertToUtf32(plainText,i);
   UnicodeCategory category=CharUnicodeInfo.GetUnicodeCategory(plainText,i);
   bool letter=category==UnicodeCategory.UppercaseLetter || category==UnicodeCategory.LowercaseLetter || category==UnicodeCategory.TitlecaseLetter || category==UnicodeCategory.ModifierLetter || category==UnicodeCategory.OtherLetter;
   bool mark=category==UnicodeCategory.NonSpacingMark || category==UnicodeCategory.SpacingCombiningMark || category==UnicodeCategory.EnclosingMark;
   bool number=category==UnicodeCategory.DecimalDigitNumber || category==UnicodeCategory.LetterNumber || category==UnicodeCategory.OtherNumber;
   if(letter || number) meaningful=true;
   if(!IsTraditional && (letter || mark) && !AllowsScript(scalar)) throw new Exception(Name+"译文混入其他语言文字，必须重新翻译。");
   if(scalar>0xFFFF) i++;
  }
  if(!meaningful && !string.IsNullOrEmpty(sourcePlainText) && Srt.Chinese.IsMatch(sourcePlainText)) throw new Exception("中文正文未生成有内容的"+Name+"译文，必须重新翻译。");
 }
 bool AllowsScript(int scalar) {
  if(IsLatin(scalar) || Between(scalar,0x0300,0x036F) || Between(scalar,0x1DC0,0x1DFF)) return true;
  if(Code=="ja") return IsHan(scalar) || Between(scalar,0x3040,0x30FF) || Between(scalar,0x31F0,0x31FF) || Between(scalar,0xFF66,0xFF9F) || Between(scalar,0x1B000,0x1B16F);
  if(Code=="ko") return Between(scalar,0x1100,0x11FF) || Between(scalar,0x3130,0x318F) || Between(scalar,0xA960,0xA97F) || Between(scalar,0xAC00,0xD7FF);
  if(Code=="th") return Between(scalar,0x0E00,0x0E7F);
  if(Code=="hi") return Between(scalar,0x0900,0x097F) || Between(scalar,0xA8E0,0xA8FF) || Between(scalar,0x11B00,0x11B5F);
  return false;
 }
 static bool IsLatin(int scalar) { return Between(scalar,'A','Z') || Between(scalar,'a','z') || Between(scalar,0x00C0,0x024F) || Between(scalar,0x1E00,0x1EFF) || Between(scalar,0xA720,0xA7FF) || Between(scalar,0xAB30,0xAB6F); }
 static bool IsHan(int scalar) { return Between(scalar,0x3005,0x3007) || scalar==0x303B || Between(scalar,0x3400,0x4DBF) || Between(scalar,0x4E00,0x9FFF) || Between(scalar,0xF900,0xFAFF) || Between(scalar,0x20000,0x323AF); }
 static bool Between(int value,int start,int end) { return value>=start && value<=end; }
}
public static class Languages {
 public static readonly ReadOnlyCollection<TargetLanguage> All = Array.AsReadOnly(new[] {
  new TargetLanguage("en","英文","English"),
  new TargetLanguage("zh-Hant","繁体中文","Traditional Chinese"),
  new TargetLanguage("ja","日语","Japanese"),
  new TargetLanguage("ko","韩语","Korean"),
  new TargetLanguage("th","泰语","Thai"),
  new TargetLanguage("hi","印地语","Hindi"),
  new TargetLanguage("es","西班牙语","Spanish"),
  new TargetLanguage("it","意大利语","Italian")
 });
 public static TargetLanguage Get(string code) {
  foreach(var language in All) if(string.Equals(language.Code,code,StringComparison.Ordinal)) return language;
  throw new ArgumentException("目标语言无效。", "code");
 }
 internal static bool IsOutputSuffix(string suffix) {
  foreach(var language in All) if(suffix==language.OutputSuffix("offline") || suffix==language.OutputSuffix("online")) return true;
  return false;
 }
}
}
