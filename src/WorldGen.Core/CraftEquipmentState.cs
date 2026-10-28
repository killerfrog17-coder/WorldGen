using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    [Serializable] public sealed class CraftItemLocation
    {
        public string Id="",Container="";public PossessionPlace Place;public int X,Y;public bool Rotated;
        public CraftItemLocation Copy()=>(CraftItemLocation)MemberwiseClone();
        public static CraftItemLocation Of(Possession p)=>new CraftItemLocation{Id=p.Id,Place=p.Place,Container=p.ContainerId,X=p.X,Y=p.Y,Rotated=p.Rotated};
    }
    [Serializable] public sealed class CraftEquipmentState
    {
        public List<CraftItemLocation> PreviousHands=new List<CraftItemLocation>(),Borrowed=new List<CraftItemLocation>();
        public CraftEquipmentState Copy()=>new CraftEquipmentState{PreviousHands=PreviousHands.Select(x=>x.Copy()).ToList(),Borrowed=Borrowed.Select(x=>x.Copy()).ToList()};
        public static CraftEquipmentState Capture(PlayerInventory inventory,IEnumerable<string> borrowed)
        {
            var ids=new HashSet<string>(borrowed);return new CraftEquipmentState{
                PreviousHands=inventory.Items.Where(p=>p.Place==PossessionPlace.RightHand||p.Place==PossessionPlace.LeftHand).Select(CraftItemLocation.Of).ToList(),
                Borrowed=inventory.Items.Where(p=>ids.Contains(p.Id)).Select(CraftItemLocation.Of).ToList()};
        }
        public void Validate()
        {
            if(PreviousHands==null||Borrowed==null||PreviousHands.Count>2||PreviousHands.Concat(Borrowed).Any(p=>p==null||string.IsNullOrEmpty(p.Id)||!Enum.IsDefined(typeof(PossessionPlace),p.Place))||PreviousHands.Select(p=>p.Id).Distinct().Count()!=PreviousHands.Count||Borrowed.Select(p=>p.Id).Distinct().Count()!=Borrowed.Count)throw new ArgumentException("Invalid saved crafting equipment.");
        }
    }
}
