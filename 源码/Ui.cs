using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using OfflineSubtitles;

namespace SubtitleStudio {
 sealed class LanguageChoice {
  readonly TargetLanguage target;
  public LanguageChoice(TargetLanguage value){target=value;}
  public string Name{get{return target.Name;}}
  public string Code{get{return target.Code;}}
 }
 sealed class QueueItem : INotifyPropertyChanged {
  public string Path{get;set;} public string Output{get;set;}
  public string Name{get{return System.IO.Path.GetFileName(Path);}}
  public string Folder{get{return System.IO.Path.GetDirectoryName(Path);}}
  string state="等待翻译";
  public string State{get{return state;}set{state=value;Changed("State");Changed("StateForeground");Changed("StateBackground");}}
  public Brush StateForeground{get{return Brush(State=="已完成"?"#6B947D":State=="未完成"?"#B67B8D":State=="翻译中"||State=="转换中"?"#8A62B1":"#AA9DB5");}}
  public Brush StateBackground{get{return Brush(State=="已完成"?"#EFF5F0":State=="未完成"?"#F9EFF2":State=="翻译中"||State=="转换中"?"#F1E8FA":"#F6F2F8");}}
  static Brush Brush(string hex){var b=new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));b.Freeze();return b;}
  public event PropertyChangedEventHandler PropertyChanged;
  void Changed(string p){var handler=PropertyChanged;if(handler!=null)handler(this,new PropertyChangedEventArgs(p));}
 }
 public sealed class StudioWindow : Window {
  readonly string root;readonly Grid view;readonly ObservableCollection<QueueItem> queue=new ObservableCollection<QueueItem>();
  readonly Button add,clear,start,cancel,open,account,details,quickTraditional;
  readonly ComboBox targetLanguage;
  readonly ToggleButton offline,online;readonly CheckBox cpu;readonly ListBox files;readonly TextBox messages;
  readonly TextBlock status,percent,count,description,accountState;readonly Border outer,detailsPanel;readonly FrameworkElement empty,accountPanel;readonly ProgressBar progress;
  string mode="offline",lastOutput;bool busy,expanded;ITranslationJob worker;CodexClient loginWorker;int accountGeneration;
  T Find<T>(string name) where T:class{return (T)view.FindName(name);}
  public StudioWindow(string directory,string[] initial){
   root=directory;Title="SUB Studio · 字幕翻译便携版";WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ResizeMode=ResizeMode.CanResizeWithGrip;WindowStartupLocation=WindowStartupLocation.CenterScreen;MinWidth=760;MinHeight=620;Width=Math.Min(960,SystemParameters.WorkArea.Width-28);Height=Math.Min(746,SystemParameters.WorkArea.Height-28);
   using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("SubtitleStudio.Ui.xaml"))using(var reader=new StreamReader(s)){view=(Grid)XamlReader.Parse(reader.ReadToEnd());}Content=view;
   using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("SubtitleStudio.Sub.png")){var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=s;image.EndInit();image.Freeze();Find<Image>("BrandImage").Source=image;Icon=image;}
   add=Find<Button>("Add");clear=Find<Button>("Clear");start=Find<Button>("Start");cancel=Find<Button>("Cancel");open=Find<Button>("Open");account=Find<Button>("Account");details=Find<Button>("Details");offline=Find<ToggleButton>("Offline");online=Find<ToggleButton>("Online");cpu=Find<CheckBox>("Cpu");files=Find<ListBox>("Files");messages=Find<TextBox>("Messages");status=Find<TextBlock>("Status");percent=Find<TextBlock>("Percent");count=Find<TextBlock>("FileCount");description=Find<TextBlock>("ModeDescription");accountState=Find<TextBlock>("AccountState");outer=Find<Border>("OuterFrame");detailsPanel=Find<Border>("DetailsPanel");empty=Find<FrameworkElement>("Empty");accountPanel=Find<FrameworkElement>("AccountPanel");progress=Find<ProgressBar>("Progress");
   targetLanguage=Find<ComboBox>("TargetLanguage");quickTraditional=Find<Button>("QuickTraditional");targetLanguage.ItemsSource=Languages.All.Select(l=>new LanguageChoice(l)).ToArray();targetLanguage.DisplayMemberPath="Name";targetLanguage.SelectedValuePath="Code";targetLanguage.SelectedValue="en";
   targetLanguage.SelectionChanged+=delegate{if(!busy){foreach(var item in queue){item.State="等待处理";item.Output=null;}lastOutput=null;open.IsEnabled=false;UpdateQueue();SelectMode(SelectedTarget.IsTraditional?"offline":mode);}};
   files.ItemsSource=queue;offline.Click+=delegate{SelectMode("offline");};online.Click+=delegate{SelectMode("online");};add.Click+=delegate{ChooseFiles();};clear.Click+=delegate{if(!busy){queue.Clear();lastOutput=null;open.IsEnabled=false;UpdateQueue();}};start.Click+=delegate{BeginTranslation();};quickTraditional.Click+=delegate{if(!busy){targetLanguage.SelectedValue="zh-Hant";BeginTranslation();}};cancel.Click+=delegate{RequestCancel();};account.Click+=delegate{BeginLogin();};details.Click+=delegate{ShowDetails(!expanded);};open.Click+=delegate{if(lastOutput!=null&&File.Exists(lastOutput))Process.Start(new ProcessStartInfo("explorer.exe","/select,"+WindowsArgs.Quote(lastOutput)){UseShellExecute=true});};
   Find<Button>("Minimize").Click+=delegate{WindowState=WindowState.Minimized;};Find<Button>("Maximize").Click+=delegate{ToggleMaximize();};Find<Button>("CloseWindow").Click+=delegate{Close();};
   Find<FrameworkElement>("TitleArea").MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){if(e.OriginalSource is Button)return;if(e.ClickCount==2)ToggleMaximize();else if(e.LeftButton==MouseButtonState.Pressed)try{DragMove();}catch(InvalidOperationException){}};
   StateChanged+=delegate{outer.Margin=WindowState==WindowState.Maximized?new Thickness(0):new Thickness(10);outer.CornerRadius=new CornerRadius(WindowState==WindowState.Maximized?0:30);};
   files.KeyDown+=delegate(object sender,KeyEventArgs e){if(!busy&&e.Key==Key.Delete&&files.SelectedItem!=null){queue.Remove((QueueItem)files.SelectedItem);UpdateQueue();}};
   AllowDrop=true;PreviewDragOver+=delegate(object sender,DragEventArgs e){e.Effects=!busy&&e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};Drop+=delegate(object sender,DragEventArgs e){if(!busy&&e.Data.GetDataPresent(DataFormats.FileDrop))AddFiles((string[])e.Data.GetData(DataFormats.FileDrop));};
   foreach(var b in new[]{add,clear,start,cancel,open,account,details,quickTraditional})AttachMotion(b);
   Closed+=delegate{if(worker!=null)worker.Cancel();if(loginWorker!=null)loginWorker.Cancel();};SelectMode("offline");AddFiles(initial);
  }
  void AttachMotion(Button b){var transform=new ScaleTransform();b.RenderTransform=transform;b.RenderTransformOrigin=new Point(.5,.5);Action<double> animate=delegate(double target){if(!SystemParameters.ClientAreaAnimation)return;foreach(var p in new[]{ScaleTransform.ScaleXProperty,ScaleTransform.ScaleYProperty})transform.BeginAnimation(p,new DoubleAnimation(target,TimeSpan.FromMilliseconds(140)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});};b.MouseEnter+=delegate{if(b.IsEnabled)animate(1.015);};b.MouseLeave+=delegate{animate(1);};b.PreviewMouseLeftButtonDown+=delegate{if(b.IsEnabled)animate(.98);};b.PreviewMouseLeftButtonUp+=delegate{animate(b.IsMouseOver?1.015:1);};b.IsEnabledChanged+=delegate{if(!b.IsEnabled)animate(1);};}
  void ToggleMaximize(){WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;}
  TargetLanguage SelectedTarget{get{var choice=targetLanguage.SelectedItem as LanguageChoice;return Languages.Get(choice==null?"en":choice.Code);}}
  void SelectMode(string next){if(busy)return;bool traditional=SelectedTarget.IsTraditional;mode=traditional?"offline":next;offline.IsChecked=mode=="offline";online.IsChecked=mode=="online";online.IsEnabled=!traditional;offline.Content=traditional?"本机转换":"离线";cpu.Visibility=!traditional&&mode=="offline"?Visibility.Visible:Visibility.Collapsed;accountPanel.Visibility=!traditional&&mode=="online"?Visibility.Visible:Visibility.Collapsed;description.Text=traditional?"繁体使用本机词典，一键转换；无需网络、模型或账号。":mode=="offline"?"翻译为"+SelectedTarget.Name+" · 模型随包提供，无需网络。":"翻译为"+SelectedTarget.Name+" · 沿用 Codex 登录，需要网络；字幕将发送给联网 AI。";start.Content=traditional?"转为繁体  →":"开始翻译  →";System.Windows.Automation.AutomationProperties.SetName(start,traditional?"转为繁体":"开始翻译");if(mode=="online")RefreshAccount();}
  void RefreshAccount(){int generation=++accountGeneration;accountState.Text="检查账号…";var thread=new Thread(delegate(){bool logged=CodexClient.LoginStatus(root);Ui(delegate{if(generation==accountGeneration){accountState.Text=logged?"已登录":"未登录";account.Content=logged?"重新登录":"登录账号";Find<System.Windows.Shapes.Ellipse>("AccountDot").Fill=new SolidColorBrush(logged?Color.FromRgb(122,164,144):Color.FromRgb(170,161,178));}});});thread.IsBackground=true;thread.Start();}
  void ChooseFiles(){var d=new OpenFileDialog{Title="选择字幕",Filter="SRT 字幕 (*.srt)|*.srt",Multiselect=true,CheckFileExists=true};if(d.ShowDialog(this)==true)AddFiles(d.FileNames);}
  void AddFiles(string[] paths){foreach(string path in paths)if(File.Exists(path)&&string.Equals(System.IO.Path.GetExtension(path),".srt",StringComparison.OrdinalIgnoreCase)){string full=System.IO.Path.GetFullPath(path);if(!queue.Any(i=>string.Equals(i.Path,full,StringComparison.OrdinalIgnoreCase)))queue.Add(new QueueItem{Path=full});}UpdateQueue();}
  void UpdateQueue(){count.Text=queue.Count==0?"字幕文件":"字幕文件  ·  "+queue.Count;empty.Visibility=queue.Count==0?Visibility.Visible:Visibility.Collapsed;clear.IsEnabled=queue.Count>0&&!busy;SetProgress(0);status.Text=queue.Count==0?"添加字幕，准备开始":"已选择 "+queue.Count+" 个文件 · 输出到各自原目录";}
  void Ui(Action fn){if(!Dispatcher.HasShutdownStarted&&!Dispatcher.HasShutdownFinished)try{Dispatcher.BeginInvoke(fn);}catch(InvalidOperationException){}}
  void Log(string text){Ui(delegate{messages.AppendText(DateTime.Now.ToString("HH:mm:ss ")+"  "+text+Environment.NewLine);messages.ScrollToEnd();});}
  void SetProgress(int value){value=Math.Max(0,Math.Min(100,value));percent.Text=value+"%";if(SystemParameters.ClientAreaAnimation){var animation=new DoubleAnimation(progress.Value,value,TimeSpan.FromMilliseconds(180)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};progress.BeginAnimation(RangeBase.ValueProperty,animation);}else{progress.BeginAnimation(RangeBase.ValueProperty,null);progress.Value=value;}}
  void ShowDetails(bool show){expanded=show;detailsPanel.Visibility=show?Visibility.Visible:Visibility.Collapsed;Find<RowDefinition>("DetailsRow").Height=new GridLength(show?82:0);details.Content=show?"收起详情":"运行详情";}
  void Busy(bool value){busy=value;offline.IsEnabled=add.IsEnabled=start.IsEnabled=cpu.IsEnabled=account.IsEnabled=quickTraditional.IsEnabled=targetLanguage.IsEnabled=!value;online.IsEnabled=!value&&!SelectedTarget.IsTraditional;clear.IsEnabled=!value&&queue.Count>0;cancel.IsEnabled=value;open.IsEnabled=!value&&lastOutput!=null;}
  void RequestCancel(){cancel.IsEnabled=false;status.Text="正在停止，已完成进度会保留…";var w=worker;if(w!=null)w.Cancel();var c=loginWorker;if(c!=null)c.Cancel();}
  void BeginLogin(){if(busy)return;Busy(true);messages.Clear();status.Text="请在浏览器中完成官方登录…";loginWorker=new CodexClient(root,Log);var c=loginWorker;var thread=new Thread(delegate(){string finish;try{c.Login();finish="账号登录完成";}catch(OperationCanceledException){finish="登录已取消";}catch(Exception ex){finish=ex.Message;Log(finish);}finally{c.Dispose();loginWorker=null;}Ui(delegate{Busy(false);status.Text=finish;RefreshAccount();});});thread.IsBackground=true;thread.Start();}
  void BeginTranslation(){
   if(busy)return;if(queue.Count==0){ChooseFiles();if(queue.Count==0)return;}TargetLanguage selectedTarget=SelectedTarget;string selectedMode=mode;string action=selectedTarget.IsTraditional?"转换":"翻译";var selection=queue.ToArray();bool cpuOnly=cpu.IsChecked==true;worker=ModeRules.Create(root,selectedMode,Log,cpuOnly,selectedTarget.Code);var w=worker;Busy(true);messages.Clear();SetProgress(0);lastOutput=null;status.Text="正在准备"+selectedTarget.Name+action+"…";foreach(var i in selection){i.Output=null;i.State="等待处理";}
   var thread=new Thread(delegate(){int completed=0,failed=0;bool stopped=false;var results=new List<object>();var errors=new List<object>();try{for(int j=0;j<selection.Length;j++){var item=selection[j];int number=j+1;Ui(delegate{item.State=action+"中";});w.Progress=delegate(int done,int total){Ui(delegate{SetProgress((int)(done*100L/total));status.Text="文件 "+number+" / "+selection.Length+" · 已"+action+" "+done+" / "+total+" 条";});};try{var report=w.TranslateFile(item.Path);results.Add(report);var obj=Json.Object(Json.Write(report));string result=Convert.ToString(obj["output"]);lastOutput=result;completed++;Ui(delegate{item.Output=result;item.State="已完成";});}catch(OperationCanceledException ex){Log(ex.Message);stopped=true;Ui(delegate{item.State="已取消";});break;}catch(Exception ex){failed++;errors.Add(new{file=item.Path,error=ex.Message});Log(System.IO.Path.GetFileName(item.Path)+"："+ex.Message);Ui(delegate{item.State="未完成";ShowDetails(true);});}}}finally{w.Dispose();worker=null;try{AtomicFile.Write(System.IO.Path.Combine(root,"data","last-run.json"),Json.Write(new{mode=selectedTarget.IsTraditional?"local":selectedMode,target_language_code=selectedTarget.Code,success=failed==0&&!stopped,cancelled=stopped,files=results.ToArray(),errors=errors.ToArray()}));}catch{}Ui(delegate{Busy(false);status.Text=stopped?"已取消 · 点击开始可继续":failed>0?"完成 "+completed+" 个，未完成 "+failed+" 个 · 查看运行详情":"完成 "+completed+" 个文件 · 时间轴校验通过";if(completed>0&&!stopped)SetProgress(100);});}});thread.IsBackground=true;thread.Start();
  }
 }
}
