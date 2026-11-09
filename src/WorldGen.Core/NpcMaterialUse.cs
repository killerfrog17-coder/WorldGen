using System;
using System.Linq;

namespace WorldGen.Core
{
    public static class NpcMaterialUse
    {
        public static bool Tinder(PossessionKind kind)=>kind==PossessionKind.TinderBundle||kind==PossessionKind.DryGrass||kind==PossessionKind.PreparedFibre||kind==PossessionKind.RawFibre||kind==PossessionKind.BarkStrips||kind==PossessionKind.LeafLitter||kind==PossessionKind.ReedBundle;
        // Fuel is consumable wood/fibre, not an excuse to burn a person's spear.
        public static bool Fuel(Possession p)=>p!=null&&p.CampMaterial==0&&(p.Kind==PossessionKind.Branch||p.Kind==PossessionKind.KindlingBundle||CampRules.Tinder(p)||p.Kind==PossessionKind.BarkSheet);
        public static bool Feed(PlayerInventory stock,CampObject fire,Possession item)
        {
            if(stock==null||fire?.Kind!=CampKind.Fire||!stock.Items.Contains(item)||!stock.Carried(item)||!Fuel(item))return false;
            CampRules.Add(fire,item);stock.Crafts.RemoveAll(j=>PortableCrafting.Affects(j,item.Id));stock.Items.Remove(item);return true;
        }
        public static double Eat(PlayerInventory stock,PlayerSurvival nutrition,Possession food)
        {
            if(stock==null||!stock.Items.Contains(food)||!stock.Carried(food)||!Foraging.Food(food))return 0;
            double eaten=Foraging.Consume(nutrition,food,false);if(food.MassKg<.00001)stock.Items.Remove(food);return eaten;
        }
        public static bool HasIgnitionKit(PlayerInventory stock)=>stock!=null&&stock.Items.Any(p=>stock.Carried(p)&&p.Kind==PossessionKind.HandDrillSet&&ItemUseRules.Condition(p)>0);
    }
}
