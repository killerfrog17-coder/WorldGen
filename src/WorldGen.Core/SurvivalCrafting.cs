using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    public sealed class SurvivalRecipe
    {
        public string Id,Name,Output,Tool,Description;public int Count=1;public double Seconds=30;public bool Fire;
        public (string item,int count)[] Materials=Array.Empty<(string,int)>();
    }
    public sealed class ShelterBlueprint
    {
        public string Id,Name,Description,Evidence;public double Shelter,Insulation,Radius,Seconds=180;
        public (string item,int count)[] Materials;
    }
    public static class SurvivalCrafting
    {
        public static readonly SurvivalRecipe[] Recipes={
            Recipe("flake","Knapp sharp flakes","flake",2,20,null,false,("flint",1)),
            Recipe("hammer","Select hammerstone","hammer",1,5,null,false,("stone",1)),
            Recipe("handaxe","Knapp bifacial handaxe","handaxe",1,120,"hammer",false,("flint",2)),
            Recipe("scraper","Knapp hide scraper","scraper",1,45,"hammer",false,("flint",1)),
            Recipe("cord","Twist plant-fibre cordage","cord",2,60,null,false,("fibre",3)),
            Recipe("tinder","Prepare dry tinder","tinder",3,20,null,false,("grass",1),("bark",1)),
            Recipe("wood-spear","Carve wooden spear","wood-spear",1,90,"sharp",false,("stick",2)),
            Recipe("stone-spear","Haft stone-tipped spear","stone-spear",1,120,"sharp",false,("wood-spear",1),("flint",1),("cord",1)),
            Recipe("knife","Haft stone knife","knife",1,60,"sharp",false,("flake",1),("stick",1),("cord",1)),
            Recipe("club","Shape wooden club","club",1,45,"sharp",false,("stick",2)),
            Recipe("throwing-stick","Shape throwing stick","throwing-stick",1,45,"sharp",false,("stick",1)),
            Recipe("digging-stick","Shape digging stick","digging-stick",1,40,"sharp",false,("stick",1)),
            Recipe("fire-drill","Make hand-drill fire kit","fire-drill",1,90,"sharp",false,("stick",2),("bark",1)),
            Recipe("fire-striker","Assemble flint-and-pyrite kit","fire-striker",1,10,null,false,("flint",1),("pyrite",1)),
            Recipe("dryhide","Scrape and dry hide","dryhide",1,240,"scraper",false,("hide",1)),
            Recipe("small-dryhide","Scrape and dry small pelt","small-dryhide",1,100,"scraper",false,("small-hide",1)),
            Recipe("pelt-pouch","Sew small-pelt pouch","pouch",1,120,"awl",false,("small-dryhide",3),("cord",2)),
            Recipe("awl","Shape bone awl","awl",1,90,"sharp",false,("bone",1)),
            Recipe("needle","Shape eyed bone needle","needle",1,180,"awl",false,("bone",1)),
            Recipe("pouch","Sew hide belt pouch","pouch",1,120,"awl",false,("dryhide",1),("cord",2)),
            Recipe("pack","Sew hide sling pack","pack",1,180,"awl",false,("dryhide",2),("cord",3)),
            Recipe("basket","Weave carrying basket","basket",1,180,null,false,("fibre",8),("stick",3)),
            Recipe("belt","Cut hide tool belt","belt",1,90,"sharp",false,("dryhide",1),("cord",1)),
            Recipe("empty-waterskin","Sew hide waterskin","empty-waterskin",1,180,"awl",false,("dryhide",1),("sinew",2)),
            Recipe("hide-wrap","Make hide body wrap","hide-wrap",1,120,"sharp",false,("dryhide",1),("cord",1)),
            Recipe("fur-wrap","Make fur mantle","fur-wrap",1,150,"awl",false,("dryhide",2),("sinew",2)),
            Recipe("footwraps","Make hide footwraps","footwraps",1,90,"sharp",false,("dryhide",1),("cord",1)),
            Recipe("sling","Weave hunting sling","sling",1,120,null,false,("cord",3),("fibre",2)),
            Recipe("dressing","Prepare absorbent dressing","dressing",2,30,null,false,("fibre",2)),
            Recipe("cooked-meat","Roast meat","cooked-meat",1,45,null,true,("raw-meat",1)),
            Recipe("boil-water","Boil water using heated stones","waterskin",1,100,"stone",true,("raw-water",1)),
            Recipe("marrow","Crack marrow bone","marrow",1,25,"hammer",false,("bone",1)),
            Recipe("roasted-roots","Roast edible roots","roasted-roots",1,45,null,true,("roots",1)),
            Recipe("dried-meat","Heat and dry meat","dried-meat",1,150,null,true,("raw-meat",1))
        };
        static SurvivalRecipe Recipe(string id,string name,string output,int count,double seconds,string tool,bool fire,params (string,int)[] materials)
            =>new SurvivalRecipe{Id=id,Name=name,Output=output,Count=count,Seconds=seconds,Tool=tool,Fire=fire,Materials=materials};
        public static readonly ShelterBlueprint[] Shelters={
            Blueprint("hearth","Open hearth",0,0,2,30,"Controlled fire is well evidenced; ignition technique and dates vary.",("stone",3),("stick",3),("tinder",1)),
            Blueprint("windbreak","Brush windbreak",.35,.2,2.5,90,"Plausible perishable reconstruction, not a dated universal design.",("stick",5),("grass",4),("cord",2)),
            Blueprint("lean-to","Branch-and-brush lean-to",.65,.45,2.5,180,"Plausible pole-and-vegetation shelter reconstruction.",("stick",8),("grass",8),("cord",4)),
            Blueprint("bark-lean-to","Bark-covered lean-to",.8,.5,2.5,210,"Organic shelter reconstruction; bark availability is local.",("stick",8),("bark",8),("cord",4)),
            Blueprint("hide-tent","Hide-covered pole shelter",.9,1,3,300,"Late Paleolithic framed hide shelters; not a claim for the later Plains tipi design.",("stick",10),("dryhide",4),("cord",6)),
            Blueprint("brush-hut","Domed brush hut",.8,.8,3,300,"Plausible flexible-pole shelter reconstruction.",("stick",12),("grass",12),("cord",5)),
            Blueprint("snow-shelter","Dug snow shelter",.9,1.3,2.5,240,"Climate-specific reconstruction; not an assertion of Paleolithic block igloos.",("digging-stick",1)),
            Blueprint("bone-shelter","Bone-supported hide shelter",.9,1,3.5,420,"Inspired by late Upper Paleolithic mammoth-bone structures; generic bones are a prototype abstraction.",("bone",18),("dryhide",5),("cord",8)),
            Blueprint("bed","Grass bedding",.05,.6,1.5,45,"Grass bedding is documented at Border Cave around 200,000 years ago.",("grass",5)),
            Blueprint("hide-bed","Hide-covered bedding",.1,1.1,1.5,60,"Plausible layered bedding reconstruction.",("grass",4),("dryhide",1)),
            Blueprint("cache","Raised storage rack",0,0,1.5,100,"Perishable storage reconstruction; not a guarantee of pest-proof preservation.",("stick",6),("cord",3)),
            Blueprint("drying-rack","Meat-drying rack",0,0,1.5,100,"Drying/smoking technology reconstruction; heat treatment still required.",("stick",5),("cord",3))
        };
        static ShelterBlueprint Blueprint(string id,string name,double shelter,double insulation,double radius,double seconds,string evidence,params (string,int)[] materials)
            =>new ShelterBlueprint{Id=id,Name=name,Shelter=shelter,Insulation=insulation,Radius=radius,Seconds=seconds,Evidence=evidence,Materials=materials};
        public static string MaterialsText((string item,int count)[] materials)=>string.Join(", ",materials.Select(m=>m.count+" "+SurvivalItems.Get(m.item).Name.ToLowerInvariant()));
        public static bool CanCraft(SurvivalState s,SurvivalRecipe r,bool fire,out string message)
        {
            message="";if(s.Mode==SurvivalMode.Creative)return true;
            if(r.Fire&&!fire){message="A burning hearth is needed.";return false;}
            if(r.Tool=="sharp"&&!s.Inventory.Items.Any(i=>SurvivalItems.Get(i.Item).Sharp)||r.Tool!=null&&r.Tool!="sharp"&&!s.Inventory.Has(r.Tool)){message="Requires "+(r.Tool=="sharp"?"a sharp stone tool":SurvivalItems.Get(r.Tool).Name.ToLowerInvariant())+".";return false;}
            foreach(var m in r.Materials)if(!s.Inventory.Has(m.item,m.count)){message="Requires "+MaterialsText(r.Materials)+".";return false;}
            foreach(var m in r.Materials)if(s.Inventory.Items.Any(i=>i.Item==m.item&&SurvivalItems.Get(i.Item).ShelfDays>0&&i.AgeDays>SurvivalItems.Get(i.Item).ShelfDays)){message="Spoiled food cannot be made safe by cooking.";return false;}
            return true;
        }
        public static bool Craft(SurvivalState s,SurvivalRecipe r,bool fire,out string message)
        {
            if(!CanCraft(s,r,fire,out message))return false;var inventory=s.Inventory.Copy();double fraction=1;
            if(s.Mode!=SurvivalMode.Creative&&SurvivalItems.Get(r.Output).Kind==SurvivalItemKind.Food)foreach(var m in r.Materials)
                if(SurvivalItems.Get(m.item).Kind==SurvivalItemKind.Food){fraction=inventory.Items.Where(i=>i.Item==m.item).OrderByDescending(i=>i.AgeDays).First().Fraction;break;}
            if(s.Mode!=SurvivalMode.Creative)foreach(var m in r.Materials)inventory.Remove(m.item,m.count);
            if(!inventory.TryAdd(r.Output,r.Count,out message,0,fraction))return false;
            s.Inventory=inventory;message="Made "+r.Name.ToLowerInvariant()+".";return true;
        }
        public static bool Build(SurvivalState s,ShelterBlueprint b,V point,double heading,out string message)
        {
            if(b.Id=="hearth"&&s.Mode!=SurvivalMode.Creative&&!s.Inventory.Has("fire-drill")&&!s.Inventory.Has("fire-striker")){message="A firemaking kit is needed.";return false;}
            if(s.Mode!=SurvivalMode.Creative)foreach(var m in b.Materials)if(!s.Inventory.Has(m.item,m.count)){message="Requires "+MaterialsText(b.Materials)+".";return false;}
            var inventory=s.Inventory.Copy();if(s.Mode!=SurvivalMode.Creative)foreach(var m in b.Materials)if(m.item!="digging-stick")inventory.Remove(m.item,m.count);
            s.Inventory=inventory;s.Sites=s.Sites.Concat(new[]{new SurvivalSite{Id=s.NextSite++,Kind=b.Id,Position=point,Heading=heading,FuelHours=b.Id=="hearth"?2:0,Lit=b.Id=="hearth"}}).ToArray();message="Built "+b.Name.ToLowerInvariant()+".";return true;
        }
        public static SurvivalSite PlaceBlueprint(SurvivalState s,ShelterBlueprint b,V point,double heading)
        {
            var site=new SurvivalSite{Id=s.NextSite++,Kind=b.Id,Position=point,Heading=heading,Progress=0};
            s.Sites=s.Sites.Concat(new[]{site}).ToArray();return site;
        }
        public static bool SupplyBlueprint(SurvivalState s,SurvivalSite site,out string message)
        {
            var b=Shelters.FirstOrDefault(x=>x.Id==site.Kind);if(b==null||site.Progress>=1){message="This structure is already complete.";return false;}
            var inventory=s.Inventory.Copy();var supplied=site.Contents.Select(i=>i.Copy()).ToList();int donated=0,total=0,ready=0;
            foreach(var material in b.Materials)
            {
                total+=material.count;int existing=supplied.Where(i=>i.Item==material.item).Sum(i=>i.Count);
                int amount=Math.Min(material.count-existing,inventory.Count(material.item));
                if(material.item=="digging-stick")amount=inventory.Has(material.item)?material.count-existing:0;
                if(s.Mode==SurvivalMode.Creative)amount=material.count-existing;
                if(amount>0)
                {
                    if(s.Mode==SurvivalMode.Creative||material.item=="digging-stick"||inventory.Remove(material.item,amount))
                    {supplied.Add(new SurvivalStack{Id=supplied.Count+1,Item=material.item,Count=amount});existing+=amount;donated+=amount;}
                }
                ready+=Math.Min(material.count,existing);
            }
            if(donated==0){message="Bring "+MaterialsText(b.Materials)+"; materials can be delivered in several trips.";return false;}
            s.Inventory=inventory;site.Contents=supplied.ToArray();site.Progress=ready/(double)Math.Max(1,total);
            if(site.Progress>=1)
            {
                site.Contents=Array.Empty<SurvivalStack>();site.FuelHours=site.Kind=="hearth"?2:0;
                site.Lit=site.Kind=="hearth"&&(inventory.Has("fire-drill")||inventory.Has("fire-striker")||s.Mode==SurvivalMode.Creative);
                message="Completed "+b.Name.ToLowerInvariant()+".";
            }
            else message="Supplied materials · "+(site.Progress*100).ToString("F0")+"% complete.";
            return true;
        }
    }
}
