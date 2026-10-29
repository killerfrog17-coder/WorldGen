using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    public sealed class CraftMaterialNeed
    {
        public int Count;public PossessionKind[] Kinds;
        public CraftMaterialNeed(int count,params PossessionKind[] kinds){Count=count;Kinds=kinds;}
        public string Description=>Count+" x "+string.Join(" / ",Kinds.Select(k=>PortableTechnology.Item(k)?.Name??k.ToString()));
    }
    public sealed class PortableRecipe
    {
        public string Id,Name;public PossessionKind Output;public PossessionKind[] Primary;public CraftMaterialNeed[] Materials;public CraftCapability[] Tools;
        public double WorkSeconds,Yield=.85,MinLength,MaxThickness=10;public bool SoundWood,Maintenance,RestoreEdge;
        public bool Accepts(Possession p)=>p!=null&&Primary.Contains(p.Kind);
        public string Category=>PortableTechnology.Item(Output)?.Form.ToString()??"Materials";
        public string PrimaryName=>string.Join(" / ",Primary.Select(k=>PortableTechnology.Item(k)?.Name??k.ToString()));
        public string Requirements=>"Start with "+PrimaryName+"; "+(Materials.Length==0?"no extra materials":string.Join(", ",Materials.Select(m=>m.Description)))+". Tools: "+(Tools.Length==0?"hands only":string.Join(", ",Tools.Select(t=>t.ToString())))+".";
    }
    public sealed class PortableCraftPlan
    {
        public PortableRecipe Recipe;public string PrimaryId;public List<string> Ingredients=new List<string>(),Tools=new List<string>();
    }
    public static class PortableCrafting
    {
        static CraftMaterialNeed N(int count,params PossessionKind[] kinds)=>new CraftMaterialNeed(count,kinds);
        static CraftMaterialNeed Binding(int n)=>N(n,PossessionKind.Cordage,PossessionKind.RawhideStrips,PossessionKind.SinewCord);
        static CraftMaterialNeed Thread(int n)=>N(n,PossessionKind.Thread,PossessionKind.SinewCord);
        static PortableRecipe R(string id,string name,PossessionKind output,PossessionKind[] primary,double work,CraftMaterialNeed[] materials=null,CraftCapability[] tools=null,double yield=.85,double length=0,double thickness=10,bool wood=false)
            =>new PortableRecipe{Id=id,Name=name,Output=output,Primary=primary,WorkSeconds=work,Materials=materials??Array.Empty<CraftMaterialNeed>(),Tools=tools??Array.Empty<CraftCapability>(),Yield=yield,MinLength=length,MaxThickness=thickness,SoundWood=wood};
        static PossessionKind[] P(params PossessionKind[] kinds)=>kinds;
        static CraftCapability[] T(params CraftCapability[] caps)=>caps;
        public static readonly PortableRecipe[] Recipes=new[]{
            R("shred-tinder","Shred a fine tinder bundle",PossessionKind.TinderBundle,P(PossessionKind.DryGrass,PossessionKind.BarkStrips,PossessionKind.PreparedFibre),90,yield:.9),
            R("split-kindling","Split thin kindling",PossessionKind.KindlingBundle,P(PossessionKind.Branch,PossessionKind.TrimmedPole),180,tools:T(CraftCapability.Chop|CraftCapability.Cut),yield:.9,thickness:.06),
            R("hand-drill","Shape spindle and notched hearthboard",PossessionKind.HandDrillSet,P(PossessionKind.Branch,PossessionKind.TrimmedPole),900,materials:new[]{N(1,PossessionKind.Branch,PossessionKind.TrimmedPole)},tools:T(CraftCapability.Cut),yield:.7,length:.35,thickness:.06,wood:true),
            R("prepare-fibres","Separate and prepare fibres",PossessionKind.PreparedFibre,P(PossessionKind.RawFibre),120,yield:.8),
            R("bark-fibres","Scrape inner-bark fibres",PossessionKind.PreparedFibre,P(PossessionKind.BarkStrips),180,tools:T(CraftCapability.Scrape),yield:.65),
            R("twist-cord","Twist a cordage coil",PossessionKind.Cordage,P(PossessionKind.PreparedFibre),360,yield:.95),
            R("twist-thread","Twist fine thread",PossessionKind.Thread,P(PossessionKind.PreparedFibre),420,yield:.45),
            R("prepare-sinew","Separate and twist sinew",PossessionKind.SinewCord,P(PossessionKind.Sinew),240,yield:.85),
            R("split-reeds","Split weaving strips",PossessionKind.WeavingStrips,P(PossessionKind.ReedBundle),150,tools:T(CraftCapability.Cut),yield:.8),
            R("cut-bark","Cut bark into tying strips",PossessionKind.BarkStrips,P(PossessionKind.BarkSheet),120,tools:T(CraftCapability.Cut),yield:.8),
            R("scrape-hide","Flesh and scrape hide",PossessionKind.ScrapedHide,P(PossessionKind.RawHide),600,tools:T(CraftCapability.Scrape),yield:.75),
            R("work-hide","Work and soften hide",PossessionKind.WorkedHide,P(PossessionKind.ScrapedHide),900,yield:.9),
            R("cut-hide-strips","Cut rawhide binding strips",PossessionKind.RawhideStrips,P(PossessionKind.ScrapedHide,PossessionKind.WorkedHide),150,tools:T(CraftCapability.Cut),yield:.35),
            R("stone-blade","Shape a cutting blade",PossessionKind.StoneBlade,P(PossessionKind.StoneFlake),180,tools:T(CraftCapability.Strike),yield:.8),
            R("stone-scraper","Shape a scraping edge",PossessionKind.StoneScraper,P(PossessionKind.StoneFlake),150,tools:T(CraftCapability.Strike),yield:.8),
            R("stone-chopper","Chip a stone chopper",PossessionKind.StoneChopper,P(PossessionKind.SmallRock,PossessionKind.LargeRock),360,tools:T(CraftCapability.Strike),yield:.75),
            R("handaxe","Shape a bifacial handaxe",PossessionKind.Handaxe,P(PossessionKind.SmallRock,PossessionKind.LargeRock),900,tools:T(CraftCapability.Strike),yield:.6),
            R("stone-point","Shape a spear point",PossessionKind.StonePoint,P(PossessionKind.StoneFlake,PossessionKind.StoneBlade),300,tools:T(CraftCapability.Strike),yield:.7),
            R("arrowhead","Shape a small arrowhead",PossessionKind.Arrowhead,P(PossessionKind.StoneFlake),240,tools:T(CraftCapability.Strike),yield:.3),
            R("stone-borer","Shape a stone borer",PossessionKind.StoneBorer,P(PossessionKind.StoneFlake),180,tools:T(CraftCapability.Strike),yield:.5),
            R("bone-awl","Carve a bone awl",PossessionKind.BoneAwl,P(PossessionKind.Bone),420,tools:T(CraftCapability.Cut|CraftCapability.Scrape),yield:.2),
            R("bone-needle","Carve and pierce a needle",PossessionKind.BoneNeedle,P(PossessionKind.Bone),720,tools:T(CraftCapability.Scrape,CraftCapability.Pierce),yield:.04),
            R("bone-point","Carve a bone point",PossessionKind.BonePoint,P(PossessionKind.Bone),480,tools:T(CraftCapability.Cut|CraftCapability.Scrape),yield:.4),
            R("barbed-point","Carve a barbed antler point",PossessionKind.BarbedPoint,P(PossessionKind.Antler),720,tools:T(CraftCapability.Cut|CraftCapability.Scrape),yield:.4),
            R("bone-gorge","Carve a fishing gorge",PossessionKind.FishingGorge,P(PossessionKind.Bone),240,tools:T(CraftCapability.Cut),yield:.05),
            R("fishhook","Carve and pierce a fishhook",PossessionKind.Fishhook,P(PossessionKind.Bone),600,tools:T(CraftCapability.Scrape,CraftCapability.Pierce),yield:.08),
            R("antler-retoucher","Shape an antler retoucher",PossessionKind.AntlerRetoucher,P(PossessionKind.Antler),300,tools:T(CraftCapability.Cut|CraftCapability.Scrape),yield:.3),
            R("wood-handle","Carve a short tool handle",PossessionKind.WoodHandle,P(PossessionKind.Branch,PossessionKind.TrimmedPole),240,tools:T(CraftCapability.Cut),yield:.25,length:.2,wood:true),
            R("notch-shaft","Cut a spear-shaft seat",PossessionKind.NotchedShaft,P(PossessionKind.TrimmedPole),180,tools:T(CraftCapability.Cut),yield:.95,length:1.2,wood:true),
            R("seat-point","Seat stone point on shaft",PossessionKind.LooseStoneSpear,P(PossessionKind.NotchedShaft),90,new[]{N(1,PossessionKind.StonePoint)},yield:.98),
            R("bind-spear","Bind a stone-tipped spear",PossessionKind.StoneTippedSpear,P(PossessionKind.LooseStoneSpear),300,new[]{Binding(2)},yield:.98),
            R("bone-spear","Bind a bone-tipped spear",PossessionKind.BoneTippedSpear,P(PossessionKind.NotchedShaft),300,new[]{N(1,PossessionKind.BonePoint),Binding(2)},yield:.98),
            R("barbed-spear","Bind a barbed spear",PossessionKind.Harpoon,P(PossessionKind.NotchedShaft),300,new[]{N(1,PossessionKind.BarbedPoint),Binding(2)},yield:.98),
            R("haft-knife","Haft a stone knife",PossessionKind.HaftedKnife,P(PossessionKind.StoneBlade),360,new[]{N(1,PossessionKind.WoodHandle),Binding(1)},yield:.9),
            R("haft-axe","Grind and haft a stone axe",PossessionKind.StoneAxe,P(PossessionKind.Handaxe),900,new[]{N(1,PossessionKind.WoodHandle),Binding(2)},T(CraftCapability.Grind),yield:.9),
            R("haft-adze","Grind and lash a stone adze",PossessionKind.StoneAdze,P(PossessionKind.StoneChopper),900,new[]{N(1,PossessionKind.WoodHandle),Binding(2)},T(CraftCapability.Grind),yield:.9),
            R("arrow-shaft","Split and shape an arrow shaft",PossessionKind.ArrowShaft,P(PossessionKind.Branch,PossessionKind.TrimmedPole),360,tools:T(CraftCapability.Cut),yield:.2,length:.7,thickness:.07,wood:true),
            R("tip-arrow","Bind an arrowhead",PossessionKind.UnfletchedArrow,P(PossessionKind.ArrowShaft),180,new[]{N(1,PossessionKind.Arrowhead),Binding(1)},yield:.65),
            R("fletch-arrow","Fletch an arrow",PossessionKind.Arrow,P(PossessionKind.UnfletchedArrow),240,new[]{N(1,PossessionKind.Feather),Thread(1)},yield:.75),
            R("bow-stave","Carve a self-bow stave",PossessionKind.BowStave,P(PossessionKind.TrimmedPole),1200,tools:T(CraftCapability.Cut),yield:.8,length:1.3,thickness:.08,wood:true),
            R("string-bow","String a self bow",PossessionKind.Bow,P(PossessionKind.BowStave),180,new[]{Binding(2)},yield:.95),
            R("wood-thrower","Carve a wooden spear-thrower",PossessionKind.SpearThrower,P(PossessionKind.TrimmedPole,PossessionKind.Branch),600,tools:T(CraftCapability.Cut),yield:.35,length:.4,wood:true),
            R("antler-thrower","Carve an antler spear-thrower",PossessionKind.SpearThrower,P(PossessionKind.Antler),720,tools:T(CraftCapability.Cut|CraftCapability.Scrape),yield:.7),
            R("dart-shaft","Shape a dart shaft",PossessionKind.DartShaft,P(PossessionKind.TrimmedPole),360,tools:T(CraftCapability.Cut),yield:.5,length:1.2,wood:true),
            R("tip-dart","Bind a spear-thrower dart",PossessionKind.SpearDart,P(PossessionKind.DartShaft),240,new[]{N(1,PossessionKind.StonePoint,PossessionKind.BonePoint),Binding(1)},yield:.95),
            R("sling","Knot a sling with hide pocket",PossessionKind.Sling,P(PossessionKind.Cordage),300,new[]{N(1,PossessionKind.Cordage),N(1,PossessionKind.RawhideStrips)},yield:.85),
            R("fishing-line","Twist a fishing line",PossessionKind.FishingLine,P(PossessionKind.Thread,PossessionKind.SinewCord),180,new[]{Thread(1)},yield:.95),
            R("hand-line","Tie a hook to a hand-line",PossessionKind.HandLine,P(PossessionKind.FishingLine),120,new[]{N(1,PossessionKind.Fishhook,PossessionKind.FishingGorge)},yield:.98),
            R("net","Knot a small fibre net",PossessionKind.FibreNet,P(PossessionKind.Cordage),900,new[]{N(3,PossessionKind.Cordage)},yield:.95),
            R("hide-pouch","Cut and lace a hide pouch",PossessionKind.HidePouch,P(PossessionKind.WorkedHide),480,new[]{Binding(1)},T(CraftCapability.Cut,CraftCapability.Pierce),yield:.35),
            R("bark-container","Fold and lace a bark container",PossessionKind.BarkContainer,P(PossessionKind.BarkSheet),420,new[]{Binding(1)},T(CraftCapability.Pierce),yield:.85),
            R("basket","Twine a carrying basket",PossessionKind.WovenBasket,P(PossessionKind.WeavingStrips),1200,new[]{N(2,PossessionKind.WeavingStrips),Binding(1)},yield:.9),
            R("satchel","Sew a hide satchel",PossessionKind.HideSatchel,P(PossessionKind.WorkedHide),900,new[]{N(1,PossessionKind.Cordage),Thread(1)},T(CraftCapability.Cut,CraftCapability.Sew),yield:.75),
            R("carry-frame","Lash a wooden carrying frame",PossessionKind.CarryFrame,P(PossessionKind.TrimmedPole),600,new[]{N(1,PossessionKind.TrimmedPole),Binding(3)},T(CraftCapability.Cut),yield:.9,length:.65,wood:true),
            R("hide-pack","Fit hides to a carrying frame",PossessionKind.HidePack,P(PossessionKind.CarryFrame),900,new[]{N(2,PossessionKind.WorkedHide),Binding(3)},T(CraftCapability.Pierce),yield:.9),
            R("quiver","Sew a hide quiver",PossessionKind.Quiver,P(PossessionKind.WorkedHide),720,new[]{Binding(2),Thread(1)},T(CraftCapability.Sew),yield:.65),
            R("fibre-belt","Braid a fibre belt with tool loops",PossessionKind.FibreBelt,P(PossessionKind.Cordage),360,new[]{N(1,PossessionKind.Cordage)},yield:.9),
            R("hide-belt","Cut and lace a hide belt",PossessionKind.HideBelt,P(PossessionKind.WorkedHide),360,new[]{Binding(1)},T(CraftCapability.Cut,CraftCapability.Pierce),yield:.3),
            R("needle-case","Carve and close a bone needle case",PossessionKind.NeedleCase,P(PossessionKind.Bone),420,new[]{Binding(1)},T(CraftCapability.Cut,CraftCapability.Pierce),yield:.3),
            R("knife-sheath","Cut and lace a knife sheath",PossessionKind.KnifeSheath,P(PossessionKind.WorkedHide),480,new[]{Binding(1)},T(CraftCapability.Cut,CraftCapability.Pierce),yield:.25)
        }.Concat(ItemMaintenance.Recipes()).ToArray();
        public static PortableRecipe Recipe(string id)=>Recipes.FirstOrDefault(r=>r.Id==id);
        public static bool IsJob(ItemCraftJob job)=>job!=null&&!string.IsNullOrEmpty(job.RecipeId);
        public static bool Affects(ItemCraftJob job,string id)=>job.WorkpieceId==id||job.ToolId==id||(job.IngredientIds?.Contains(id)??false)||(job.ToolIds?.Contains(id)??false);
        static bool Reserved(PlayerInventory s,string id,ItemCraftJob except)=>s.Crafts.Any(j=>j!=except&&(j.WorkpieceId==id||(j.IngredientIds?.Contains(id)??false)));
        static string PrimaryReason(PortableRecipe r,Possession p)
        {
            if(!r.Accepts(p))return "Start with "+r.PrimaryName+".";
            if(StoneCrafting.Rock(p)&&StoneCrafting.FractureQuality(p)<=0)return "This stone is unsuitable for flaking; use basalt, chert, quartzite, quartz or silicified sandstone.";
            if(r.MinLength>0&&p.HeightM<r.MinLength)return "Needs at least "+r.MinLength.ToString("G3")+" m of wood.";
            if(p.WidthM>r.MaxThickness)return "This piece is too thick for this method.";
            if(r.SoundWood&&(p.WoodSoundness<.5||p.WoodStraightness<.45))return "Needs reasonably sound, straight wood.";
            return "";
        }
        public static bool Plan(PlayerInventory s,IEnumerable<Possession> available,PortableRecipe r,string primaryId,out PortableCraftPlan plan,out string reason)
        {
            plan=null;if(r==null){reason="Unknown recipe.";return false;}
            var all=available.Where(p=>p!=null).GroupBy(p=>p.Id).Select(g=>g.First()).ToArray();var primary=all.FirstOrDefault(p=>p.Id==primaryId);
            reason=PrimaryReason(r,primary);if(reason.Length>0)return false;
            var existing=s.Crafts.FirstOrDefault(j=>j.WorkpieceId==primaryId);
            if(existing!=null){if(existing.RecipeId!=r.Id){reason="Finish or cancel the existing work on this item first.";return false;}return ResumePlan(s,all,existing,out plan,out reason);}
            if(Reserved(s,primaryId,null)||s.Crafts.Any(j=>j.ToolId==primaryId||(j.ToolIds?.Contains(primaryId)??false))){reason="This item belongs to another unfinished project.";return false;}
            var selected=new PortableCraftPlan{Recipe=r,PrimaryId=primaryId};selected.Ingredients.Add(primaryId);
            foreach(var need in r.Materials)
            {
                var choices=all.Where(p=>need.Kinds.Contains(p.Kind)&&!selected.Ingredients.Contains(p.Id)&&!Reserved(s,p.Id,null)&&!s.Crafts.Any(j=>j.ToolId==p.Id||(j.ToolIds?.Contains(p.Id)??false))).Take(need.Count).ToArray();
                if(choices.Length<need.Count){reason="Needs "+need.Description+" ("+choices.Length+" available).";return false;}selected.Ingredients.AddRange(choices.Select(p=>p.Id));
            }
            foreach(var cap in r.Tools)
            {
                var tool=all.FirstOrDefault(p=>!selected.Ingredients.Contains(p.Id)&&!Reserved(s,p.Id,null)&&(PortableTechnology.Capabilities(p)&cap)!=0);
                if(tool==null){reason="Requires a reusable tool for "+cap+".";return false;}if(!selected.Tools.Contains(tool.Id))selected.Tools.Add(tool.Id);
            }
            if(selected.Ingredients.Any(id=>s.Items.Any(p=>p.ContainerId==id))){reason="Empty the container before using it as a crafting material.";return false;}
            plan=selected;reason="";return true;
        }
        public static bool ResumePlan(PlayerInventory s,IEnumerable<Possession> available,ItemCraftJob job,out PortableCraftPlan plan,out string reason)
        {
            plan=null;var r=Recipe(job.RecipeId);var all=available.ToDictionary(p=>p.Id,p=>p);
            if(r==null||job.IngredientIds==null||job.ToolIds==null){reason="Invalid project.";return false;}
            if(job.IngredientIds.Concat(job.ToolIds).Any(id=>!all.ContainsKey(id))){reason="Bring all reserved materials and tools back within reach.";return false;}
            int cursor=1;foreach(var need in r.Materials){if(job.IngredientIds.Skip(cursor).Take(need.Count).Any(id=>!need.Kinds.Contains(all[id].Kind))){reason="A reserved ingredient no longer matches this recipe.";return false;}cursor+=need.Count;}
            if(job.IngredientIds.Any(id=>s.Items.Any(p=>p.ContainerId==id))){reason="Empty the reserved container before continuing.";return false;}
            reason=PrimaryReason(r,all[job.WorkpieceId]);if(reason.Length>0)return false;
            if(r.Tools.Any(cap=>!job.ToolIds.Any(id=>(PortableTechnology.Capabilities(all[id])&cap)!=0))){reason="A required tool is no longer usable.";return false;}
            plan=new PortableCraftPlan{Recipe=r,PrimaryId=job.WorkpieceId,Ingredients=new List<string>(job.IngredientIds),Tools=new List<string>(job.ToolIds)};return true;
        }
        public static ItemCraftJob Start(PlayerInventory s,PortableCraftPlan plan)
        {
            var job=s.Crafts.FirstOrDefault(j=>j.WorkpieceId==plan.PrimaryId);
            if(job==null){job=new ItemCraftJob{WorkpieceId=plan.PrimaryId,RecipeId=plan.Recipe.Id,Action=ItemCraftAction.PortableRecipe,RequiredSeconds=plan.Recipe.WorkSeconds,IngredientIds=new List<string>(plan.Ingredients),ToolIds=new List<string>(plan.Tools),ToolId=plan.Tools.FirstOrDefault()??""};s.Crafts.Add(job);}
            foreach(var other in s.Crafts)other.Running=false;job.Running=true;return job;
        }
        public static Possession Advance(PlayerInventory s,ItemCraftJob job,double dt,PlaySettings settings)
        {
            if(!ResumePlan(s,s.Items,job,out var plan,out _)){job.Running=false;return null;}
            job.WorkSeconds=Math.Min(job.RequiredSeconds,job.WorkSeconds+dt*86400/(settings.RealMinutesPerDay*60*settings.ItemCraftingTimeMultiplier));if(job.WorkSeconds<job.RequiredSeconds)return null;
            if(plan.Recipe.Maintenance)
            {
                var maintained=s.Find(plan.PrimaryId);
                foreach(var id in plan.Ingredients.Where(id=>id!=maintained.Id))s.Items.Remove(s.Find(id));
                ItemMaintenance.Complete(maintained,plan.Recipe.RestoreEdge);s.Crafts.Remove(job);return maintained;
            }
            var ingredients=plan.Ingredients.Select(s.Find).ToArray();var p=s.Find(plan.PrimaryId);var r=plan.Recipe;var template=PortableTechnology.Create(r.Output,p.Position);double length=p.HeightM;
            var origins=ingredients.SelectMany(i=>i.MaterialOrigins??(string.IsNullOrEmpty(i.AppearanceSpecies)?new List<MaterialOrigin>():new List<MaterialOrigin>{new MaterialOrigin{Species=i.AppearanceSpecies,Coat=i.AppearanceCoat,Seed=i.AppearanceSeed,AnimalId=i.SourceAnimalId}})).Select(o=>o.Copy()).ToList();
            var stone=ingredients.FirstOrDefault(i=>StoneCrafting.Rock(i)||i.Kind==PossessionKind.StoneFlake||PortableTechnology.Item(i.Kind)?.Form==PortableForm.Blade||PortableTechnology.Item(i.Kind)?.Form==PortableForm.Point);
            double mass=ingredients.Sum(i=>i.MassKg)*r.Yield;
            // Shaping discards offcuts: a carved arrow must not retain a whole
            // thick pole or coil mass. Multi-hide packs retain their frame mass.
            if(r.Output!=PossessionKind.HidePack){double ratio=(r.Output==PossessionKind.NotchedShaft||r.Output==PossessionKind.LooseStoneSpear||r.Output==PossessionKind.StoneTippedSpear||r.Output==PossessionKind.BoneTippedSpear||r.Output==PossessionKind.Harpoon||r.Output==PossessionKind.Bow)?length/template.HeightM:1;mass=Math.Min(mass,template.MassKg*ratio);}
            int variant=stone?.Variant??p.Variant;double straight=p.WoodStraightness,sound=p.WoodSoundness;
            foreach(var other in ingredients.Where(i=>i.Id!=p.Id))s.Items.Remove(other);
            p.Kind=template.Kind;p.WidthM=template.WidthM;p.HeightM=template.HeightM;p.DepthM=template.DepthM;p.MassKg=mass;p.CellsWide=template.CellsWide;p.CellsHigh=template.CellsHigh;p.EdgeSharpness=template.EdgeSharpness;p.Tint=template.Tint;p.Variant=variant;p.MaterialOrigins=origins;p.FlakeRemovals=0;
            p.WoodStraightness=straight;p.WoodSoundness=sound;
            if(p.Kind==PossessionKind.NotchedShaft||p.Kind==PossessionKind.LooseStoneSpear||p.Kind==PossessionKind.StoneTippedSpear||p.Kind==PossessionKind.BoneTippedSpear||p.Kind==PossessionKind.Harpoon||p.Kind==PossessionKind.Bow){p.HeightM=length;p.CellsHigh=Math.Max(1,(int)Math.Ceiling(length/.1));}
            if(origins.Count>0){p.AppearanceSpecies=origins[0].Species;p.AppearanceCoat=origins[0].Coat;p.AppearanceSeed=origins[0].Seed;p.SourceAnimalId=origins[0].AnimalId;}
            if(!s.CanMove(p.Id,p.Place,p.ContainerId,p.X,p.Y,p.Rotated,out _)){p.Place=PossessionPlace.Ground;p.ContainerId="";if(p.Position.Length<.9)p.Position=new V(1,0,0);}
            s.Crafts.Remove(job);return p;
        }
        public static void Validate(PlayerInventory s,ItemCraftJob job)
        {
            var r=Recipe(job.RecipeId);if(r==null||job.Action!=ItemCraftAction.PortableRecipe||job.IngredientIds==null||job.ToolIds==null||job.IngredientIds.Count!=1+r.Materials.Sum(m=>m.Count)||job.IngredientIds.Distinct().Count()!=job.IngredientIds.Count||job.IngredientIds[0]!=job.WorkpieceId||job.ToolIds.Intersect(job.IngredientIds).Any()||job.IngredientIds.Concat(job.ToolIds).Any(id=>s.Find(id)==null))throw new ArgumentException("Invalid portable crafting project.");
        }
    }
}
