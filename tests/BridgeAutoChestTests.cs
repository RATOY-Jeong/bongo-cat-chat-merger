using System;using System.IO;using System.Reflection;using System.Web.Script.Serialization;using BongoChat.Protocol;using BongoChat;using BongoCat;
namespace HarmonyLib{public static class AccessTools{public static FieldInfo Field(Type t,string n){return t.GetField(n,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance);}public static MethodInfo Method(Type t,string n,Type[] p){return t.GetMethod(n,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance,null,p,null);}}}
namespace Newtonsoft.Json{public static class JsonConvert{public static T DeserializeObject<T>(string text){return new JavaScriptSerializer().Deserialize<T>(text);}}}
namespace BongoChatBridge
{
 public sealed partial class BridgePlugin
 {
  private string directory,session="test-session";private FakeLogger Logger=new FakeLogger();
  private sealed class FakeLogger{public void LogWarning(string text){}}
  public BridgePlugin(string path){directory=path;Directory.CreateDirectory(path);InitializeAutoChests();}
  public void Tick(float time){UpdateAutoChests(time);}
  public bool Supported{get{return autoChestSupported;}}
  public string Status{get{return autoChestStatus;}}
 }
}
public static class BridgeAutoChestTests
{
 static int checks;static string folder,owner=Guid.NewGuid().ToString("N");static JavaScriptSerializer json=new JavaScriptSerializer();
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
 static void Lease(bool on,string session="test-session",int age=0){var now=DateTime.UtcNow.AddSeconds(-age);File.WriteAllText(Path.Combine(folder,"auto-chests.json"),json.Serialize(new AutoChestLease{protocol=1,kind="auto_chests",owner=owner,session=session,enabled=on,createdUtc=now.ToString("o"),expiresUtc=now.AddSeconds(5).ToString("o")}));}
 public static int Main(string[] args){try{
  folder=Path.GetFullPath(args[0]);Directory.CreateDirectory(folder);Shop.NormalShop=new Shop(false);Shop.EmoteShop=new Shop(true);var bridge=new BongoChatBridge.BridgePlugin(folder);
  Check(bridge.Supported,"actual bridge adapter binds the expected chest API shape");bridge.Tick(0);Check(Shop.NormalShop.Calls==0&&Shop.EmoteShop.Calls==0,"no lease never opens a chest");
  Lease(true,"wrong-session");bridge.Tick(1);Check(Shop.NormalShop.Calls==0,"a stale game-session lease cannot open a chest");
  Lease(true,age:10);bridge.Tick(2);Check(Shop.NormalShop.Calls==0,"expired permission stops new chest requests");
  Lease(false);bridge.Tick(3);Check(Shop.NormalShop.Calls==0,"explicit off is respected");
  Lease(true);Shop.NormalShop.EnoughPoints=Shop.EmoteShop.EnoughPoints=false;bridge.Tick(4);Check(Shop.NormalShop.Calls==0&&Shop.EmoteShop.Calls==0,"insufficient native game points never initiate opening");
  Shop.NormalShop.EnoughPoints=Shop.EmoteShop.EnoughPoints=true;Shop.NormalShop.StockRefreshTimeLeft=Shop.EmoteShop.StockRefreshTimeLeft=5;bridge.Tick(5);Check(Shop.NormalShop.Calls==0,"remaining stock timer prevents opening even with an inconsistent readiness flag");
  Shop.NormalShop.StockRefreshTimeLeft=Shop.EmoteShop.StockRefreshTimeLeft=0;Shop.NormalShop._shopItem.gameObject.activeSelf=Shop.EmoteShop._shopItem.gameObject.activeSelf=false;bridge.Tick(6);Check(Shop.NormalShop.Calls==0&&Shop.EmoteShop.Calls==0,"native inactive chest buttons are not bypassed");
  Shop.NormalShop._shopItem.gameObject.activeSelf=Shop.EmoteShop._shopItem.gameObject.activeSelf=true;Steam.ChestExchanger.IsExchanging=true;bridge.Tick(7);Check(Shop.NormalShop.Calls==0,"a manual or Steam exchange blocks automatic overlap");Steam.ChestExchanger.IsExchanging=false;
  bridge.Tick(8);Check(Shop.NormalShop.Calls==1&&Shop.EmoteShop.Calls==0,"ready normal chest calls the original guarded game click once");bridge.Tick(11);Check(Shop.EmoteShop.Calls==0,"the second chest waits for the first game operation to finish");
  Shop.NormalShop.Finish();Lease(true);bridge.Tick(12);Check(Shop.EmoteShop.Calls==1,"emote chest opens after the first operation completes");
  Shop.EmoteShop._openingChest=false;Shop.EmoteShop._shopItem._waitingForServer=false;Steam.ChestExchanger.IsExchanging=false;bridge.Tick(15);bridge.Tick(18);Check(Shop.EmoteShop.Calls==1,"uncertain results do not repeatedly consume the same ready chest");
  Shop.EmoteShop.gameObject.activeInHierarchy=false;bridge.Tick(19);Shop.EmoteShop.gameObject.activeInHierarchy=true;bridge.Tick(20);Check(Shop.EmoteShop.Calls==1,"hiding the UI cannot rearm an uncertain opening");
  Shop.EmoteShop.Finish();bridge.Tick(21);Shop.EmoteShop.ChestIsReady=true;Lease(true);bridge.Tick(23);Check(Shop.EmoteShop.Calls==2,"a genuine new readiness cycle rearms opening");Shop.EmoteShop.Finish();
  Shop.NormalShop.ChestIsReady=true;Lease(false);bridge.Tick(25);Check(Shop.NormalShop.Calls==1,"turning off stops subsequent ready chests");Lease(true);bridge.Tick(26);Check(Shop.NormalShop.Calls==2,"explicit re-enable may retry after an off lease");Shop.NormalShop.Finish();
  Shop.NormalShop.ChestIsReady=true;File.WriteAllText(Path.Combine(folder,"auto-chests.json"),"{broken");bridge.Tick(28);Check(Shop.NormalShop.Calls==2&&bridge.Status=="error","malformed permissions fail closed without invoking the game");
  File.WriteAllText(Path.Combine(folder,"auto-chests.json"),new string('x',5000));bridge.Tick(35);Check(Shop.NormalShop.Calls==2,"oversized permissions cannot trigger an opening");
  Lease(true);Shop.EmoteShop.ChestIsReady=false;Shop.NormalShop._shopItem._shop=Shop.EmoteShop;bridge.Tick(36);Check(Shop.NormalShop.Calls==2,"a mismatched shop ownership binding fails closed");
  Console.WriteLine("All "+checks+" bridge chest checks passed.");return 0;
 }catch(Exception ex){Console.WriteLine(ex);return 1;}}
}