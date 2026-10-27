using System;
using System.Linq;

namespace WorldGen.Core
{
    // Inventory/site transfers belong to the saved game rules, not a menu.
    // Trial inventories are published only after carrying checks succeed.
    public static class SurvivalStorage
    {
        static bool Owns(SurvivalState state,SurvivalSite site)=>site!=null&&state?.Sites!=null&&state.Sites.Any(s=>ReferenceEquals(s,site));
        static SurvivalStack Stored(SurvivalStack source,int count,long id)
        {
            var copy=source.Copy();copy.Id=id;copy.Count=count;copy.Worn=false;copy.Container=-1;copy.X=copy.Y=0;copy.Rotated=false;return copy;
        }
        public static bool Store(SurvivalState state,SurvivalSite site,long itemId,int count,out string message)
        {
            message="This is not a completed storage rack.";
            if(!Owns(state,site)||site.Kind!="cache"||site.Progress<1)return false;
            var source=state.Inventory.Items.FirstOrDefault(i=>i.Id==itemId);
            if(source==null||count<1||count>source.Count){message="That carried stack is no longer available.";return false;}
            if(SurvivalItems.Get(source.Item).StorageWidth>0){message="Carrying attachments must be emptied and dropped separately.";return false;}
            long last=site.Contents.Length==0?0:site.Contents.Max(i=>i.Id);
            if(last==long.MaxValue){message="Stored item identifiers are exhausted.";return false;}
            var trial=state.Inventory.Copy();if(!trial.RemoveStack(itemId,count)){message="The remaining equipment cannot be carried.";return false;}
            var contents=site.Contents.Concat(new[]{Stored(source,count,Math.Max(0,last)+1)}).ToArray();
            state.Inventory=trial;site.Contents=contents;message="Stored "+SurvivalItems.Get(source.Item).Name.ToLowerInvariant()+".";return true;
        }
        // The actual entry reference rejects stale UI requests and also supports
        // old saves whose site-local IDs were not unique. New stores use max+1.
        public static bool Take(SurvivalState state,SurvivalSite site,SurvivalStack entry,int count,out string message)
        {
            message="That stored stack is no longer available.";
            if(!Owns(state,site)||site.Progress<1||(site.Kind!="cache"&&site.Kind!="drop")||entry==null||!site.Contents.Any(i=>ReferenceEquals(i,entry))||count<1||count>entry.Count)return false;
            var trial=state.Inventory.Copy();
            if(!trial.TryAdd(entry.Item,count,out message,entry.AgeDays,entry.Fraction,entry.Condition))return false;
            var contents=site.Contents.Select(i=>{var copy=i.Copy();if(ReferenceEquals(i,entry))copy.Count-=count;return copy;}).Where(i=>i.Count>0).ToArray();
            state.Inventory=trial;site.Contents=contents;
            if(site.Kind=="drop"&&contents.Length==0)state.Sites=state.Sites.Where(s=>!ReferenceEquals(s,site)).ToArray();
            message="Took "+SurvivalItems.Get(entry.Item).Name.ToLowerInvariant()+".";return true;
        }
        public static bool Drop(SurvivalState state,long itemId,int count,V point,out SurvivalSite site,out string message)
        {
            site=null;message="That carried stack is no longer available.";
            var source=state?.Inventory?.Items.FirstOrDefault(i=>i.Id==itemId);
            if(source==null||count<1||count>source.Count)return false;
            if(!SurvivalMath.Finite(point.Length)||Math.Abs(point.Length-1)>.00001){message="Invalid drop location.";return false;}
            if(state.NextSite<1||state.NextSite==long.MaxValue){message="Camp identifiers are exhausted.";return false;}
            var trial=state.Inventory.Copy();if(!trial.RemoveStack(itemId,count)){message="Empty the carrying attachment first.";return false;}
            site=new SurvivalSite{Id=state.NextSite,Kind="drop",Position=point,Contents=new[]{Stored(source,count,1)}};
            var sites=state.Sites.Concat(new[]{site}).ToArray();state.Inventory=trial;state.Sites=sites;state.NextSite++;
            message="Dropped "+SurvivalItems.Get(source.Item).Name.ToLowerInvariant()+".";return true;
        }
    }
}
