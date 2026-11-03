// Isolated model used only by bridge tests; no actual game or Steam calls.
namespace UnityEngine
{
 public class Object{public static implicit operator bool(Object value){return value!=null;}public static bool operator !(Object value){return value==null;}}
 public class GameObject:Object{public bool activeInHierarchy=true,activeSelf=true;public Scene scene=new Scene();}
 public class Scene{public bool valid=true,isLoaded=true;public bool IsValid(){return valid;}}
 public class Component:Object{public GameObject gameObject=new GameObject();}
 public class Behaviour:Component{public bool enabled=true;public bool isActiveAndEnabled{get{return enabled&&gameObject.activeInHierarchy;}}}
 public static class Resources{public static Object[] Items=new Object[0];public static Object[] FindObjectsOfTypeAll(System.Type type){return System.Array.FindAll(Items,x=>x!=null&&type.IsInstanceOfType(x));}}
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
  public int StockRefreshTimeLeft,Calls,Gifts,Points=100,Price=1;
  public System.Collections.Generic.Queue<System.Action> _pendingGiftPayoffs=new System.Collections.Generic.Queue<System.Action>();
  public bool CanAffordChest{get{return EnoughPoints&&Points>=Price;}}
  public Shop(bool emote){_isEmoteShop=emote;_shopItem=new ShopItem{_shop=this,_isEmoteChest=emote};}
  public bool CanGetChest{get{return ChestIsReady&&EnoughPoints;}}
  public void OnClick(){if(!_shopItem.gameObject.activeSelf||!EnoughPoints)return;Calls++;_openingChest=true;_shopItem._waitingForServer=true;Steam.ChestExchanger.IsExchanging=true;}
  public void Finish(){ChestIsReady=false;_openingChest=false;_shopItem._waitingForServer=false;Steam.ChestExchanger.IsExchanging=false;}
 }
}namespace Steamworks{public struct CSteamID{public ulong m_SteamID;}}
namespace Heathen.SteamworksIntegration
{
 public struct UserData{public ulong SteamId;public bool Self;public bool IsMe{get{return Self;}}public bool IsValid{get{return SteamId!=0;}}}
 public struct LobbyData{public static LobbyData Current;public Steamworks.CSteamID SteamId;public LobbyMemberData[] Members;public bool IsValid{get{return SteamId.m_SteamID!=0;}}}
 public struct LobbyMemberData{public LobbyData lobby;public UserData user;public System.Collections.Generic.Dictionary<string,string> Values;public string this[string key]{get{string value;return Values!=null&&Values.TryGetValue(key,out value)?value:null;}}}
 public class SteamUserData:UnityEngine.Object{public UserData Data;}
 public class SteamLobbyMemberData:UnityEngine.Object{public LobbyMemberData Data;}
}
namespace BongoCat
{
 public enum ChestType{Normal,Emote}
}
namespace BongoCat.Multiplayer
{
 public class SetActiveBasedOnLobbyMemberValue:UnityEngine.Behaviour
 {public UnityEngine.GameObject _gameObject=new UnityEngine.GameObject();public string _key;public Heathen.SteamworksIntegration.SteamLobbyMemberData _lobbyMemberData;public bool _frozen;}
 public class OpenChestForMember:UnityEngine.Behaviour
 {
  public Heathen.SteamworksIntegration.SteamUserData _targetUser;public BongoCat.ChestType _chestType;public SetActiveBasedOnLobbyMemberValue _setChestActive;public bool _hasClicked,Uncertain,Throw;public int Calls;
  public void OnClicked(){if(_hasClicked)return;var shop=_chestType==BongoCat.ChestType.Normal?BongoCat.Shop.NormalShop:BongoCat.Shop.EmoteShop;if(!shop.CanAffordChest)return;Calls++;shop.Gifts++;shop.Points-=shop.Price;if(Throw)throw new System.Exception("synthetic response lost");if(!Uncertain){_hasClicked=true;_setChestActive._frozen=true;shop._pendingGiftPayoffs.Enqueue(delegate{});}}
 }
}