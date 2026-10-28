using System;
using System.Linq;

namespace WorldGen.Core
{
    [Serializable]
    public sealed class ItemCraftJob
    {
        public string WorkpieceId="",ToolId="";
        [System.Runtime.Serialization.OptionalField] public CraftEquipmentState Equipment;
        public double WorkSeconds,RequiredSeconds;
        public bool Running;
        [System.Runtime.Serialization.OptionalField] public ItemCraftAction Action;
        public double Fraction=>Math.Max(0,Math.Min(1,WorkSeconds/RequiredSeconds));
        [System.Runtime.Serialization.OptionalField] public string RecipeId;
        [System.Runtime.Serialization.OptionalField] public System.Collections.Generic.List<string> IngredientIds,ToolIds;
        public ItemCraftJob Copy(){var j=(ItemCraftJob)MemberwiseClone();j.Equipment=Equipment?.Copy();j.IngredientIds=IngredientIds==null?null:new System.Collections.Generic.List<string>(IngredientIds);j.ToolIds=ToolIds==null?null:new System.Collections.Generic.List<string>(ToolIds);return j;}
    }
    /// <summary>Ordinary stones acquire roles by properties, not by renaming or conversion.</summary>
    public static class StoneCrafting
    {
        public static bool Rock(Possession p)=>p!=null&&(p.Kind==PossessionKind.SmallRock||p.Kind==PossessionKind.LargeRock);
        public static double FractureQuality(Possession p)=>Rock(p)?ToolStone.Quality(p.Variant):0;
        public static string Material(Possession p)=>ToolStone.Name(p.Variant);
        public static string WorkpieceReason(Possession p)
        {
            if(!Rock(p))return "Choose an ordinary stone to strike a flake from.";
            if(FractureQuality(p)<=0)return "This material is unlikely to yield a useful cutting flake.";
            if(p.MassKg<.06||p.WidthM<.04||p.HeightM<.02)return "This stone is too small or thin for another useful flake.";
            if(p.MassKg>10||p.WidthM>.45)return "This piece is too large for this hand-flaking method.";
            return "";
        }
        public static bool StrikingStone(Possession p)=>Rock(p)&&p.Hands==1&&p.MassKg>=.06&&p.WidthM>=.035&&p.WidthM<=.22&&p.HeightM/p.WidthM>=.22&&(ToolStone.Family(p.Variant)!=RockFamily.Mudstone);
        public static string PairReason(Possession workpiece,Possession tool)
        {
            var reason=WorkpieceReason(workpiece);if(reason.Length>0)return reason;
            if(tool==null||tool.Id==workpiece.Id||!StrikingStone(tool))return "Requires another suitably sized, compact stone for striking.";
            return "";
        }
        public static ItemCraftJob Start(PlayerInventory inventory,string workpiece,string tool,out string reason)
        {
            reason=PairReason(inventory.Find(workpiece),inventory.Find(tool));if(reason.Length>0)return null;
            var job=inventory.Crafts.FirstOrDefault(j=>j.WorkpieceId==workpiece);
            if(job==null)
            {
                var stone=inventory.Find(workpiece);
                // A preparation-and-flaking cycle, not a claim that one blow takes minutes.
                double required=120*(.85+.25*Math.Min(4,stone.MassKg))/(.6+.4*FractureQuality(stone));
                job=new ItemCraftJob{WorkpieceId=workpiece,RequiredSeconds=required};inventory.Crafts.Add(job);
            }
            foreach(var other in inventory.Crafts)other.Running=false;
            job.ToolId=tool;job.Running=true;return job;
        }
        public static double RealSecondsLeft(ItemCraftJob job,PlaySettings settings)=>(job.RequiredSeconds-job.WorkSeconds)*settings.RealMinutesPerDay*60/86400*settings.ItemCraftingTimeMultiplier;
        public static Possession Advance(PlayerInventory inventory,ItemCraftJob job,double realSeconds,PlaySettings settings)
        {
            if(!job.Running||realSeconds<=0)return null;
            if(double.IsNaN(realSeconds)||double.IsInfinity(realSeconds))throw new ArgumentOutOfRangeException(nameof(realSeconds));
            if(PortableCrafting.IsJob(job))return PortableCrafting.Advance(inventory,job,realSeconds,settings);
            if(job.Action!=ItemCraftAction.Flake)return WoodCrafting.Advance(inventory,job,realSeconds,settings);
            var core=inventory.Find(job.WorkpieceId);var tool=inventory.Find(job.ToolId);
            if(PairReason(core,tool).Length>0){job.Running=false;return null;}
            job.WorkSeconds=Math.Min(job.RequiredSeconds,job.WorkSeconds+realSeconds*86400/(settings.RealMinutesPerDay*60*settings.ItemCraftingTimeMultiplier));
            if(job.WorkSeconds<job.RequiredSeconds)return null;
            double quality=FractureQuality(core),w=Math.Min(.14,core.WidthM*.72),d=Math.Min(.09,core.DepthM*.62),h=Math.Min(.014,core.HeightM*.25);
            double mass=Math.Min(core.MassKg*.18,w*d*h*.4*2600);h=mass/(w*d*.4*2600);
            var flake=new Possession{Id="flake-"+Guid.NewGuid().ToString("N"),Kind=PossessionKind.StoneFlake,Place=PossessionPlace.Ground,Position=core.Position,
                WidthM=w,HeightM=h,DepthM=d,MassKg=mass,Variant=core.Variant,Tint=core.Tint,CellsWide=Math.Max(1,(int)Math.Ceiling(w/.1)),CellsHigh=Math.Max(1,(int)Math.Ceiling(d/.1)),EdgeSharpness=.6+.4*quality};
            double shrink=Math.Pow((core.MassKg-mass)/core.MassKg,1.0/3);core.MassKg-=mass;core.WidthM*=shrink;core.HeightM*=shrink;core.DepthM*=shrink;core.FlakeRemovals++;
            core.CellsWide=Math.Max(1,(int)Math.Ceiling(core.WidthM/.1));core.CellsHigh=Math.Max(1,(int)Math.Ceiling(core.HeightM/.1));
            core.Kind=core.WidthM<=.2&&core.MassKg<=2?PossessionKind.SmallRock:PossessionKind.LargeRock;
            inventory.Items.Add(flake);inventory.Crafts.Remove(job);return flake;
        }
        public static void Validate(PlayerInventory inventory)
        {
            bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
            if(inventory.Items.Any(p=>p.FlakeRemovals<0||!Finite(p.EdgeSharpness)||p.EdgeSharpness<0||p.EdgeSharpness>1))throw new ArgumentException("Invalid stone working state.");
            if(inventory.Crafts.Any(j=>j==null)||inventory.Crafts.Select(j=>j.WorkpieceId).Distinct().Count()!=inventory.Crafts.Count||inventory.Crafts.Count(j=>j.Running)>1)throw new ArgumentException("Invalid crafting projects.");
            foreach(var job in inventory.Crafts)
                if(!Enum.IsDefined(typeof(ItemCraftAction),job.Action)||inventory.Find(job.WorkpieceId)==null||(!PortableCrafting.IsJob(job)&&(inventory.Find(job.ToolId)==null||job.WorkpieceId==job.ToolId))||!Finite(job.WorkSeconds)||!Finite(job.RequiredSeconds)||job.RequiredSeconds<=0||job.WorkSeconds<0||job.WorkSeconds>=job.RequiredSeconds)throw new ArgumentException("Invalid saved crafting progress.");
            foreach(var job in inventory.Crafts)job.Equipment?.Validate();
            foreach(var job in inventory.Crafts.Where(PortableCrafting.IsJob))PortableCrafting.Validate(inventory,job);
        }
    }
}
