using System;using System.IO;using System.Reflection;using System.Text;using BongoChat.Protocol;using HarmonyLib;using Newtonsoft.Json;using UnityEngine;
namespace BongoChatBridge
{
    public sealed partial class BridgePlugin
    {
        private Type chestShopType,chestItemType,chestExchangeType;
        private FieldInfo normalShopField,emoteShopField,shopItemField,chestReadyField,stockLeftField,shopOpeningField,shopEmoteField,itemWaitingField,itemShopField,itemEmoteField,chestExchangeBusy;
        private MethodInfo canGetChest,chestClick;
        private bool autoChestSupported;
        private string autoChestStatus="off";
        private readonly AutoChestGate autoChestGate=new AutoChestGate();
        private float nextAutoChestRead;
        private int autoChestRequests;
        private void InitializeAutoChests()
        {
            try
            {
                chestShopType=Type.GetType("BongoCat.Shop, Assembly-CSharp",true);chestItemType=Type.GetType("BongoCat.ShopItem, Assembly-CSharp",true);chestExchangeType=Type.GetType("Steam.ChestExchanger, Assembly-CSharp",true);
                normalShopField=RequiredChestField(chestShopType,"NormalShop",chestShopType);emoteShopField=RequiredChestField(chestShopType,"EmoteShop",chestShopType);
                shopItemField=RequiredChestField(chestShopType,"_shopItem",chestItemType);chestReadyField=RequiredChestField(chestShopType,"ChestIsReady",typeof(bool));stockLeftField=RequiredChestField(chestShopType,"StockRefreshTimeLeft",typeof(int));
                shopOpeningField=RequiredChestField(chestShopType,"_openingChest",typeof(bool));shopEmoteField=RequiredChestField(chestShopType,"_isEmoteShop",typeof(bool));
                itemWaitingField=RequiredChestField(chestItemType,"_waitingForServer",typeof(bool));itemShopField=RequiredChestField(chestItemType,"_shop",chestShopType);itemEmoteField=RequiredChestField(chestItemType,"_isEmoteChest",typeof(bool));chestExchangeBusy=RequiredChestField(chestExchangeType,"IsExchanging",typeof(bool));
                canGetChest=AccessTools.Method(chestShopType,"get_CanGetChest",Type.EmptyTypes);chestClick=AccessTools.Method(chestShopType,"OnClick",Type.EmptyTypes);
                if(canGetChest==null||canGetChest.ReturnType!=typeof(bool)||chestClick==null||chestClick.ReturnType!=typeof(void)||!normalShopField.IsStatic||!emoteShopField.IsStatic||!chestExchangeBusy.IsStatic)throw new NotSupportedException("Chest API changed.");
                autoChestSupported=true;
            }
            catch(Exception ex){autoChestSupported=false;autoChestStatus="unsupported";Logger.LogWarning("Automatic chests unavailable: "+ex.GetType().Name);}
        }
        private static FieldInfo RequiredChestField(Type type,string name,Type fieldType)
        {
            var field=AccessTools.Field(type,name);if(field==null||field.FieldType!=fieldType)throw new NotSupportedException("Chest field changed: "+name);return field;
        }
        private ChestReadiness ReadChest(object shop,bool emote)
        {
            var state=new ChestReadiness{Known=false};var component=shop as Component;
            if(component==null||!component||!component.gameObject.activeInHierarchy||shop.GetType()!=chestShopType)return state;
            if((bool)shopEmoteField.GetValue(shop)!=emote)return state;
            object item=shopItemField.GetValue(shop);var itemComponent=item as Component;
            if(itemComponent==null||!itemComponent||item.GetType()!=chestItemType||!System.Object.ReferenceEquals(itemShopField.GetValue(item),shop)||(bool)itemEmoteField.GetValue(item)!=emote)return state;
            state.Known=true;
            state.Ready=(bool)chestReadyField.GetValue(shop);
            state.Busy=(bool)shopOpeningField.GetValue(shop)||(bool)itemWaitingField.GetValue(item)||(bool)chestExchangeBusy.GetValue(null);
            state.CanOpen=state.Ready&&!state.Busy&&(int)stockLeftField.GetValue(shop)<=0&&itemComponent.gameObject.activeSelf&&(bool)canGetChest.Invoke(shop,null);
            return state;
        }
        private void UpdateAutoChests(float now)
        {
            if(!autoChestSupported||now<nextAutoChestRead)return;nextAutoChestRead=now+0.5f;
            try
            {
                AutoChestLease lease=null;string file=Path.Combine(directory,"auto-chests.json");
                if(File.Exists(file)&&new FileInfo(file).Length<=4096)using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new StreamReader(stream,Encoding.UTF8)){char[] data=new char[4097];int count=reader.ReadBlock(data,0,data.Length);if(count<=4096)lease=JsonConvert.DeserializeObject<AutoChestLease>(new string(data,0,count));}
                bool valid=AutoChestPolicy.Valid(lease,session,DateTime.UtcNow),enabled=valid&&lease.enabled;
                if(valid&&!lease.enabled)autoChestGate.Reset();

                if(!enabled){autoChestStatus="off";return;}
                object normal=normalShopField.GetValue(null),emote=emoteShopField.GetValue(null);
                var normalState=ReadChest(normal,false);var emoteState=ReadChest(emote,true);
                int next=autoChestGate.Next(true,normalState,emoteState,now);autoChestStatus=autoChestGate.State;
                if(next>0)
                {
                    // The same guarded click path used by the game's own chest. No timer/currency edits.
                    chestClick.Invoke(next==1?normal:emote,null);autoChestRequests++;
                }
            }
            catch(Exception ex){autoChestStatus="error";Logger.LogWarning("Automatic chest paused: "+ex.GetType().Name);nextAutoChestRead=now+5;}
        }
    }
}