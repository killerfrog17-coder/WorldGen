using System;
using System.Linq;

namespace WorldGen.Core
{
    [Serializable] public sealed class SurvivalSite
    {
        public long Id;public string Kind;public V Position;public double Heading,Progress=1,FuelHours,AgeDays;public bool Lit;public SurvivalStack[] Contents=Array.Empty<SurvivalStack>();
        public SurvivalSite Copy(){var s=(SurvivalSite)MemberwiseClone();s.Contents=Contents.Select(i=>i.Copy()).ToArray();return s;}
    }
    [Serializable] public sealed class SurvivalCorpse
    {
        public string Slug;public ulong SpawnId;public V Position;public double Heading,MassKg,MeatKg,FatKg,OrganKg,HideUnits,BoneKg,SinewUnits,AgeDays;public bool Skinned;
        [System.Runtime.Serialization.OptionalField] public V AnchorPosition;
        [System.Runtime.Serialization.OptionalField] public bool HasAnchor;
        [System.Runtime.Serialization.OptionalField] public float[] SettledPose;
        [System.Runtime.Serialization.OptionalField] public string PoseSource;
        [System.Runtime.Serialization.OptionalField] public uint AppearanceHash;
        [System.Runtime.Serialization.OptionalField] public int CoatSeed;
        [System.Runtime.Serialization.OptionalField] public double ColdC,Aridity;
        [System.Runtime.Serialization.OptionalField] public double RootOffsetM;
        public SurvivalCorpse Copy(){var c=(SurvivalCorpse)MemberwiseClone();c.SettledPose=SettledPose==null?null:(float[])SettledPose.Clone();return c;}
    }
    [Serializable] public sealed class SurvivalResourceUse
    {public string Key;public double UsedDay;public int Count;public SurvivalResourceUse Copy()=>(SurvivalResourceUse)MemberwiseClone();}
    [Serializable] public sealed class SurvivalState
    {
        public int Version=1;public SurvivalMode Mode;public SurvivalInventory Inventory=SurvivalItems.Starter();
        public int Seed,ConsumptionEvents;
        public double Health=100,Stamina=100,EnergyKcal=6000,WaterDeficitL,Fatigue=.05,CoreTemperatureC=37,Wetness,Bleeding,Illness,Days;
        public long NextSite=1;public bool Dead;public SurvivalSite[] Sites=Array.Empty<SurvivalSite>();
        public SurvivalCorpse[] Corpses=Array.Empty<SurvivalCorpse>();public SurvivalResourceUse[] Resources=Array.Empty<SurvivalResourceUse>();
        public SurvivalState Copy(){var s=(SurvivalState)MemberwiseClone();s.Inventory=Inventory.Copy();s.Sites=Sites.Select(x=>x.Copy()).ToArray();s.Corpses=Corpses.Select(x=>x.Copy()).ToArray();s.Resources=Resources.Select(x=>x.Copy()).ToArray();return s;}
        public void Validate()
        {
            if(Version!=1||!Enum.IsDefined(typeof(SurvivalMode),Mode)||Inventory==null||Sites==null||Corpses==null||Resources==null)throw new ArgumentException("Unsupported survival state.");
            Inventory.Validate();
            bool Range(double n,double lo,double hi)=>SurvivalMath.Finite(n)&&n>=lo&&n<=hi;
            if(!Range(Health,0,100)||!Range(Stamina,0,100)||!Range(EnergyKcal,0,16000)||!Range(WaterDeficitL,0,12)||!Range(Fatigue,0,1)||!Range(CoreTemperatureC,20,45)||!Range(Wetness,0,1)||!Range(Bleeding,0,10)||!Range(Illness,0,1)||!Range(Days,0,1e10)||NextSite<1)throw new ArgumentException("Invalid survival physiology.");
            bool Point(V p)=>Range(p.Length,.99999,1.00001);
            var ids=new System.Collections.Generic.HashSet<long>();foreach(var s in Sites)
                if(s==null||!ids.Add(s.Id)||s.Id<1||s.Id>=NextSite||s.Kind==null||!Point(s.Position)||!Range(s.Heading,-1e9,1e9)||!Range(s.Progress,0,1)||!Range(s.FuelHours,0,10000)||!Range(s.AgeDays,0,1e10)||s.Contents==null)throw new ArgumentException("Invalid camp structure.");
            foreach(var site in Sites)foreach(var item in site.Contents)
                if(item==null||!SurvivalItems.TryGet(item.Item,out var d)||item.Count<1||!Range(item.AgeDays,0,1e10)||!Range(item.Condition,0,1)||!Range(item.Fraction,double.Epsilon,1)||item.Fraction!=1&&d.Kind!=SurvivalItemKind.Food)throw new ArgumentException("Invalid stored camp item.");
            foreach(var c in Corpses)if(c==null||c.Slug==null||!Point(c.Position)||c.HasAnchor&&!Point(c.AnchorPosition)||!Range(c.MassKg,.00001,1e8)||!Range(c.MeatKg,0,c.MassKg)||!Range(c.FatKg,0,c.MassKg)||!Range(c.OrganKg,0,c.MassKg)||!Range(c.BoneKg,0,c.MassKg)||!Range(c.HideUnits,0,1)||!Range(c.SinewUnits,0,100)||!Range(c.AgeDays,0,1e10)||c.SettledPose!=null&&(c.SettledPose.Length%16!=0||c.SettledPose.Any(p=>!SurvivalMath.Finite(p))))throw new ArgumentException("Invalid harvested animal.");
            if(Corpses.Select(c=>c.SpawnId).Distinct().Count()!=Corpses.Length||Corpses.Any(c=>!Range(c.Heading,-1e9,1e9)||!SurvivalMath.Finite(c.ColdC)||!SurvivalMath.Finite(c.Aridity)||!Range(c.RootOffsetM,-1e7,1e7)))throw new ArgumentException("Invalid carcass identity or appearance.");
            foreach(var r in Resources)if(r==null||r.Key==null||!Range(r.UsedDay,0,Days)||r.Count<0)throw new ArgumentException("Invalid gathered resource record.");
        }
    }
    public struct SurvivalEnvironment
    {
        public double TemperatureC,WindMps,RainMmPerHour,SpeedMps,CarriedKg,Shelter,FireWarmth,Insulation;
        public bool Sprinting,Resting,Sleeping,InWater;
    }
    public static class SurvivalPhysiology
    {
        public static void Advance(SurvivalState s,double playableSeconds,SurvivalEnvironment e)
        {
            if(s==null||!SurvivalMath.Finite(playableSeconds)||playableSeconds<0||!SurvivalMath.Finite(e.TemperatureC)||!SurvivalMath.Finite(e.WindMps))throw new ArgumentException("Invalid survival step.");
            if(s.Mode!=SurvivalMode.Survival||s.Dead)return;
            // Integrate in short physiological intervals independent of frame rate.
            while(playableSeconds>1e-8){double dt=Math.Min(playableSeconds,60);Step(s,dt,e);playableSeconds-=dt;if(s.Dead)break;}
        }
        static void Step(SurvivalState s,double seconds,SurvivalEnvironment e)
        {
            double hours=seconds/3600,day=seconds/86400,shelter=SurvivalMath.Clamp(e.Shelter),speed=Math.Max(0,e.SpeedMps);
            s.Days+=day;
            double exertion=speed*(.8+e.CarriedKg/100)+(e.Sprinting?2:0),heat=Math.Max(0,e.TemperatureC-25);
            s.EnergyKcal=Math.Max(0,s.EnergyKcal-(75+speed*115*(1+e.CarriedKg/80)+(e.Sprinting?180:0)+Math.Max(0,36-s.CoreTemperatureC)*65)*hours);
            s.WaterDeficitL=SurvivalMath.Clamp(s.WaterDeficitL+(.09+exertion*.045+heat*.014+s.Illness*.25)*hours,0,12);
            s.Fatigue=SurvivalMath.Clamp(s.Fatigue+(e.Sleeping?-.13:e.Resting?-.025:.032+exertion*.004)*hours);
            double wetTarget=e.InWater?1:Math.Min(1,e.RainMmPerHour*.22)*(1-shelter);
            double dryRate=.35+Math.Max(0,e.TemperatureC-10)*.015+e.FireWarmth*.1;
            s.Wetness=SurvivalMath.Clamp(s.Wetness+(wetTarget>s.Wetness?(wetTarget-s.Wetness)*3:(wetTarget-s.Wetness)*dryRate)*hours);
            double insulation=Math.Max(0,e.Insulation)*(1-s.Wetness*.65)+shelter*1.2;
            double effective=e.TemperatureC+e.FireWarmth+insulation*9-Math.Sqrt(Math.Max(0,e.WindMps))*(1-shelter)*2.5-s.Wetness*8+exertion*1.8;
            double equilibrium=37+(effective<18?(effective-18)*.065:effective>34?(effective-34)*.08:0);
            if(e.InWater)equilibrium-=Math.Max(0,24-e.TemperatureC)*.1;
            double k=e.InWater?1.8:.55;s.CoreTemperatureC=SurvivalMath.Clamp(equilibrium+(s.CoreTemperatureC-equilibrium)*Math.Exp(-k*hours),20,45);
            double loss=(e.Sprinting?8:speed>4?3:speed>.2?-3:-12)*(seconds/60);
            if(loss>0)s.Stamina=Math.Max(0,s.Stamina-loss);else s.Stamina=Math.Min(MaxStamina(s),s.Stamina-loss);
            double harm=Math.Max(0,s.WaterDeficitL-3)*2+Math.Max(0,35-s.CoreTemperatureC)*5+Math.Max(0,s.CoreTemperatureC-39)*7+s.Bleeding*8+s.Illness*1.5+(s.EnergyKcal<=0?1.3:0);
            s.Health=SurvivalMath.Clamp(s.Health-harm*hours,0,100);
            if(harm<.01&&s.EnergyKcal>1500&&s.WaterDeficitL<1&&s.Fatigue<.7)s.Health=Math.Min(100,s.Health+(e.Sleeping?2:.4)*hours);
            s.Bleeding=Math.Max(0,s.Bleeding-hours*.05);s.Illness=Math.Max(0,s.Illness-hours*.005);
            double ageing=day*Math.Pow(2,SurvivalMath.Clamp(e.TemperatureC-15,-30,35)/10);
            foreach(var item in s.Inventory.Items){var d=SurvivalItems.Get(item.Item);if(d.ShelfDays>0)item.AgeDays+=ageing;}
            foreach(var corpse in s.Corpses)corpse.AgeDays+=ageing;
            foreach(var site in s.Sites){site.AgeDays+=day;if(site.Lit){site.FuelHours=Math.Max(0,site.FuelHours-hours);if(site.FuelHours<=0)site.Lit=false;}foreach(var item in site.Contents)if(SurvivalItems.Get(item.Item).ShelfDays>0)item.AgeDays+=ageing;}
            s.Dead=s.Health<=0;
        }
        public static double MaxStamina(SurvivalState s)=>100*SurvivalMath.Clamp(1-s.Fatigue*.4-Math.Max(0,s.WaterDeficitL-1)*.1,.2,1);
        public static double MovementFactor(SurvivalState s)=>s.Mode!=SurvivalMode.Survival?1:SurvivalMath.Clamp(.45+.55*s.Health/100-Math.Max(0,s.WaterDeficitL-2)*.05-s.Fatigue*.15,.25,1);
        public static bool Eat(SurvivalState s,long id,out string message)
        {
            var stack=s.Inventory.Items.FirstOrDefault(i=>i.Id==id);if(stack==null){message="That item is no longer here.";return false;}var d=SurvivalItems.Get(stack.Item);
            if(d.Kcal<=0&&d.WaterMl<=0){message="This is not food or drink.";return false;}
            bool spoiled=d.ShelfDays>0&&stack.AgeDays>d.ShelfDays;
            if(d.Id=="waterskin"||d.Id=="raw-water")
            {
                // Keep the vessel; emptying it cannot silently destroy it on a full inventory.
                var trial=s.Inventory.Copy();trial.RemoveStack(id,1);if(!trial.TryAdd("empty-waterskin",1,out message))return false;s.Inventory=trial;
            }
            else if(!s.Inventory.RemoveStack(id,1)){message="This item cannot be removed.";return false;}
            s.EnergyKcal=Math.Min(16000,s.EnergyKcal+d.Kcal*stack.Fraction);s.WaterDeficitL=Math.Max(0,s.WaterDeficitL-d.WaterMl*stack.Fraction/1000);
            double risk=spoiled?.9:d.Id=="raw-water"?.12:d.Raw&&d.Id!="roots"?.15:0;
            bool sick=Rng.Value(s.Seed,"survival-food-risk",s.ConsumptionEvents++)<risk;
            if(sick)s.Illness=Math.Min(1,s.Illness+(spoiled?.6:.25));
            message=sick?"The food or untreated water makes you feel ill.":"Consumed "+d.Name.ToLowerInvariant()+".";return true;
        }
        public static SurvivalCorpse Carcass(string slug,ulong id,V point,double massKg,double heading)
        {
            massKg=Math.Max(.01,massKg);
            return new SurvivalCorpse{Slug=slug,SpawnId=id,Position=point,AnchorPosition=point,HasAnchor=true,Heading=heading,MassKg=massKg,MeatKg=massKg*.32,FatKg=massKg*.05,OrganKg=massKg*.06,BoneKg=massKg*.12,HideUnits=massKg>.5?(massKg<15?.2:1):0,SinewUnits=Math.Min(8,massKg*.008/.04)};
        }
        public static bool Harvest(SurvivalState s,SurvivalCorpse c,string part,out string message)
        {
            if(!s.Inventory.Items.Any(i=>SurvivalItems.Get(i.Item).Sharp)){message="A sharp stone or knife is needed for butchery.";return false;}
            string item;double available,unit;
            switch(part){case "hide":item=c.MassKg<15?"small-hide":"hide";available=c.HideUnits;unit=item=="small-hide"?.2:1;break;case "fat":item="fat";available=c.FatKg;unit=.1;break;case "organs":item="organs";available=c.OrganKg;unit=.2;break;case "bone":item="bone";available=c.BoneKg;unit=.3;break;case "sinew":item="sinew";available=c.SinewUnits;unit=1;break;default:item="raw-meat";available=c.MeatKg;unit=.25;break;}
            double fraction=1;if(SurvivalItems.Get(item).Kind==SurvivalItemKind.Food&&available>1e-8){double portion=Math.Min(unit,available);fraction=portion/unit;unit=portion;}
            if(available+1e-8<unit){message="There is no usable "+part+" remaining.";return false;}
            if(!s.Inventory.TryAdd(item,1,out message,c.AgeDays,fraction))return false;
            switch(part){case "hide":c.HideUnits-=unit;c.Skinned=true;break;case "fat":c.FatKg-=unit;break;case "organs":c.OrganKg-=unit;break;case "bone":c.BoneKg-=unit;break;case "sinew":c.SinewUnits-=unit;break;default:c.MeatKg-=unit;break;}
            message="Harvested "+SurvivalItems.Get(item).Name.ToLowerInvariant()+".";return true;
        }
    }
}
