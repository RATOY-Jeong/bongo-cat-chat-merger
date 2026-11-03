// Isolated model used only by bridge tests; no actual game or Steam calls.
namespace UnityEngine
{
 public class Object{public static implicit operator bool(Object value){return value!=null;}public static bool operator !(Object value){return value==null;}}
 public class GameObject{public bool activeInHierarchy=true,activeSelf=true;}
 public class Component:Object{public GameObject gameObject=new GameObject();}
}
namespace Steam{public static class ChestExchanger{public static bool IsExchanging;}}
namespace BongoCat
{
 public class ShopItem:UnityEngine.Component{public bool _waitingForServer,_isEmoteChest;public Shop _shop;}
 public class Shop:UnityEngine.Component
 {
  public static Shop NormalShop,EmoteShop;
  public ShopItem _shopItem;
  public bool ChestIsReady=true,_openingChest,_isEmoteShop,EnoughPoints=true;
  public int StockRefreshTimeLeft,Calls;
  public Shop(bool emote){_isEmoteShop=emote;_shopItem=new ShopItem{_shop=this,_isEmoteChest=emote};}
  public bool CanGetChest{get{return ChestIsReady&&EnoughPoints;}}
  public void OnClick(){if(!_shopItem.gameObject.activeSelf||!EnoughPoints)return;Calls++;_openingChest=true;_shopItem._waitingForServer=true;Steam.ChestExchanger.IsExchanging=true;}
  public void Finish(){ChestIsReady=false;_openingChest=false;_shopItem._waitingForServer=false;Steam.ChestExchanger.IsExchanging=false;}
 }
}