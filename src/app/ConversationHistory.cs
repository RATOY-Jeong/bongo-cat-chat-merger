using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
namespace BongoChat
{
    public sealed class MeowAuthor
    {
        public string key { get; set; }
        public string name { get; set; }
        public long count { get; set; }
    }
    internal sealed class MeowAccumulator
    {
        private string firstId, firstUtc, lastUtc, room;
        private long count;
        private bool moreAuthors;
        private readonly Dictionary<string,MeowAuthor> authors = new Dictionary<string,MeowAuthor>(StringComparer.Ordinal);
        private readonly HashSet<string> phrases = new HashSet<string>(StringComparer.Ordinal);
        public MeowAccumulator(ChatEntry item) { firstId=item.id;firstUtc=lastUtc=item.utc;room=item.room;Add(item,false); }
        public static MeowAccumulator FromGroup(ChatEntry group)
        {
            var result=new MeowAccumulator();result.firstId=group.id.Substring(5);result.firstUtc=group.utc;result.lastUtc=group.meowLastUtc;result.room=group.room;result.count=group.meowCount;result.moreAuthors=group.meowMoreAuthors;
            foreach(var a in group.meowAuthors ?? new List<MeowAuthor>())result.authors[a.key]=new MeowAuthor{key=a.key,name=a.name,count=a.count};
            foreach(string p in (group.text ?? "").Split('\n'))if(p.Length>0)result.phrases.Add(p);
            return result;
        }
        private MeowAccumulator() { }
        public void Add(ChatEntry item,bool older)
        {
            count++;
            if(older){firstId=item.id;firstUtc=item.utc;}else lastUtc=item.utc;
            phrases.Add(item.text);
            string key=!String.IsNullOrEmpty(item.sender)?"id:"+item.sender:"name:"+(item.name??"");
            MeowAuthor author;
            if(authors.TryGetValue(key,out author))author.count++;
            else if(authors.Count<128)authors.Add(key,new MeowAuthor{key=key,name=item.name,count=1});
            else moreAuthors=true;
        }
        public ChatEntry Snapshot()
        {
            var list=authors.Values.OrderByDescending(a=>a.count).ThenBy(a=>a.key,StringComparer.Ordinal).Select(a=>new MeowAuthor{key=a.key,name=a.name,count=a.count}).ToList();
            return new ChatEntry{kind="meow_group",id="meow:"+firstId,room=room,utc=firstUtc,meowLastUtc=lastUtc,meowCount=count,meowMoreAuthors=moreAuthors,meowAuthors=list,
                name=String.Join("\n",list.Select(a=>a.name??"")),text=String.Join("\n",phrases.OrderBy(p=>p,StringComparer.Ordinal))};
        }
    }
    // A worker-owned cache. First load scans backwards until 500 ordinary events;
    // later polls consume only appended bytes. Synthetic meow groups stay in memory.
    public sealed class ConversationHistory
    {
        private const int BlockSize=65536, MaxLine=65536, SeenLimit=8192;
        private readonly LinkedList<ChatEntry> items=new LinkedList<ChatEntry>();
        private readonly HashSet<string> seen=new HashSet<string>(StringComparer.Ordinal), normalIds=new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> seenOrder=new Queue<string>();
        private readonly JavaScriptSerializer json=new JavaScriptSerializer{MaxJsonLength=MaxLine};
        private readonly byte[] pending=new byte[MaxLine];
        private string path, room;
        private long position;
        private int limit, normalCount, pendingCount;
        private bool skipping, valid;
        private byte[] guard=new byte[0];
        private DateTime stamp;
        private MeowAccumulator lastGroup;
        public long LastBytesRead {get;private set;}
        public List<ChatEntry> Read(string file,string selectedRoom,int maximum,CancellationToken cancel)
        {
            LastBytesRead=0;
            if(maximum<=0)return new List<ChatEntry>();
            try
            {
                using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
                {
                    long length=stream.Length;DateTime write=File.GetLastWriteTimeUtc(file);
                    bool reload=!valid||!String.Equals(path,file,StringComparison.OrdinalIgnoreCase)||room!=selectedRoom||limit!=maximum||length<position;
                    if(!reload && length==position && write!=stamp)reload=true;
                    if(!reload && guard.Length>0)
                    {
                        byte[] check=new byte[guard.Length];stream.Position=position-guard.Length;
                        if(stream.Read(check,0,check.Length)!=check.Length||!check.SequenceEqual(guard))reload=true;
                        LastBytesRead+=check.Length;
                    }
                    if(reload)Reload(stream,length,selectedRoom,maximum,cancel);
                    else if(length>position)ReadAppend(stream,length,cancel);
                    cancel.ThrowIfCancellationRequested();
                    guard=new byte[(int)Math.Min(128,position)];stream.Position=position-guard.Length;
                    if(stream.Read(guard,0,guard.Length)!=guard.Length)throw new IOException("Journal changed while being read.");
                    LastBytesRead+=guard.Length;
                    path=file;room=selectedRoom;limit=maximum;stamp=write;valid=true;
                    return items.ToList();
                }
            }
            catch(FileNotFoundException){Reset();return new List<ChatEntry>();}
            catch(DirectoryNotFoundException){Reset();return new List<ChatEntry>();}
            catch{valid=false;throw;}
        }
        private void Reset()
        {
            valid=false;items.Clear();seen.Clear();seenOrder.Clear();normalIds.Clear();normalCount=0;pendingCount=0;skipping=false;lastGroup=null;
        }
        private bool Remember(string id)
        {
            if(normalIds.Contains(id)||!seen.Add(id))return false;
            seenOrder.Enqueue(id);if(seenOrder.Count>SeenLimit)seen.Remove(seenOrder.Dequeue());return true;
        }
        private ChatEntry Parse(string line,string selectedRoom)
        {
            try
            {
                var entry=json.Deserialize<ChatEntry>(line.TrimStart('\uFEFF'));
                if(entry==null||String.IsNullOrEmpty(entry.id)||entry.text==null||entry.room!=selectedRoom)return null;
                return entry.kind=="chat"||(entry.kind=="emote"&&entry.emoteId>0)?entry:null;
            }
            catch(ArgumentException){return null;}catch(InvalidOperationException){return null;}
        }
        private static string Decode(byte[] bytes,int count)
        {
            if(count>0&&bytes[count-1]==13)count--;
            try{return new UTF8Encoding(false,true).GetString(bytes,0,count);}catch(DecoderFallbackException){return "";}
        }
        private void Reload(FileStream stream,long length,string selectedRoom,int maximum,CancellationToken cancel)
        {
            Reset();room=selectedRoom;limit=maximum;
            long end=length;
            // Retain an unfinished appended record for the next poll, without displaying it twice.
            if(end>0)
            {
                stream.Position=end-1;
                if(stream.ReadByte()!=10)
                {
                    long cursor=end,complete=0;byte[] tail=new byte[BlockSize];
                    while(cursor>0){cancel.ThrowIfCancellationRequested();int size=(int)Math.Min(tail.Length,cursor);cursor-=size;stream.Position=cursor;int got=stream.Read(tail,0,size);LastBytesRead+=got;if(got!=size)throw new IOException("Journal changed while being read.");int newline=Array.LastIndexOf(tail,(byte)10,got-1,got);if(newline>=0){complete=cursor+newline+1;break;}}
                    long remainder=end-complete;skipping=remainder>MaxLine;pendingCount=skipping?0:(int)remainder;
                    if(pendingCount>0){stream.Position=complete;if(stream.Read(pending,0,pendingCount)!=pendingCount)throw new IOException("Journal changed while being read.");LastBytesRead+=pendingCount;}
                    end=complete;
                }
            }
            var newest=new List<ChatEntry>();MeowAccumulator group=null;
            foreach(string line in ReverseLines(stream,end,cancel))
            {
                cancel.ThrowIfCancellationRequested();var entry=Parse(line,selectedRoom);if(entry==null||!Remember(entry.id))continue;
                if(entry.kind=="chat"&&MeowMessages.IsMeow(entry.text))
                {if(group==null)group=new MeowAccumulator(entry);else group.Add(entry,true);continue;}
                if(group!=null){newest.Add(group.Snapshot());group=null;}
                newest.Add(entry);normalIds.Add(entry.id);normalCount++;
                if(normalCount>=maximum)break;
            }
            if(group!=null)newest.Add(group.Snapshot());
            newest.Reverse();foreach(var entry in newest)items.AddLast(entry);
            if(items.Last!=null&&items.Last.Value.kind=="meow_group")lastGroup=MeowAccumulator.FromGroup(items.Last.Value);
            position=length;
        }
        private IEnumerable<string> ReverseLines(FileStream stream,long end,CancellationToken cancel)
        {
            byte[] block=new byte[BlockSize],line=new byte[MaxLine];int count=0;bool oversized=false;long cursor=end;
            while(cursor>0)
            {
                cancel.ThrowIfCancellationRequested();int size=(int)Math.Min(block.Length,cursor);cursor-=size;stream.Position=cursor;int read=0,n;
                while(read<size&&(n=stream.Read(block,read,size-read))>0)read+=n;
                LastBytesRead+=read;if(read!=size)throw new IOException("Journal changed while being read.");
                for(int i=read-1;i>=0;i--)
                {
                    byte b=block[i];
                    if(b==10){if(!oversized&&count>0){Array.Reverse(line,0,count);yield return Decode(line,count);}count=0;oversized=false;}
                    else if(!oversized){if(count==line.Length){count=0;oversized=true;}else line[count++]=b;}
                }
            }
            if(!oversized&&count>0){Array.Reverse(line,0,count);yield return Decode(line,count);}
        }
        private void ReadAppend(FileStream stream,long end,CancellationToken cancel)
        {
            stream.Position=position;byte[] block=new byte[BlockSize];
            while(position<end)
            {
                cancel.ThrowIfCancellationRequested();int read=stream.Read(block,0,(int)Math.Min(block.Length,end-position));if(read==0)throw new IOException("Journal changed while being read.");LastBytesRead+=read;
                for(int i=0;i<read;i++)
                {
                    byte b=block[i];
                    if(b==10){if(!skipping&&pendingCount>0){var entry=Parse(Decode(pending,pendingCount),room);if(entry!=null&&Remember(entry.id))Append(entry);}pendingCount=0;skipping=false;}
                    else if(!skipping){if(pendingCount==pending.Length){pendingCount=0;skipping=true;}else pending[pendingCount++]=b;}
                }
                position+=read;
            }
        }
        private void Append(ChatEntry entry)
        {
            if(entry.kind=="chat"&&MeowMessages.IsMeow(entry.text))
            {
                if(lastGroup==null){lastGroup=new MeowAccumulator(entry);items.AddLast(lastGroup.Snapshot());}
                else{lastGroup.Add(entry,false);items.Last.Value=lastGroup.Snapshot();}
                return;
            }
            lastGroup=null;items.AddLast(entry);normalIds.Add(entry.id);normalCount++;
            while(normalCount>limit)
            {
                while(items.First!=null&&items.First.Value.kind=="meow_group")items.RemoveFirst();
                if(items.First==null)break;normalIds.Remove(items.First.Value.id);items.RemoveFirst();normalCount--;
                while(items.First!=null&&items.First.Value.kind=="meow_group")items.RemoveFirst();
            }
        }
    }
}