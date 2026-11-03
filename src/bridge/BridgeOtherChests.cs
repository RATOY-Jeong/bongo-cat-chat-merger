using System;using System.Collections.Generic;using System.IO;using System.Reflection;using System.Text;using BongoChat.Protocol;using HarmonyLib;using Newtonsoft.Json;using UnityEngine;using Heathen.SteamworksIntegration;
namespace BongoChatBridge
{
 public sealed partial class BridgePlugin
 {
  private bool autoOtherChestSupported;private string autoOtherChestStatus="off";private int autoOtherChestRequests;
  private Type otherChestType,otherChestActiveType;private FieldInfo otherTarget,otherKind,otherClicked,otherActive,otherActiveObject,otherActiveKey,otherActiveMember,otherFrozen;
  private MethodInfo otherClick,otherCanAfford;private float nextOtherChestRead;
  private readonly HashSet<string> otherChestAttempted=new HashSet<string>();
  private void InitializeOtherChests()
  {
   try{
    if(!autoChestSupported)throw new NotSupportedException("Base chest API unavailable.");
    otherChestType=Type.GetType("BongoCat.Multiplayer.OpenChestForMember, Assembly-CSharp",true);otherChestActiveType=Type.GetType("BongoCat.Multiplayer.SetActiveBasedOnLobbyMemberValue, Assembly-CSharp",true);
    var chestKind=Type.GetType("BongoCat.ChestType, Assembly-CSharp",true);if(!chestKind.IsEnum)throw new NotSupportedException("Chest kind changed.");
    otherTarget=RequiredChestField(otherChestType,"_targetUser",typeof(SteamUserData));otherKind=RequiredChestField(otherChestType,"_chestType",chestKind);otherClicked=RequiredChestField(otherChestType,"_hasClicked",typeof(bool));otherActive=RequiredChestField(otherChestType,"_setChestActive",otherChestActiveType);
    otherActiveObject=RequiredChestField(otherChestActiveType,"_gameObject",typeof(GameObject));otherActiveKey=RequiredChestField(otherChestActiveType,"_key",typeof(string));otherActiveMember=RequiredChestField(otherChestActiveType,"_lobbyMemberData",typeof(SteamLobbyMemberData));otherFrozen=RequiredChestField(otherChestActiveType,"_frozen",typeof(bool));
    otherClick=AccessTools.Method(otherChestType,"OnClicked",Type.EmptyTypes);otherCanAfford=AccessTools.Method(chestShopType,"get_CanAffordChest",Type.EmptyTypes);
    if(otherClick==null||otherClick.ReturnType!=typeof(void)||otherClick.IsStatic||otherCanAfford==null||otherCanAfford.ReturnType!=typeof(bool)||otherCanAfford.IsStatic)throw new NotSupportedException("Gift click API changed.");
    autoOtherChestSupported=true;
   }catch(Exception ex){autoOtherChestSupported=false;autoOtherChestStatus="unsupported";Logger.LogWarning("Other members' chests unavailable: "+ex.GetType().Name);}
  }
  private static string OtherChestKey(string room,ulong user,int kind){return room+":"+user.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+kind;}
  private void UpdateOtherChests(float now)
  {
   if(!autoOtherChestSupported||now<nextOtherChestRead)return;nextOtherChestRead=now+1;
   try{
    LobbyData lobby=LobbyData.Current;string room=lobby.IsValid?lobby.SteamId.m_SteamID.ToString():"";
    AutoChestLease lease=null;string file=Path.Combine(directory,"other-chests.json");
    if(File.Exists(file)&&new FileInfo(file).Length<=4096)using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new StreamReader(stream,Encoding.UTF8)){char[] data=new char[4097];int count=reader.ReadBlock(data,0,data.Length);if(count<=4096)lease=JsonConvert.DeserializeObject<AutoChestLease>(new string(data,0,count));}
    bool valid=AutoChestPolicy.ValidForOthers(lease,session,room,DateTime.UtcNow);
    if(valid&&!lease.enabled)otherChestAttempted.Clear();
    if(!valid||!lease.enabled){autoOtherChestStatus="off";return;}
    if(now<nextChestAction){autoOtherChestStatus="opening";return;}
    object normal=normalShopField.GetValue(null),emote=emoteShopField.GetValue(null);var normalState=ReadChest(normal,false);var emoteState=ReadChest(emote,true);
    if(!normalState.Known||!emoteState.Known){autoOtherChestStatus="waiting";return;}
    if(normalState.Busy||emoteState.Busy){autoOtherChestStatus="opening";return;}
    var members=lobby.Members;if(members==null){autoOtherChestStatus="waiting";return;}
    var memberMap=new Dictionary<ulong,LobbyMemberData>();
    foreach(var member in members){if(!member.user.IsValid||member.user.IsMe||member.lobby.SteamId.m_SteamID!=lobby.SteamId.m_SteamID)continue;memberMap[member.user.SteamId]=member;for(int kind=0;kind<2;kind++){bool ready;if(Boolean.TryParse(member[kind==0?"chest_ready":"emote_chest_ready"],out ready)&&!ready)otherChestAttempted.Remove(OtherChestKey(room,member.user.SteamId,kind));}}
    autoOtherChestStatus="waiting";
    foreach(UnityEngine.Object obj in Resources.FindObjectsOfTypeAll(otherChestType)){
     var component=obj as Behaviour;if(component==null||!component||component.GetType()!=otherChestType||!component.isActiveAndEnabled||!component.gameObject.activeInHierarchy||!component.gameObject.scene.IsValid()||!component.gameObject.scene.isLoaded)continue;
     if((bool)otherClicked.GetValue(obj))continue;
     var target=otherTarget.GetValue(obj) as SteamUserData;if(target==null||!target)continue;var user=target.Data;if(!user.IsValid||user.IsMe)continue;
     int kind=Convert.ToInt32(otherKind.GetValue(obj));if(kind<0||kind>1)continue;
     LobbyMemberData member;if(!memberMap.TryGetValue(user.SteamId,out member))continue;string key=kind==0?"chest_ready":"emote_chest_ready";bool ready;if(!Boolean.TryParse(member[key],out ready)||!ready)continue;
     object active=otherActive.GetValue(obj);var activeComponent=active as Component;if(activeComponent==null||!activeComponent||active.GetType()!=otherChestActiveType||(bool)otherFrozen.GetValue(active))continue;
     var visible=otherActiveObject.GetValue(active) as GameObject;var memberData=otherActiveMember.GetValue(active) as SteamLobbyMemberData;
     if(visible==null||!visible||!visible.activeInHierarchy||!visible.scene.IsValid()||!visible.scene.isLoaded||memberData==null||!memberData||(string)otherActiveKey.GetValue(active)!=key)continue;
     var binding=memberData.Data;if(binding.user.SteamId!=user.SteamId||binding.lobby.SteamId.m_SteamID!=lobby.SteamId.m_SteamID)continue;
     string attempt=OtherChestKey(room,user.SteamId,kind);if(otherChestAttempted.Contains(attempt))continue;
     if(!(bool)otherCanAfford.Invoke(kind==0?normal:emote,null))continue;
     if(otherChestAttempted.Count>=4096){autoOtherChestStatus="limit";return;}
     // Only the native visible member-chest click. This spends OUR points as the game normally does.
     otherChestAttempted.Add(attempt);nextChestAction=now+2;otherClick.Invoke(obj,null);autoOtherChestRequests++;autoOtherChestStatus="sent";return;
    }
   }catch(Exception ex){autoOtherChestStatus="error";Logger.LogWarning("Other members' chest opening paused: "+ex.GetType().Name);nextOtherChestRead=now+5;}
  }
 }
}