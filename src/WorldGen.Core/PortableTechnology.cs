using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    [Flags] public enum CraftCapability { None=0,Cut=1,Strike=2,Scrape=4,Pierce=8,Chop=16,Sew=32,Grind=64 }
    public enum PortableForm { Fibre,Cord,Reed,Hide,Strip,Bone,Antler,Feather,Blade,Point,Chopper,Needle,Hook,Knife,Axe,Spear,Arrow,Bow,Thrower,Sling,Net,Pouch,Satchel,Pack,Basket,BarkBox,Frame,Quiver,Belt,Case,Shaft }
    public sealed class PortableItem
    {
        public PossessionKind Kind;public string Name,Aliases;public PortableForm Form;public double Width,Height,Depth,Mass;
        public int StorageWidth,StorageHeight,Hands=1;public PossessionPlace? Wear;public CraftCapability Capability;
        public bool Long=>Form==PortableForm.Spear||Form==PortableForm.Arrow||Form==PortableForm.Bow||Form==PortableForm.Shaft;
    }
    [Serializable] public sealed class MaterialOrigin
    {
        public string Species="";public int Coat,Seed;public ulong AnimalId;
        public MaterialOrigin Copy()=>(MaterialOrigin)MemberwiseClone();
    }
    public static class PortableTechnology
    {
        static PortableItem D(PossessionKind kind,string name,PortableForm form,double w,double h,double d,double mass,CraftCapability capability=0,int sw=0,int sh=0,PossessionPlace? wear=null,string aliases="",int hands=1)
            =>new PortableItem{Kind=kind,Name=name,Form=form,Width=w,Height=h,Depth=d,Mass=mass,Capability=capability,StorageWidth=sw,StorageHeight=sh,Wear=wear,Aliases=aliases,Hands=hands};
        public static readonly PortableItem[] Items={
            D(PossessionKind.BarkSheet,"Bark sheet",PortableForm.Strip,.35,.45,.04,.35,aliases:"sheet bark"),
            D(PossessionKind.WeavingStrips,"Weaving strips",PortableForm.Reed,.1,.5,.04,.2,aliases:"split reeds"),
            D(PossessionKind.RawFibre,"Raw fibre bundle",PortableForm.Fibre,.12,.25,.08,.18,aliases:"fiber|fibres|fibers|raw fibre|raw fiber"),
            D(PossessionKind.PreparedFibre,"Prepared fibres",PortableForm.Fibre,.1,.2,.05,.14,aliases:"prepared fiber|prepared fibre"),
            D(PossessionKind.Cordage,"Cordage coil",PortableForm.Cord,.13,.07,.13,.12,aliases:"cord|rope|cordage"),
            D(PossessionKind.Thread,"Thread bundle",PortableForm.Cord,.07,.04,.07,.03,aliases:"thread"),
            D(PossessionKind.BarkStrips,"Bark strips",PortableForm.Strip,.14,.3,.03,.2,aliases:"bark"),
            D(PossessionKind.ReedBundle,"Reed bundle",PortableForm.Reed,.12,.7,.1,.3,aliases:"reeds|reed"),
            D(PossessionKind.RawHide,"Raw hide",PortableForm.Hide,.35,.4,.08,1.5,aliases:"hide|skin"),
            D(PossessionKind.ScrapedHide,"Scraped hide",PortableForm.Hide,.3,.35,.06,1.1),
            D(PossessionKind.WorkedHide,"Worked hide",PortableForm.Hide,.3,.3,.05,.85,aliases:"soft hide|softened hide"),
            D(PossessionKind.RawhideStrips,"Rawhide strips",PortableForm.Strip,.12,.2,.03,.12,aliases:"hide strips"),
            D(PossessionKind.Sinew,"Sinew bundle",PortableForm.Fibre,.06,.18,.03,.08,aliases:"sinew"),
            D(PossessionKind.SinewCord,"Sinew cord",PortableForm.Cord,.08,.04,.08,.06),
            D(PossessionKind.Feather,"Feather bunch",PortableForm.Feather,.1,.22,.03,.025,aliases:"feathers|feather"),
            D(PossessionKind.Bone,"Long bone",PortableForm.Bone,.06,.35,.05,.3,aliases:"bone"),
            D(PossessionKind.Antler,"Antler piece",PortableForm.Antler,.15,.4,.08,.4,aliases:"antler"),
            D(PossessionKind.StoneBlade,"Stone blade",PortableForm.Blade,.05,.16,.012,.12,CraftCapability.Cut|CraftCapability.Scrape),
            D(PossessionKind.StoneScraper,"Stone scraper",PortableForm.Blade,.08,.08,.02,.15,CraftCapability.Scrape|CraftCapability.Cut,aliases:"scraper"),
            D(PossessionKind.StoneChopper,"Stone chopper",PortableForm.Chopper,.12,.14,.06,.7,CraftCapability.Chop|CraftCapability.Cut,aliases:"chopper"),
            D(PossessionKind.Handaxe,"Handaxe",PortableForm.Chopper,.1,.18,.035,.6,CraftCapability.Chop|CraftCapability.Cut|CraftCapability.Scrape,aliases:"hand axe"),
            D(PossessionKind.StonePoint,"Stone spear point",PortableForm.Point,.06,.13,.015,.1,aliases:"stone point|spearhead"),
            D(PossessionKind.Arrowhead,"Stone arrowhead",PortableForm.Point,.025,.04,.007,.015,aliases:"arrowhead"),
            D(PossessionKind.StoneBorer,"Stone borer",PortableForm.Point,.025,.08,.015,.05,CraftCapability.Pierce,aliases:"borer|stone drill"),
            D(PossessionKind.BoneAwl,"Bone awl",PortableForm.Needle,.02,.15,.015,.035,CraftCapability.Pierce,aliases:"awl"),
            D(PossessionKind.BoneNeedle,"Bone needle",PortableForm.Needle,.008,.09,.004,.006,CraftCapability.Sew,aliases:"needle"),
            D(PossessionKind.BonePoint,"Bone point",PortableForm.Point,.025,.14,.018,.05),
            D(PossessionKind.BarbedPoint,"Barbed antler point",PortableForm.Point,.04,.2,.025,.09,aliases:"barbed point|harpoon head"),
            D(PossessionKind.FishingGorge,"Bone fishing gorge",PortableForm.Needle,.012,.055,.008,.008,aliases:"gorge"),
            D(PossessionKind.Fishhook,"Bone fishhook",PortableForm.Hook,.04,.065,.008,.012,aliases:"fishhook|hook"),
            D(PossessionKind.AntlerRetoucher,"Antler retoucher",PortableForm.Antler,.04,.18,.03,.08,CraftCapability.Strike,aliases:"retoucher"),
            D(PossessionKind.WoodHandle,"Wooden handle",PortableForm.Shaft,.03,.22,.025,.09,aliases:"handle"),
            D(PossessionKind.NotchedShaft,"Notched spear shaft",PortableForm.Shaft,.03,1.8,.03,.8,aliases:"notched shaft"),
            D(PossessionKind.LooseStoneSpear,"Unbound stone spear",PortableForm.Spear,.07,1.9,.03,.95,aliases:"unbound spear"),
            D(PossessionKind.StoneTippedSpear,"Stone-tipped spear",PortableForm.Spear,.07,1.9,.03,1,aliases:"stone spear"),
            D(PossessionKind.BoneTippedSpear,"Bone-tipped spear",PortableForm.Spear,.045,1.9,.03,.9,aliases:"bone spear"),
            D(PossessionKind.Harpoon,"Barbed spear",PortableForm.Spear,.06,1.8,.03,1,aliases:"harpoon"),
            D(PossessionKind.HaftedKnife,"Hafted stone knife",PortableForm.Knife,.055,.28,.025,.22,CraftCapability.Cut|CraftCapability.Scrape,aliases:"knife|stone knife"),
            D(PossessionKind.StoneAxe,"Hafted stone axe",PortableForm.Axe,.14,.45,.055,.9,CraftCapability.Chop|CraftCapability.Cut,aliases:"axe|stone axe"),
            D(PossessionKind.StoneAdze,"Hafted stone adze",PortableForm.Axe,.12,.4,.06,.8,CraftCapability.Chop|CraftCapability.Cut,aliases:"adze|stone adze"),
            D(PossessionKind.ArrowShaft,"Arrow shaft",PortableForm.Arrow,.008,.75,.008,.025),
            D(PossessionKind.UnfletchedArrow,"Unfletched arrow",PortableForm.Arrow,.025,.8,.012,.045),
            D(PossessionKind.Arrow,"Fletched arrow",PortableForm.Arrow,.045,.8,.025,.055,aliases:"arrow"),
            D(PossessionKind.BowStave,"Carved bow stave",PortableForm.Bow,.25,1.5,.035,.65,aliases:"bow stave"),
            D(PossessionKind.Bow,"Self bow",PortableForm.Bow,.35,1.5,.035,.75,aliases:"bow"),
            D(PossessionKind.SpearThrower,"Spear-thrower",PortableForm.Thrower,.055,.45,.035,.25,aliases:"atlatl|spear thrower"),
            D(PossessionKind.DartShaft,"Dart shaft",PortableForm.Shaft,.015,1.3,.015,.12),
            D(PossessionKind.SpearDart,"Spear-thrower dart",PortableForm.Arrow,.045,1.4,.02,.2,aliases:"dart"),
            D(PossessionKind.Sling,"Sling",PortableForm.Sling,.15,.2,.04,.12),
            D(PossessionKind.FishingLine,"Fishing line",PortableForm.Cord,.08,.04,.08,.05,aliases:"line"),
            D(PossessionKind.HandLine,"Hook and hand-line",PortableForm.Hook,.1,.08,.05,.07,aliases:"hand line|handline"),
            D(PossessionKind.FibreNet,"Knotted fibre net",PortableForm.Net,.25,.3,.06,.35,aliases:"net|fiber net"),
            D(PossessionKind.HidePouch,"Hide pouch",PortableForm.Pouch,.18,.2,.07,.15,sw:2,sh:2,wear:PossessionPlace.Waist,aliases:"pouch"),
            D(PossessionKind.BarkContainer,"Folded bark container",PortableForm.BarkBox,.25,.25,.18,.25,sw:3,sh:3,aliases:"bark container"),
            D(PossessionKind.WovenBasket,"Twined basket",PortableForm.Basket,.35,.4,.3,.7,sw:4,sh:5,aliases:"basket|woven basket"),
            D(PossessionKind.HideSatchel,"Hide satchel",PortableForm.Satchel,.3,.32,.12,.45,sw:4,sh:4,wear:PossessionPlace.Shoulder,aliases:"satchel"),
            D(PossessionKind.HidePack,"Hide pack",PortableForm.Pack,.4,.55,.22,1.1,sw:5,sh:7,wear:PossessionPlace.Back,aliases:"pack|backpack"),
            D(PossessionKind.CarryFrame,"Wooden carrying frame",PortableForm.Frame,.45,.8,.18,1.4,sw:6,sh:9,wear:PossessionPlace.Back,aliases:"carry frame|frame",hands:2),
            D(PossessionKind.Quiver,"Hide quiver",PortableForm.Quiver,.18,.85,.15,.55,sw:4,sh:9,wear:PossessionPlace.Shoulder,aliases:"quiver"),
            D(PossessionKind.FibreBelt,"Fibre belt",PortableForm.Belt,.15,.05,.15,.12,sw:4,sh:5,wear:PossessionPlace.Waist,aliases:"belt|fiber belt"),
            D(PossessionKind.HideBelt,"Hide belt",PortableForm.Belt,.15,.05,.15,.16,sw:4,sh:5,wear:PossessionPlace.Waist),
            D(PossessionKind.NeedleCase,"Bone needle case",PortableForm.Case,.04,.14,.04,.06,sw:1,sh:2,aliases:"needle case"),
            D(PossessionKind.KnifeSheath,"Hide knife sheath",PortableForm.Case,.07,.3,.03,.09,sw:1,sh:3,wear:PossessionPlace.Waist,aliases:"sheath|knife sheath")
        };
        static readonly Dictionary<PossessionKind,PortableItem> byKind=Items.ToDictionary(p=>p.Kind);
        public static PortableItem Item(PossessionKind kind)=>byKind.TryGetValue(kind,out var item)?item:null;
        public static string Key(string value)=>new string((value??"").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        public static PortableItem Named(string name){string key=Key(name);if(key.Length==0)return null;return Items.FirstOrDefault(d=>Key(d.Name)==key||Key(d.Kind.ToString())==key||d.Aliases.Split('|').Any(a=>Key(a)==key));}
        public static string SpeciesName(string species)=>string.IsNullOrEmpty(species)?"Animal":char.ToUpperInvariant(species[0])+species.Substring(1).Replace('-',' ');
        public static string Name(Possession p)
        {
            var d=Item(p.Kind);if(d==null)return null;if(string.IsNullOrEmpty(p.AppearanceSpecies))return d.Name;string species=SpeciesName(p.AppearanceSpecies);
            if(p.Kind==PossessionKind.RawHide)return species+" hide";
            if(p.Kind==PossessionKind.ScrapedHide)return "Scraped "+species.ToLowerInvariant()+" hide";
            if(p.Kind==PossessionKind.WorkedHide)return "Worked "+species.ToLowerInvariant()+" hide";
            if(p.Kind==PossessionKind.RawhideStrips)return species+" rawhide strips";
            return d.Name.StartsWith("Hide ")?species+"-hide "+d.Name.Substring(5):d.Name;
        }
        public static bool Worn(PossessionPlace place)=>place==PossessionPlace.Back||place==PossessionPlace.Shoulder||place==PossessionPlace.Waist;
        public static PossessionPlace? Wear(Possession p)=>p.Kind==PossessionKind.TestBag?PossessionPlace.Back:Item(p.Kind)?.Wear;
        public static bool Belt(Possession p)=>p!=null&&Item(p.Kind)?.Form==PortableForm.Belt;
        public static bool Holds(Possession bag,Possession item)
        {
            if(bag.Kind==PossessionKind.Quiver)return item.Kind==PossessionKind.Arrow||item.Kind==PossessionKind.UnfletchedArrow||item.Kind==PossessionKind.ArrowShaft;
            if(bag.Kind==PossessionKind.NeedleCase)return item.Kind==PossessionKind.BoneNeedle||item.Kind==PossessionKind.BoneAwl||item.Kind==PossessionKind.Fishhook||item.Kind==PossessionKind.FishingGorge;
            if(bag.Kind==PossessionKind.KnifeSheath)return item.Kind==PossessionKind.HaftedKnife||item.Kind==PossessionKind.StoneBlade||item.Kind==PossessionKind.BoneAwl;
            if(Belt(bag))return item.Kind==PossessionKind.HaftedKnife||item.Kind==PossessionKind.StoneAxe||item.Kind==PossessionKind.StoneAdze||item.Kind==PossessionKind.SpearThrower||item.Kind==PossessionKind.NeedleCase||item.Kind==PossessionKind.KnifeSheath;
            return true;
        }
        public static CraftCapability Capabilities(Possession p)
        {
            if(p==null)return 0;
            if(StoneCrafting.Rock(p))return (StoneCrafting.StrikingStone(p)?CraftCapability.Strike:0)|(p.MassKg>=.1?CraftCapability.Grind:0);
            var cap=p.Kind==PossessionKind.StoneFlake?CraftCapability.Cut|CraftCapability.Scrape:Item(p.Kind)?.Capability??0;
            if(p.EdgeSharpness<.15)cap&=~(CraftCapability.Cut|CraftCapability.Scrape);return cap;
        }
        public static Possession Create(PossessionKind kind,V position,string species="",int coat=0,int seed=0,ulong animalId=0)
        {
            var d=Item(kind)??throw new ArgumentException("Unknown portable item.");
            var p=new Possession{Id="item-"+Guid.NewGuid().ToString("N"),Kind=kind,Place=PossessionPlace.Ground,Position=position,WidthM=d.Width,HeightM=d.Height,DepthM=d.Depth,MassKg=d.Mass,
                CellsWide=Math.Max(1,(int)Math.Ceiling(d.Width/.1)),CellsHigh=Math.Max(1,(int)Math.Ceiling(d.Height/.1)),Tint=new V(1,1,1),EdgeSharpness=(d.Capability&(CraftCapability.Cut|CraftCapability.Scrape))!=0?.85:0,Variant=ToolStone.Encode(ToolStoneMaterial.Chert,0),WoodSoundness=.9,WoodStraightness=.9,
                AppearanceSpecies=species??"",AppearanceCoat=coat,AppearanceSeed=seed,SourceAnimalId=animalId};
            if(!string.IsNullOrEmpty(species))p.MaterialOrigins=new List<MaterialOrigin>{new MaterialOrigin{Species=species,Coat=coat,Seed=seed,AnimalId=animalId}};
            return p;
        }
    }
}
