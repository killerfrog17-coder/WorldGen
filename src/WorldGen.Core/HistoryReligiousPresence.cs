using System;
using System.Collections.Generic;
using System.Linq;
namespace WorldGen.Core
{
    public sealed class ReligiousPresence
    {
        public int Faith;
        public bool Public,Household,Association,Institution,RecentContact;
        public int AssociationSupport;
        public bool ResidentEvidence=>Public||Household||Association||Institution;
    }
    public sealed partial class Prehistory
    {
        // A bounded local inventory, NOT an adherent census. In particular a
        // visiting teacher is not counted as an established resident minority.
        public static ReligiousPresence[] ReligiousPresenceAt(PrehistoryState s,int cell)
        {
            var p=ReligiousCommunityAt(s,cell);if(p==null)return Array.Empty<ReligiousPresence>();
            var result=new List<ReligiousPresence>(5);
            ReligiousPresence At(int faith)
            {
                if(faith<0||faith>=s.History.Religions.Length)return null;
                var row=result.FirstOrDefault(x=>x.Faith==faith);if(row==null){row=new ReligiousPresence{Faith=faith};result.Add(row);}return row;
            }
            var row=At(p.Dominant);if(row!=null)row.Public=true;
            row=At(p.Household);if(row!=null)row.Household=true;
            var lay=LayAssociationAt(s,cell);
            if(lay!=null&&lay.Active&&lay.Support>=100){row=At(lay.Faith);if(row!=null){row.Association=true;row.AssociationSupport=lay.Support;}}
            for(int slot=0;slot<2;slot++){var body=ReligiousInstitutionAt(s,cell,slot);if(body==null||body.Support<100)continue;row=At(body.Faith);if(row!=null)row.Institution=true;}
            if(p.LastContact>=0&&s.Years-p.LastContact<=80){row=At(p.Visitor);if(row!=null)row.RecentContact=true;}
            return result.OrderBy(x=>x.Faith).ToArray();
        }
    }
}
