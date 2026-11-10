using System;
using System.Linq;

namespace WorldGen.Core
{
    // Lazy work for one previously encountered band, using its finite saved
    // possessions. No global tribes, daily ticks or replacement supplies.
    public static class NpcCampCatchUp
    {
        public static void Apply(HumanCampMemory origin,HumanCampMemory destination,HumanPersonMemory[] people,ForagerPersonSample[] roster,PlayerInventory loose,bool carryPoles,double workSeconds)
        {
            if(origin==null||destination==null||origin.Group!=destination.Group||workSeconds<=0)return;
            long group=origin.Group;
            var adults=people.Where(p=>!p.Dead&&p.Health>40&&p.Inventory!=null&&roster.Any(r=>r.Id==p.Person&&r.AgeYears>=14)).ToArray();
            if(adults.Length==0)return;
            bool moved=origin.Site!=destination.Site;
            bool AtDestination(Possession item)=>!moved||HumanLocalLife.Angle(item.Position,destination.Position)<1e-8;
            if(moved)
            {
                foreach(var shelter in origin.Objects.Where(o=>o.Blueprint!=null))
                    while(NpcCampWork.NextPacking(shelter.Blueprint,carryPoles) is CampComponent part)
                    {
                        double seconds=Math.Max(15,part.Seconds*.25);if(workSeconds<seconds)break;workSeconds-=seconds;
                        loose.Items.AddRange(NpcCampWork.Remove(shelter,part.Key));
                    }
                // Bind actual recovered goods with actual recovered cordage.
                foreach(long household in roster.Select(r=>r.Household).Distinct())
                {
                    var stock=new PlayerInventory();
                    foreach(var item in loose.Items.Where(p=>p.CampMaterial==group&&p.OwnerHousehold==household&&p.Place==PossessionPlace.Ground).ToArray())NpcBelongings.TransferTree(loose,stock,item);
                    for(int k=0;k<adults.Length;k++)
                    {var bundle=NpcCampWork.Bundle(stock,group,household,origin.Position);if(bundle==null)break;bundle.Place=PossessionPlace.Ground;NpcBelongings.TransferTree(stock,loose,bundle);}
                    foreach(var item in stock.Items.ToArray())NpcBelongings.TransferTree(stock,loose,item);
                }
                foreach(var person in adults)
                {
                    long household=roster.First(r=>r.Id==person.Person).Household;var stock=person.Inventory;
                    foreach(var item in loose.Items.Where(p=>p.CampMaterial==group&&p.OwnerHousehold==household&&p.Place==PossessionPlace.Ground).OrderByDescending(p=>p.Kind==PossessionKind.CampBundle).ToArray())
                    {
                        if(stock.CarriedMassKg+item.MassKg>24||!NpcBelongings.CanTake(stock,item))continue;
                        NpcBelongings.TransferTree(loose,stock,item);
                        var wear=PortableTechnology.Wear(item);
                        if(!(wear.HasValue&&stock.Move(item.Id,wear.Value,"",0,0,false,out _))&&!NpcBelongings.Pack(stock,item))stock.Hold(item.Id,out _);
                    }
                }
                origin.AbandonedCalendarDay=Math.Max(0,origin.AbandonedCalendarDay);
            }
            foreach(var person in adults)
            {
                var stock=person.Inventory;
                foreach(var item in stock.Items.Where(stock.Carried).ToArray())
                {
                    item.Position=destination.Position;
                    if(item.CampMaterial!=group)continue;
                    if(item.Kind==PossessionKind.CampBundle)NpcCampWork.Unbundle(stock,item,destination.Position);
                    else{item.Place=PossessionPlace.Ground;item.ContainerId="";}
                }
                foreach(var item in stock.Items.Where(p=>p.Place==PossessionPlace.Ground&&p.CampMaterial==group&&AtDestination(p)).ToArray())NpcBelongings.TransferTree(stock,loose,item);
            }
            bool canCut=adults.Any(p=>p.Inventory.Items.Any(i=>p.Inventory.Carried(i)&&WoodCrafting.Cutter(i)));
            foreach(var shelter in destination.Objects.Where(o=>o.Blueprint!=null))
            {
                var plan=shelter.Blueprint;
                // Bounded by the actual finite component/material sets. Stops
                // when missing wood, insufficient time or another input blocks it.
                for(int k=0;k<512&&workSeconds>0;k++)
                {
                    int stage=CampBlueprints.Stage(plan);bool progressed=false;
                    foreach(var part in CampBlueprints.Components(plan).Where(c=>c.Stage==stage&&!CampBlueprints.Done(plan,c)))
                    {
                        if(!CampBlueprints.HasMaterial(plan,part))
                        {
                            var material=loose.Items.Where(p=>p.Place==PossessionPlace.Ground&&p.CampMaterial==group&&p.OwnerHousehold==shelter.NpcHousehold&&AtDestination(p)&&CampBlueprints.Suitable(plan,part,p)&&(part.Kg>0||p.HeightM<=part.LengthM+.1||canCut)).OrderBy(p=>p.Id.EndsWith("-material-"+part.Key,StringComparison.Ordinal)?0:1).ThenBy(p=>part.Kg>0?Math.Abs(p.MassKg-part.Kg):p.HeightM).FirstOrDefault();
                            if(material==null)continue;
                            progressed|=CampBlueprints.Supply(plan,part.Key,loose,material.Id,canCut,out _,out _);
                        }
                        double used=CampBlueprints.WorkContribution(plan,part,workSeconds);workSeconds-=used;progressed|=used>0;
                    }
                    if(!progressed)break;
                }
            }
            origin.Disturbed=destination.Disturbed=true;
        }
    }
}
