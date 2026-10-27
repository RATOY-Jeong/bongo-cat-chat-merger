using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
namespace BongoChat
{
    public sealed partial class ChatWindow
    {
        private string MeowGroupLabel(ChatEntry entry)
        {
            int people=entry.meowAuthors==null?0:entry.meowAuthors.Count;
            return F(entry.meowMoreAuthors?"야옹 {0:N0}회 · {1}명 이상":"야옹 {0:N0}회 · {1}명",entry.meowCount,people);
        }
        private string MeowAuthorSummary(ChatEntry entry)
        {
            var authors=entry.meowAuthors??new List<MeowAuthor>();
            string text=String.Join("\n",authors.Take(12).Select(a=>(a.name??L("고양이"))+F(" · {0:N0}회",a.count)));
            if(authors.Count>12||entry.meowMoreAuthors)text+="\n"+L("그 밖의 참여자도 포함돼요");
            return text;
        }
        private UIElement MeowGroupBubble(ChatEntry entry,DateTime first)
        {
            DateTime last;if(!DateTime.TryParse(entry.meowLastUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out last))last=first;else last=last.ToLocalTime();
            string range=first.ToString("t",UiText.Culture(language));if(last!=first)range+="–"+last.ToString(last.Date==first.Date?"t":"MM-dd HH:mm",UiText.Culture(language));
            string tooltip=L("연속된 야옹을 묶었어요. 최근 대화 500개 한도에는 포함하지 않아요.")+"\n"+first.ToString("yyyy-MM-dd HH:mm:ss")+" – "+last.ToString("yyyy-MM-dd HH:mm:ss")+"\n"+MeowAuthorSummary(entry);
            var content=new StackPanel();
            var label=SelectableMessageText(new ChatEntry{text=MeowGroupLabel(entry)});label.HorizontalContentAlignment=HorizontalAlignment.Center;label.ToolTip=tooltip;content.Children.Add(label);
            content.Children.Add(new TextBlock{Text=range,FontSize=9,Foreground=Brush("#718795"),HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,2,0,0)});
            return new Border{Child=content,Background=Brush("#D3E0E9"),CornerRadius=new CornerRadius(10),Padding=new Thickness(12,6,12,6),Margin=new Thickness(0,0,0,14),MaxWidth=Math.Max(180,(ActualWidth>0?ActualWidth:Width)-65),HorizontalAlignment=HorizontalAlignment.Center,ToolTip=tooltip};
        }
        private void RefreshLastMeowGroup(List<ChatEntry> visible,string context)
        {
            int count=renderedVisible.Count;if(renderedContext!=context||count==0||visible.Count<count||messages.Children.Count==0)return;
            ChatEntry old=renderedVisible[count-1],next=visible[count-1];
            if(old.kind!="meow_group"||next.kind!="meow_group"||old.id!=next.id||old.utc!=next.utc||next.meowCount<old.meowCount||SameRenderedEntry(old,next))return;
            for(int i=0;i<count-1;i++)if(!SameRenderedEntry(renderedVisible[i],visible[i]))return;
            DateTime time;if(!DateTime.TryParse(next.utc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out time))time=DateTime.Now;else time=time.ToLocalTime();
            messages.Children.RemoveAt(messages.Children.Count-1);messages.Children.Add(MessageBubble(next,time));renderedVisible[count-1]=next;
        }
        private void ShowGroupedCount(List<ChatEntry> visible)
        {
            if(!entries.Any(e=>e.kind=="meow_group"))return;
            int normal=visible.Count(e=>e.kind!="meow_group");long meows=visible.Where(e=>e.kind=="meow_group").Sum(e=>e.meowCount);
            resultText.Text=hideMeows?F("내 PC에 기록 · 대화 {0}개 표시 · 야옹 숨김",normal):F("내 PC에 기록 · 대화 {0}개 표시 · 야옹 {1:N0}회",normal,meows);
        }
        private ChatEntry DemoMeowGroup(DateTime time)
        {
            var group=new MeowAccumulator(new ChatEntry{id="demo-meow-one",kind="chat",room="123",sender="demo-0",name="구름냥",text="야옹 야옹",utc=time.ToUniversalTime().ToString("o")});
            for(int i=1;i<23;i++)group.Add(new ChatEntry{id="demo-meow-"+i,kind="chat",room="123",sender="demo-"+(i%4),name=new[]{"구름냥","치즈","모카","나"}[i%4],text=i%2==0?"Meow Meow":"야옹 야옹",utc=time.AddSeconds(i).ToUniversalTime().ToString("o")},false);
            return group.Snapshot();
        }
    }
}