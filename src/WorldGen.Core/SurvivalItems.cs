using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    public enum SurvivalMode { Survival, Creative, Spectator }
    public enum InventoryView { List, Grid }
    public enum SurvivalItemKind { Resource, Tool, Weapon, Food, Container, Clothing, Medicine }
    public sealed class SurvivalItem
    {
        public string Id,Name,Description;public SurvivalItemKind Kind;
        public double Kg,Litres,Kcal,WaterMl,ShelfDays,Damage,Reach,Seconds,Insulation;
        public int Width=1,Height=1,Stack=1,StorageWidth,StorageHeight;public double StorageKg,StorageLitres;
        public bool TwoHands,Sharp,Raw;
        public double Load=>Math.Max(Kg,Litres*.45);
    }
    [Serializable] public sealed class SurvivalStack
    {
        public long Id;public string Item;public int Count=1;public double AgeDays,Condition=1;
        [System.Runtime.Serialization.OptionalField] public double Fraction=1;
        [System.Runtime.Serialization.OnDeserializing] void InitializeFraction(System.Runtime.Serialization.StreamingContext context){Fraction=1;}
        public int Container=-1,X,Y;public bool Rotated,Worn;
        public SurvivalStack Copy()=>(SurvivalStack)MemberwiseClone();
    }
    [Serializable] public sealed class SurvivalInventory
    {
        public long NextId=1;public InventoryView View;public long Equipped;
        public SurvivalStack[] Items=Array.Empty<SurvivalStack>();
        public SurvivalInventory Copy()=>new SurvivalInventory{NextId=NextId,View=View,Equipped=Equipped,Items=Items.Select(s=>s.Copy()).ToArray()};
        public double Mass=>Items.Sum(s=>SurvivalItems.Get(s.Item).Kg*s.Count*s.Fraction);
        public double Bulk=>Items.Sum(s=>SurvivalItems.Get(s.Item).Litres*s.Count*s.Fraction);
        public SurvivalItem Held=>Items.FirstOrDefault(s=>s.Id==Equipped) is SurvivalStack s?SurvivalItems.Get(s.Item):null;
        public int Count(string item)=>Items.Where(s=>s.Item==item).Sum(s=>s.Count);
        public bool Has(string item,int amount=1)=>Count(item)>=amount;
        // Containers are worn independently of either UI. Both inventory views use
        // exactly the same packing and load rules; changing UI cannot create room.
        public (int width,int height,double kg,double litres) Capacity
        {
            get
            {
                int width=0,height=0;double kg=0,litres=0;
                foreach(var s in Items){var d=SurvivalItems.Get(s.Item);if(d.StorageWidth==0)continue;
                    width=Math.Max(width,d.StorageWidth);height+=d.StorageHeight;kg+=d.StorageKg;litres+=d.StorageLitres;}
                return (width,height,kg,litres);
            }
        }
        public bool TryAdd(string item,int count,out string reason,double age=0,double fraction=1)
        {
            reason="";if(count<1||!SurvivalItems.TryGet(item,out var d)){reason="Invalid item or quantity.";return false;}
            if(!SurvivalMath.Finite(age)||age<0||!SurvivalMath.Finite(fraction)||fraction<=0||fraction>1||fraction!=1&&d.Kind!=SurvivalItemKind.Food){reason="Invalid portion.";return false;}
            var capacity=Capacity;
            if(d.StorageWidth>0&&(count!=1||Has(item))){reason="That carrying attachment is already worn.";return false;}
            if(d.StorageWidth==0&&(d.Kg*count*fraction>capacity.kg+16||d.Litres*count*fraction>capacity.litres+24))
            {reason="Your hands and carrying equipment are full.";return false;}
            var proposed=Copy();int remaining=count;
            foreach(var s in proposed.Items.Where(s=>s.Item==item&&Math.Abs(s.AgeDays-age)<.05&&Math.Abs(s.Fraction-fraction)<1e-8&&s.Condition>.99))
            {int n=Math.Min(remaining,d.Stack-s.Count);s.Count+=n;remaining-=n;if(remaining==0)break;}
            var list=proposed.Items.ToList();while(remaining>0){int n=Math.Min(remaining,d.Stack);list.Add(new SurvivalStack{Id=proposed.NextId++,Item=item,Count=n,AgeDays=age,Fraction=fraction});remaining-=n;}
            proposed.Items=list.ToArray();if(!proposed.Pack(out reason))return false;
            Items=proposed.Items;NextId=proposed.NextId;return true;
        }
        public bool Pack(out string reason)
        {
            var trial=Copy();
            if(!trial.PackLayout(true,out reason))
            {
                if(View!=InventoryView.List)return false;
                trial=Copy();if(!trial.PackLayout(false,out reason))return false;
            }
            Items=trial.Items;return true;
        }
        int[] RowWidths()
        {
            var rows=new List<int>();foreach(var s in Items){var d=SurvivalItems.Get(s.Item);for(int y=0;y<d.StorageHeight;y++)rows.Add(d.StorageWidth);}return rows.ToArray();
        }
        bool PackLayout(bool retainLayout,out string reason)
        {
            reason="";var capacity=Capacity;var cells=new bool[Math.Max(1,capacity.width),Math.Max(1,capacity.height)];
            var rows=RowWidths();for(int y=0;y<rows.Length;y++)for(int x=rows[y];x<capacity.width;x++)cells[x,y]=true;
            int hands=0;double kg=0,litres=0;
            // Wear at most one of each carrying attachment, never bags inside bags.
            var worn=new HashSet<string>();
            foreach(var s in Items.Where(s=>SurvivalItems.Get(s.Item).StorageWidth>0||s.Worn))
            {if(s.Count!=1||!worn.Add(s.Item)||s.Worn&&SurvivalItems.Get(s.Item).Kind!=SurvivalItemKind.Clothing){reason="That attachment is already worn or cannot be worn.";return false;}s.Container=-2;}
            foreach(var s in Items.Where(s=>SurvivalItems.Get(s.Item).StorageWidth==0&&!s.Worn).OrderByDescending(s=>s.Id==Equipped).ThenByDescending(s=>retainLayout&&s.Container==0).ThenByDescending(s=>SurvivalItems.Get(s.Item).Width*SurvivalItems.Get(s.Item).Height))
            {
                var d=SurvivalItems.Get(s.Item);int need=d.TwoHands?2:1;
                bool inHand=(s.Id==Equipped||capacity.width==0)&&hands+need<=2&&s.Count*d.Kg*s.Fraction<=8&&s.Count*d.Litres*s.Fraction<=12;
                if(inHand){s.Container=-1;s.X=hands;s.Y=0;s.Rotated=false;hands+=need;continue;}
                kg+=d.Kg*s.Count*s.Fraction;litres+=d.Litres*s.Count*s.Fraction;
                if(kg>capacity.kg+1e-8||litres>capacity.litres+1e-8){reason="Your hands and carrying equipment are full.";return false;}
                bool placed=false;
                if(retainLayout&&s.Container==0)
                {
                    int w=s.Rotated?d.Height:d.Width,h=s.Rotated?d.Width:d.Height;bool free=s.X>=0&&s.Y>=0&&s.X+w<=capacity.width&&s.Y+h<=capacity.height;
                    if(free)for(int y=s.Y;y<s.Y+h;y++)for(int x=s.X;x<s.X+w;x++)if(cells[x,y])free=false;
                    if(free){for(int y=s.Y;y<s.Y+h;y++)for(int x=s.X;x<s.X+w;x++)cells[x,y]=true;placed=true;}
                }
                for(int turn=0;turn<2&&!placed;turn++)
                {
                    int w=turn==0?d.Width:d.Height,h=turn==0?d.Height:d.Width;
                    for(int y=0;y<=capacity.height-h&&!placed;y++)for(int x=0;x<=capacity.width-w&&!placed;x++)
                    {bool free=true;for(int yy=y;yy<y+h;yy++)for(int xx=x;xx<x+w;xx++)if(cells[xx,yy])free=false;
                        if(!free)continue;for(int yy=y;yy<y+h;yy++)for(int xx=x;xx<x+w;xx++)cells[xx,yy]=true;
                        s.Container=0;s.X=x;s.Y=y;s.Rotated=turn==1;placed=true;}
                }
                if(!placed){reason="This item will not fit. Drop something or add carrying equipment.";return false;}
            }
            return true;
        }
        public bool Move(long id,int x,int y,bool rotated,out string reason)
        {
            reason="";var s=Items.FirstOrDefault(i=>i.Id==id);if(s==null||s.Container!=0){reason="Only stored items can be rearranged.";return false;}
            var d=SurvivalItems.Get(s.Item);var capacity=Capacity;int w=rotated?d.Height:d.Width,h=rotated?d.Width:d.Height;var rows=RowWidths();
            if(x<0||y<0||x+w>capacity.width||y+h>capacity.height||Enumerable.Range(y,h).Any(row=>x+w>rows[row])){reason="Outside the carrying attachment.";return false;}
            foreach(var other in Items.Where(i=>i.Container==0&&i.Id!=id))
            {var od=SurvivalItems.Get(other.Item);int ow=other.Rotated?od.Height:od.Width,oh=other.Rotated?od.Width:od.Height;if(x<other.X+ow&&x+w>other.X&&y<other.Y+oh&&y+h>other.Y){reason="Another item occupies that space.";return false;}}
            s.X=x;s.Y=y;s.Rotated=rotated;return true;
        }
        public bool Compact(out string reason)
        {
            // A greedy arrangement can fail even when the existing layout fits.
            // Never publish a partially packed bag with overlapping old/new slots.
            var trial=Copy();if(!trial.PackLayout(false,out reason))return false;
            Items=trial.Items;return true;
        }
        public bool Remove(string item,int amount)
        {
            if(amount<1||!Has(item,amount))return false;
            var trial=Copy();foreach(var s in trial.Items.Where(s=>s.Item==item).OrderByDescending(s=>s.AgeDays)){int n=Math.Min(amount,s.Count);s.Count-=n;amount-=n;if(amount==0)break;}
            trial.Items=trial.Items.Where(s=>s.Count>0).ToArray();if(!trial.Items.Any(s=>s.Id==trial.Equipped))trial.Equipped=0;
            if(!trial.Pack(out _))return false;Items=trial.Items;Equipped=trial.Equipped;return true;
        }
        public bool RemoveStack(long id,int amount)
        {
            var trial=Copy();var stack=trial.Items.FirstOrDefault(i=>i.Id==id);if(stack==null||amount<1||stack.Count<amount)return false;
            stack.Count-=amount;trial.Items=trial.Items.Where(s=>s.Count>0).ToArray();if(!trial.Items.Any(s=>s.Id==trial.Equipped))trial.Equipped=0;
            if(!trial.Pack(out _))return false;Items=trial.Items;Equipped=trial.Equipped;return true;
        }
        public bool Equip(long id,out string reason)
        {long old=Equipped;Equipped=Equipped==id?0:id;if(Pack(out reason))return true;Equipped=old;Pack(out _);return false;}
        public bool Wear(long id,out string reason)
        {
            var item=Items.FirstOrDefault(s=>s.Id==id);if(item==null||SurvivalItems.Get(item.Item).Kind!=SurvivalItemKind.Clothing){reason="This item cannot be worn.";return false;}
            item.Worn=!item.Worn;if(Pack(out reason))return true;item.Worn=!item.Worn;Pack(out _);return false;
        }
        public void Validate()
        {
            if(Items==null||NextId<1||!Enum.IsDefined(typeof(InventoryView),View))throw new ArgumentException("Invalid inventory.");
            var ids=new HashSet<long>();foreach(var s in Items)
                if(s==null||!ids.Add(s.Id)||s.Id<1||s.Id>=NextId||!SurvivalItems.TryGet(s.Item,out var d)||s.Count<1||s.Count>d.Stack||!SurvivalMath.Finite(s.AgeDays)||s.AgeDays<0||!SurvivalMath.Finite(s.Condition)||s.Condition<0||s.Condition>1||!SurvivalMath.Finite(s.Fraction)||s.Fraction<=0||s.Fraction>1||s.Fraction!=1&&d.Kind!=SurvivalItemKind.Food)throw new ArgumentException("Invalid inventory item.");
            if(Equipped!=0&&!ids.Contains(Equipped))throw new ArgumentException("Invalid held item.");
            if(!Copy().Pack(out _))throw new ArgumentException("Inventory exceeds carrying equipment.");
        }
    }
    public static class SurvivalItems
    {
        static readonly Dictionary<string,SurvivalItem> items=new Dictionary<string,SurvivalItem>(StringComparer.Ordinal);
        public static IEnumerable<SurvivalItem> All=>items.Values;
        static SurvivalItems()
        {
            void Add(string id,string name,SurvivalItemKind kind,double kg,double litres,int stack=1,int w=1,int h=1,string description="")=>items.Add(id,new SurvivalItem{Id=id,Name=name,Kind=kind,Kg=kg,Litres=litres,Stack=stack,Width=w,Height=h,Description=description});
            Add("stick","Straight branch",SurvivalItemKind.Resource,.35,1.2,6,3,1);
            Add("log","Wood section",SurvivalItemKind.Resource,5,9,1,4,2);
            Add("stone","Knapping stone",SurvivalItemKind.Resource,.6,.3,4);
            Add("flint","Flint / chert",SurvivalItemKind.Resource,.3,.15,6);
            Add("fibre","Plant fibre",SurvivalItemKind.Resource,.08,.3,12);
            Add("grass","Dry grass / sedge",SurvivalItemKind.Resource,.15,1.5,8,2,1);
            Add("tinder","Dry tinder",SurvivalItemKind.Resource,.03,.25,8);
            Add("bark","Bark sheet",SurvivalItemKind.Resource,.2,1,6,2,1);
            Add("cord","Plant-fibre cordage",SurvivalItemKind.Resource,.07,.2,12);
            Add("hide","Fresh hide",SurvivalItemKind.Resource,2.5,5,1,3,2);
            Add("small-hide","Small fresh pelt",SurvivalItemKind.Resource,.15,.5,4,2,1);
            Add("small-dryhide","Small dried pelt",SurvivalItemKind.Resource,.1,.35,4,2,1);
            Add("dryhide","Scraped, dried hide",SurvivalItemKind.Resource,1.2,3,1,3,2);
            Add("bone","Bone",SurvivalItemKind.Resource,.3,.35,6,2,1);
            Add("sinew","Sinew",SurvivalItemKind.Resource,.04,.12,8);
            Add("resin","Tree resin",SurvivalItemKind.Resource,.05,.06,8);
            Add("ochre","Ochre",SurvivalItemKind.Resource,.15,.1,6);
            Add("pyrite","Iron pyrite",SurvivalItemKind.Resource,.18,.08,2);
            Add("flake","Sharp stone flake",SurvivalItemKind.Tool,.12,.06,4);items["flake"].Sharp=true;items["flake"].Damage=8;items["flake"].Reach=1.2;
            Add("handaxe","Bifacial handaxe",SurvivalItemKind.Tool,.65,.35);items["handaxe"].Sharp=true;items["handaxe"].Damage=18;items["handaxe"].Reach=1.3;
            Add("scraper","Hide scraper",SurvivalItemKind.Tool,.2,.15);items["scraper"].Sharp=true;
            Add("knife","Hafted stone knife",SurvivalItemKind.Tool,.3,.2,1,2,1);items["knife"].Sharp=true;items["knife"].Damage=15;items["knife"].Reach=1.4;
            Add("awl","Bone awl",SurvivalItemKind.Tool,.08,.08);
            Add("needle","Bone needle",SurvivalItemKind.Tool,.02,.02);
            Add("hammer","Hammerstone",SurvivalItemKind.Tool,.65,.3);items["hammer"].Damage=12;items["hammer"].Reach=1.25;
            Add("wood-spear","Wooden spear",SurvivalItemKind.Weapon,.9,2,1,5,1);items["wood-spear"].Damage=30;items["wood-spear"].Reach=2.8;items["wood-spear"].TwoHands=true;
            Add("stone-spear","Stone-tipped spear",SurvivalItemKind.Weapon,1.15,2.2,1,5,1);items["stone-spear"].Damage=45;items["stone-spear"].Reach=3;items["stone-spear"].TwoHands=true;
            Add("throwing-stick","Throwing stick",SurvivalItemKind.Weapon,.5,.8,1,3,1);items["throwing-stick"].Damage=18;items["throwing-stick"].Reach=1.7;
            Add("club","Wooden club",SurvivalItemKind.Weapon,.9,1.1,1,3,1);items["club"].Damage=20;items["club"].Reach=1.7;
            Add("sling","Fibre sling",SurvivalItemKind.Weapon,.12,.2);items["sling"].Damage=22;items["sling"].Reach=1.4;
            Add("fire-drill","Hand-drill fire kit",SurvivalItemKind.Tool,.4,.9,1,3,1);
            Add("fire-striker","Flint-and-pyrite kit",SurvivalItemKind.Tool,.4,.25);
            Add("digging-stick","Digging stick",SurvivalItemKind.Tool,.6,1.2,1,4,1);
            Add("pouch","Hide belt pouch",SurvivalItemKind.Container,.3,.4);Store("pouch",4,2,4,5);
            Add("pack","Hide sling pack",SurvivalItemKind.Container,.9,1.5);Store("pack",6,5,15,24);
            Add("basket","Woven carrying basket",SurvivalItemKind.Container,1.2,2);Store("basket",5,4,10,18);
            Add("belt","Hide tool belt",SurvivalItemKind.Container,.25,.2);Store("belt",5,1,3,4);
            Add("waterskin","Filled hide waterskin",SurvivalItemKind.Food,1.15,1.2);items["waterskin"].WaterMl=1000;
            Add("raw-water","Untreated freshwater waterskin",SurvivalItemKind.Food,1.15,1.2);items["raw-water"].WaterMl=1000;
            Add("empty-waterskin","Empty hide waterskin",SurvivalItemKind.Tool,.15,.3);
            Add("hide-wrap","Hide body wrap",SurvivalItemKind.Clothing,1.1,2,1,2,2);items["hide-wrap"].Insulation=.8;
            Add("fur-wrap","Fur mantle",SurvivalItemKind.Clothing,2,4,1,3,2);items["fur-wrap"].Insulation=1.6;
            Add("footwraps","Hide footwraps",SurvivalItemKind.Clothing,.4,.6);items["footwraps"].Insulation=.2;
            Food("raw-meat","Raw meat",.25,.3,330,150,1,true);
            Food("cooked-meat","Roasted meat",.2,.25,330,100,2,false);
            Food("dried-meat","Heat-dried meat",.09,.15,300,10,16,false);
            Food("organs","Fresh organs",.2,.24,250,130,.6,true);
            Food("fat","Animal fat",.1,.12,850,0,4,true);
            Food("marrow","Bone marrow",.08,.1,600,5,1,true);
            Food("berries","Edible berries",.15,.25,70,120,3,false);
            Food("roots","Edible roots",.25,.35,190,160,8,true);
            Food("roasted-roots","Roasted roots",.2,.25,190,100,3,false);
            Food("nuts","Edible nuts",.1,.15,580,5,45,false);
            Add("dressing","Clean absorbent dressing",SurvivalItemKind.Medicine,.04,.2,4);
        }
        static void Store(string id,int w,int h,double kg,double litres){var d=items[id];d.StorageWidth=w;d.StorageHeight=h;d.StorageKg=kg;d.StorageLitres=litres;}
        static void Food(string id,string name,double kg,double litres,double kcal,double water,double shelf,bool raw)
        {items.Add(id,new SurvivalItem{Id=id,Name=name,Kind=SurvivalItemKind.Food,Kg=kg,Litres=litres,Kcal=kcal,WaterMl=water,ShelfDays=shelf,Raw=raw,Stack=8});}
        public static bool TryGet(string id,out SurvivalItem item){item=null;return id!=null&&items.TryGetValue(id,out item);}
        public static SurvivalItem Get(string id)=>items.TryGetValue(id,out var item)?item:throw new ArgumentException("Unknown survival item: "+id);
        public static SurvivalInventory Starter()
        {
            var bag=new SurvivalInventory();foreach(string id in new[]{"pack","pouch","belt","stone-spear","knife","hammer","fire-drill","waterskin","dressing"})
                if(!bag.TryAdd(id,1,out var why))throw new InvalidOperationException(why+" "+id);
            foreach(var pair in new[]{("tinder",4),("cord",3),("cooked-meat",3),("nuts",3)})
                if(!bag.TryAdd(pair.Item1,pair.Item2,out var why))throw new InvalidOperationException(why);
            bag.Equip(bag.Items.First(s=>s.Item=="stone-spear").Id,out _);return bag;
        }
    }
    public static class SurvivalMath
    {
        public static bool Finite(double n)=>!double.IsNaN(n)&&!double.IsInfinity(n);
        public static double Clamp(double n,double min=0,double max=1)=>Math.Max(min,Math.Min(max,n));
    }
}
