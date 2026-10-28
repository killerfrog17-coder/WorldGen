using System;
using System.Linq;

namespace WorldGen.Core
{
    public enum ItemCraftAction { Flake,TrimBranch,StripBark,DiggingStick,WoodenSpear }
    public static class WoodCrafting
    {
        public static bool Wood(Possession p)=>p!=null&&(p.Kind==PossessionKind.Branch||p.Kind==PossessionKind.TrimmedPole||p.Kind==PossessionKind.DiggingStick||p.Kind==PossessionKind.WoodenSpear);
        public static Possession Branch(ScatterInstance p)=>new Possession{Id="wood-"+p.Id.ToString("x16"),NaturalId=p.Id,Kind=PossessionKind.Branch,Place=PossessionPlace.Ground,Position=p.Position,Variant=p.Variant,
            WidthM=p.HeightM,HeightM=p.WidthM,DepthM=p.HeightM,CellsWide=1,CellsHigh=Math.Max(1,(int)Math.Ceiling(p.WidthM/.1)),MassKg=Math.Max(.03,Math.PI*p.HeightM*p.HeightM/4*p.WidthM*620),WoodStraightness=1-.18*(p.Variant%4),WoodSoundness=p.Variant<4?.95:.5,Tint=new V(1,1,1),GroundRotation=new V(0,p.YawDegrees,90)};
        public static string Label(ItemCraftAction a)=>a==ItemCraftAction.TrimBranch?"Trim branch":a==ItemCraftAction.StripBark?"Strip bark":a==ItemCraftAction.DiggingStick?"Shape digging stick":a==ItemCraftAction.WoodenSpear?"Sharpen wooden spear":"Strike off a cutting flake";
        public static string Reason(Possession p,ItemCraftAction action)
        {
            if(!Wood(p))return "Choose a branch or worked wooden pole.";
            if(action==ItemCraftAction.TrimBranch)return p.Kind!=PossessionKind.Branch?"This piece is already trimmed.":p.WidthM>.065?"Too thick for this flake-based trimming method.":p.WoodSoundness<.35?"This branch is too rotten or fragile to work.":"";
            if(action==ItemCraftAction.StripBark)return p.BarkRemoved?"The bark is already removed.":"";
            if(p.Kind!=PossessionKind.TrimmedPole)return "Trim a suitable branch into a pole first.";
            if(p.WoodSoundness<.65)return "This piece is too weathered or rotten for a durable point.";
            if(action==ItemCraftAction.WoodenSpear)
            {if(p.HeightM<1.4||p.HeightM>2.6)return "A spear needs a 1.4-2.6 m shaft.";if(p.WidthM<.018||p.WidthM>.045)return "A spear shaft must be 1.8-4.5 cm thick.";if(p.WoodStraightness<.7)return "This shaft is too crooked for a spear.";}
            else if(action==ItemCraftAction.DiggingStick)
            {if(p.HeightM<.5||p.HeightM>1.5)return "A digging stick needs a 0.5-1.5 m pole.";if(p.WidthM<.02||p.WidthM>.06)return "A digging stick must be 2-6 cm thick.";if(p.WoodStraightness<.45)return "This piece is too crooked to use comfortably.";}
            else return "Unknown woodworking action.";
            return "";
        }
        public static bool Cutter(Possession p)=>p!=null&&p.Kind==PossessionKind.StoneFlake&&p.EdgeSharpness>=.2;
        public static ItemCraftJob Start(PlayerInventory inventory,string id,string tool,ItemCraftAction action,out string reason)
        {
            var p=inventory.Find(id);reason=Reason(p,action);if(reason.Length>0)return null;
            if(!Cutter(inventory.Find(tool))){reason="Requires a usable cutting flake.";return null;}
            var job=inventory.Crafts.FirstOrDefault(j=>j.WorkpieceId==id);
            if(job!=null&&job.Action!=action){reason="Finish the existing work on this piece first.";return null;}
            if(job==null){double work=action==ItemCraftAction.TrimBranch?90:action==ItemCraftAction.StripBark?150:action==ItemCraftAction.DiggingStick?420:900;job=new ItemCraftJob{WorkpieceId=id,Action=action,RequiredSeconds=work*(.65+p.WidthM*12)};inventory.Crafts.Add(job);}
            foreach(var other in inventory.Crafts)other.Running=false;job.ToolId=tool;job.Running=true;return job;
        }
        public static Possession Advance(PlayerInventory inventory,ItemCraftJob job,double dt,PlaySettings settings)
        {
            var p=inventory.Find(job.WorkpieceId);var tool=inventory.Find(job.ToolId);
            if(Reason(p,job.Action).Length>0||!Cutter(tool)){job.Running=false;return null;}
            job.WorkSeconds=Math.Min(job.RequiredSeconds,job.WorkSeconds+dt*86400/(settings.RealMinutesPerDay*60*settings.ItemCraftingTimeMultiplier));
            if(job.WorkSeconds<job.RequiredSeconds)return null;
            if(job.Action==ItemCraftAction.TrimBranch)p.Kind=PossessionKind.TrimmedPole;
            else if(job.Action==ItemCraftAction.StripBark)p.BarkRemoved=true;
            else p.Kind=job.Action==ItemCraftAction.DiggingStick?PossessionKind.DiggingStick:PossessionKind.WoodenSpear;
            p.MassKg*=job.Action==ItemCraftAction.StripBark?.93:.97;tool.EdgeSharpness=Math.Max(0,tool.EdgeSharpness-((job.Action==ItemCraftAction.WoodenSpear?.07:.025)/Math.Max(.3,ToolStone.Quality(tool.Variant))));
            inventory.Crafts.Remove(job);return p;
        }
    }
}
