using System;

namespace WorldGen.Core
{
    public sealed class NpcDiscussion
    {
        public string Topic,FirstView,SecondView;public bool Disagreement;
        public static NpcDiscussion For(NpcPersonality a,NpcPersonality b,ForagerCulture culture,bool children=false)
        {
            double[] differences={Math.Abs(a.Kindness-b.Kindness),Math.Abs((a.Sociability-a.Vigilance)-(b.Sociability-b.Vigilance))*.5,Math.Abs(a.Ambition-b.Ambition),Math.Abs(a.Devotion-b.Devotion)};
            int axis=0;for(int i=1;i<differences.Length;i++)if(differences[i]>differences[axis])axis=i;
            if(children)axis=0;
            string View(NpcPersonality p)=>children?p.Kindness>.5?"everyone should get a turn in the game":"the quickest or most skilful player should get the next turn":axis==0?p.Kindness>.5?"sharing food widely is worth protecting":"households should retain a fair portion for their own dependants":axis==1?p.Sociability>p.Vigilance?"introduced visitors bring useful news and connections":"strangers should first earn confidence and be introduced":axis==2?p.Ambition>.5?"proven organizers should be heard and their expertise recognized":"decisions work better when other adults have a real say":p.Devotion>.5?"carefully keeping customary rites matters":"practical care for living people matters more than getting every ritual detail right";
            return new NpcDiscussion{Topic=children?"taking turns in their game":axis==0?"how households share food":axis==1?"welcoming unfamiliar visitors":axis==2?"how people should make camp decisions":"the importance of customary rites",FirstView=View(a),SecondView=View(b),Disagreement=differences[axis]>.42&&(children||culture.PrestigeCompetition>.25||culture.ConflictAvoidance<.75)};
        }
    }
}
