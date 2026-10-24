using System;using System.IO;using System.Reflection;using System.Threading;using System.Windows;using System.Windows.Controls;using System.Windows.Threading;using System.Web.Script.Serialization;using BongoChat;
public static class UpdateTests {
 static int checks;static BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
 static object Get(ChatWindow w,string name){return typeof(ChatWindow).GetField(name,flags).GetValue(w);}
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 static string Release(string tag,bool pre=false,bool draft=false,string state="uploaded",string name=null,string link=null,long size=1234){
  return new JavaScriptSerializer().Serialize(new{tag_name=tag,prerelease=pre,draft=draft,html_url="https://evil.example/ignored",assets=new[]{new{name=name??("BongoCatChatMerger-"+tag.TrimStart('v')+".exe"),state=state,size=size,browser_download_url=link??(UpdateService.Repository+"/releases/download/"+tag+"/app.exe")}}});
 }
 static void Pump(ChatWindow w,Func<bool> done){
  DateTime until=DateTime.UtcNow.AddSeconds(5);
  while(!done()&&DateTime.UtcNow<until){w.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(delegate{}));Thread.Sleep(5);}
  if(!done())throw new Exception("Update UI did not complete in fixture");
 }
 static void Select(ChatWindow w,string code){
  var box=(ComboBox)((Grid)w.Content).FindName("Language");foreach(ComboBoxItem item in box.Items)if((string)item.Tag==code){box.SelectedItem=item;return;}
 }
 [STAThread]public static int Main(string[] args){try{
  if(args.Length==1&&args[0]=="--live"){var r=UpdateService.Check(typeof(ChatWindow).Assembly.GetName().Version);Console.WriteLine(r.Kind+" "+r.VersionText+" "+r.PageUrl);return r.Kind==UpdateKind.Failed||r.Kind==UpdateKind.RateLimited?1:0;}
  var installed=new Version(0,4,2,0);
  Check(UpdateService.Parse(Release("v0.4.3"),installed).Kind==UpdateKind.Available,"a newer published EXE produces an update offer");
  Check(UpdateService.Parse(Release("v0.4.2"),installed).Kind==UpdateKind.Current,"an equal stable release does not prompt an update");
  Check(UpdateService.Parse(Release("v0.4.1"),installed).Kind==UpdateKind.Current,"an older release never prompts a downgrade");
  Check(UpdateService.Parse(Release("v0.10.0"),new Version(0,9,9)).Kind==UpdateKind.Available,"versions compare numerically instead of lexically");
  Check(UpdateService.Parse(Release("v0.4.3",true),installed).Kind==UpdateKind.NoPackage,"prerelease metadata is excluded");
  Check(UpdateService.Parse(Release("v0.4.3",false,true),installed).Kind==UpdateKind.NoPackage,"draft metadata is excluded");
  Check(UpdateService.Parse(Release("v0.4.3",false,false,"starter"),installed).Kind==UpdateKind.NoPackage,"an unfinished upload cannot trigger an update");
  Check(UpdateService.Parse(Release("v0.4.3",name:"BongoCatChatMerger.zip"),installed).Kind==UpdateKind.NoPackage,"source/ZIP-only metadata is not advertised as a ready EXE");
  Check(UpdateService.Parse(Release("v0.4.3",size:0),installed).Kind==UpdateKind.NoPackage,"empty installer assets are not offered");
  Check(UpdateService.Parse(Release("v0.4.3",link:"https://evil.example/app.exe"),installed).Kind==UpdateKind.NoPackage,"a foreign asset host is rejected");
  Check(UpdateService.Parse(Release("v0.4.3",link:"http://github.com/RATOY-Jeong/bongo-cat-chat-merger/releases/download/v0.4.3/app.exe"),installed).Kind==UpdateKind.NoPackage,"an insecure asset URL is rejected");
  Check(UpdateService.Parse(Release("v0.4.3",link:"https://github.com/other/repo/releases/download/v0.4.3/app.exe"),installed).Kind==UpdateKind.NoPackage,"an asset from another repository is rejected");
  var safe=UpdateService.Parse(Release("v0.4.3"),installed);
  Check(safe.PageUrl==UpdateService.Repository+"/releases/tag/v0.4.3","the browser target is built from the fixed repository, ignoring remote html_url");
  Check(UpdateService.ParseVersion("v0.4.3-rc1")==null&&UpdateService.ParseVersion("v00.4.3")==null&&UpdateService.ParseVersion("0.4.3+foo")==null,"non-stable and malformed version tags are rejected");
  Check(UpdateService.Parse("{broken",installed).Kind==UpdateKind.Failed&&UpdateService.Parse("{}",installed).Kind==UpdateKind.Failed,"invalid metadata becomes a controlled failure");
  Check(UpdateService.Parse(new string('x',262145),installed).Kind==UpdateKind.Failed,"metadata size is bounded before parsing");
  Check(UpdateService.Parse(Release("v0.4.2"),new Version(0,4,2,8)).Kind==UpdateKind.Current,"assembly revision does not cause a false semver update");
  TabletCompatibility.Configure();new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
  string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);AppStorage.Root=root;string game=Path.Combine(root,"game");Directory.CreateDirectory(game);File.WriteAllText(Path.Combine(game,"BongoCat.exe"),"");
  var w=new ChatWindow(false,game,true,"ko"){Left=-10000,Top=-10000,WindowStartupLocation=WindowStartupLocation.Manual,ShowActivated=false};
  var grid=(Grid)w.Content;var check=(Button)grid.FindName("CheckUpdate");var open=(Button)grid.FindName("OpenRelease");
  var input=(TextBox)Get(w,"composer");input.Text="업데이트 동안 남길 초안";
  string originalStatus=((TextBlock)Get(w,"statusText")).Text;int calls=0,uiThread=Thread.CurrentThread.ManagedThreadId,fetchThread=uiThread;string opened=null;
  var gate=new ManualResetEventSlim(false);Func<UpdateResult> next=delegate{return safe;};
  w.EnableUpdateNotifications(delegate{Interlocked.Increment(ref calls);fetchThread=Thread.CurrentThread.ManagedThreadId;gate.Wait(5000);return next();},url=>opened=url);
  w.Show();Pump(w,()=>Volatile.Read(ref calls)>0);
  Check((bool)Get(w,"updateBusy")&&!check.IsEnabled&&fetchThread!=uiThread,"startup checks run outside the UI thread and disable duplicate checks");
  check.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(calls==1,"clicks during a check do not start duplicate network work");
  input.Text+=" 수정";gate.Set();Pump(w,()=>!(bool)Get(w,"updateBusy"));
  Check(((TextBlock)Get(w,"updateText")).Text.Contains("최신 업데이트가 있습니다!")&&open.Visibility==Visibility.Visible,"a discovered release displays the Korean update notice and download button");
  Check(opened==null&&input.Text.EndsWith("수정")&&((TextBlock)Get(w,"statusText")).Text==originalStatus,"checking updates neither opens a browser nor changes chat state or drafts");
  open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(opened==safe.PageUrl,"only the user's download-button click opens the canonical release page");
  Select(w,"en");Check(check.Content.ToString()=="Check updates"&&((TextBlock)Get(w,"updateText")).Text.Contains("new update"),"English update controls and notice switch immediately");
  Select(w,"ja");Check(check.Content.ToString()=="更新を確認"&&((TextBlock)Get(w,"updateText")).Text.Contains("アップデート"),"Japanese update controls and notice switch immediately");
  next=()=>UpdateResult.Failure(false);check.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(w,()=>!(bool)Get(w,"updateBusy"));
  Check(open.Visibility==Visibility.Visible&&((TextBlock)Get(w,"updateText")).Text.Contains("再確認失敗")&&((UpdateResult)Get(w,"updateResult")).PageUrl==safe.PageUrl,"offline rechecks preserve the previously found update and link");
  next=()=>UpdateService.Parse(Release("v0.4.2"),installed);check.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(w,()=>!(bool)Get(w,"updateBusy"));
  Check(((TextBlock)Get(w,"updateText")).Text.Contains("最新バージョン")&&open.Visibility==Visibility.Collapsed,"manual checks report the current version without offering a downgrade");
  next=()=>UpdateResult.Failure(true);check.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(w,()=>!(bool)Get(w,"updateBusy"));
  Check(((TextBlock)Get(w,"updateText")).Text.Contains("制限")&&input.Text.Contains("초안"),"rate-limit failures remain local and preserve the original draft");
  int beforeReload=calls;w.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
  w.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(delegate{}));Check(calls==beforeReload,"startup automatically checks only once even if the window loads again");
  next=()=>{throw new IOException("synthetic transport failure");};check.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(w,()=>!(bool)Get(w,"updateBusy"));
  Check(check.IsEnabled&&((TextBlock)Get(w,"updateText")).Text.Contains("確認できません"),"unexpected transport failures restore the button without crashing the UI");
  w.Width=420;w.Height=w.MinHeight;w.UpdateLayout();w.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(delegate{}));
  var offer=(Border)grid.FindName("UpdateOffer");var bottom=offer.TranslatePoint(new Point(offer.ActualWidth,offer.ActualHeight),grid);
  Check(bottom.Y<=grid.ActualHeight+1&&offer.ActualHeight>0,"the update panel stays inside the minimum-size window");
  w.Width=510;w.Height=750;w.UpdateLayout();w.Snapshot(Path.Combine(root,"update-ja.png"));Select(w,"ko");next=()=>safe;check.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(w,()=>!(bool)Get(w,"updateBusy"));w.Snapshot(Path.Combine(root,"update-ko.png"));w.Close();gate.Dispose();
  var demo=new ChatWindow(true,null,true,"en");int demoCalls=0;demo.EnableUpdateNotifications(()=>{demoCalls++;return safe;});((Button)((Grid)demo.Content).FindName("CheckUpdate")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Check(demoCalls==0&&!((Button)((Grid)demo.Content).FindName("CheckUpdate")).IsEnabled,"demo and screenshot windows never contact GitHub");demo.Close();
  Console.WriteLine("All "+checks+" update checks passed.");return 0;
 }catch(Exception ex){Console.WriteLine(ex);return 1;}}
}