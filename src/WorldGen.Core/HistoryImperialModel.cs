using System;
using System.Linq;
namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalImperialSystem
    {
        public HistoricalImperialGovernment[] Governments=Array.Empty<HistoricalImperialGovernment>();
        public HistoricalTerritorialAdministration Territories;
        [System.Runtime.Serialization.OptionalField] public HistoricalProvincialFrontiers Frontiers;
        public HistoricalImperialSystem Copy()=>new HistoricalImperialSystem{Governments=Governments.Select(g=>g.Copy()).ToArray(),Territories=Territories?.Copy(),Frontiers=Frontiers?.Copy()};
    }
    [Serializable] public sealed class HistoricalImperialGovernment
    {
        public int Patron=-1,SinceYear,AutonomyMilli=800,ArrearsYears,LastReportYear,LastPatronSuccession,SubjectCount;
        public long SubjectPopulation,SubjectAreaKm2,SubjectArmy,ReportedPopulation,ReportedAreaKm2,ReportedArmy,AnnualTribute,TotalTribute;
        public bool Imposed;
        public long AdministrativePostCost;
        public int Height;
        public int[] ChildrenByHeight=new int[8];
        public HistoricalLocalBag AdministrativePosts=new HistoricalLocalBag();
        public HistoricalImperialGovernment Copy(){var copy=(HistoricalImperialGovernment)MemberwiseClone();copy.AdministrativePosts=AdministrativePosts.Copy();copy.ChildrenByHeight=(int[])ChildrenByHeight.Clone();return copy;}
    }
    public static class HistoricalImperialHierarchy
    {
        public const int MaximumDepth=8;
        public static int Patron(PrehistoryState s,int polity)
        {
            var a=s.History?.Aggregates;int current=a!=null?a.Resolve(polity,s.History):polity>=0&&polity<(s.History?.Polities.Length??0)&&s.History.Polities[polity].EndedYear<0?polity:-1;
            if(current<0||a?.Empires==null)return -1;int raw=a.Empires.Governments[current].Patron;
            return raw<0?-1:a.Resolve(raw,s.History);
        }
        public static int Sovereign(PrehistoryState s,int polity)
        {
            var a=s.History?.Aggregates;int current=a!=null?a.Resolve(polity,s.History):polity>=0&&polity<(s.History?.Polities.Length??0)&&s.History.Polities[polity].EndedYear<0?polity:-1;
            if(current<0||a?.Empires==null)return current;
            for(int depth=0;depth<MaximumDepth;depth++)
            {int next=Patron(s,current);if(next<0)return current;if(next==current)throw new InvalidOperationException("Cyclic imperial allegiance.");current=next;}
            throw new InvalidOperationException("Imperial delegation exceeds its bounded depth.");
        }
        public static long Population(PrehistoryState s,int polity)=>s.History.Polities[polity].Population+(s.History.Aggregates?.Empires?.Governments[polity].SubjectPopulation??0);
        public static long AreaKm2(PrehistoryState s,int polity)=>s.History.Polities[polity].AreaKm2+(s.History.Aggregates?.Empires?.Governments[polity].SubjectAreaKm2??0);
        public static int InstalledPractices(HistoricalCulture culture)=>culture.Technical==null?culture.Technology.Count(y=>y>0):culture.Technical.Deployment.Count(y=>y>=300);
    }
    public sealed partial class Prehistory
    {
        void InitializeImperialSystem(PrehistoryState s)
        {
            var a=s.History.Aggregates;if(a.Empires!=null)return;
            a.Empires=new HistoricalImperialSystem{Governments=s.History.Polities.Select(p=>new HistoricalImperialGovernment{SinceYear=s.Years,LastReportYear=s.Years}).ToArray()};
            InitializeTerritorialAdministration(s);
        }
        void ValidateImperialSystem(PrehistoryState s)
        {
            var h=s.History;var a=h.Aggregates;
            if(!TechnicalHistory(s)){if(a.Empires!=null)throw new ArgumentException("Legacy imperial extension.");return;}
            var records=a.Empires?.Governments;if(records==null||records.Length!=h.Polities.Length)throw new ArgumentException("Imperial government alignment.");
            var population=new long[records.Length];var area=new long[records.Length];var army=new long[records.Length];var count=new int[records.Length];var heights=new int[records.Length*8];
            for(int i=0;i<records.Length;i++)
            {
                var g=records[i];if(g==null||g.Patron<-1||g.Patron>=records.Length||g.Patron==i||g.SinceYear>s.Years||g.LastReportYear>s.Years||g.AutonomyMilli<0||g.AutonomyMilli>1000||g.ArrearsYears<0||g.ArrearsYears>100||g.SubjectPopulation<0||g.SubjectAreaKm2<0||g.SubjectArmy<0||g.SubjectCount<0||g.ReportedPopulation<0||g.ReportedAreaKm2<0||g.ReportedArmy<0||g.AnnualTribute<0||g.TotalTribute<0)throw new ArgumentException("Invalid imperial government "+i+".");
                if(g.ChildrenByHeight?.Length!=8||g.Height<0||g.Height>=8||g.ChildrenByHeight.Any(v=>v<0))throw new ArgumentException("Invalid imperial hierarchy height.");
                int height=0;for(int k=0;k<8;k++)if(g.ChildrenByHeight[k]>0)height=k+1;if(height!=g.Height)throw new ArgumentException("Imperial height cache.");
                if(h.Polities[i].EndedYear>=0)continue;
                HistoricalImperialHierarchy.Sovereign(s,i);int patron=HistoricalImperialHierarchy.Patron(s,i);if(patron<0)continue;
                population[patron]+=g.ReportedPopulation;area[patron]+=g.ReportedAreaKm2;army[patron]+=g.ReportedArmy;count[patron]+=1+g.SubjectCount;
                heights[patron*8+g.Height]++;
            }
            for(int i=0;i<records.Length;i++)if(h.Polities[i].EndedYear<0&&(population[i]!=records[i].SubjectPopulation||area[i]!=records[i].SubjectAreaKm2||army[i]!=records[i].SubjectArmy||count[i]!=records[i].SubjectCount))throw new ArgumentException("Imperial cached obligation totals "+i+".");
            for(int i=0;i<records.Length;i++)if(h.Polities[i].EndedYear<0)for(int k=0;k<8;k++)if(heights[i*8+k]!=records[i].ChildrenByHeight[k])throw new ArgumentException("Imperial direct-child height counts.");
        }
        void AppendImperialGovernment(PrehistoryState s,int predecessor=-1)
        {
            var a=s.History.Aggregates;if(!TechnicalHistory(s))return;
            var next=predecessor<0?new HistoricalImperialGovernment{SinceYear=s.Years,LastReportYear=s.Years}:a.Empires.Governments[predecessor].Copy();
            a.Empires.Governments=a.Empires.Governments.Concat(new[]{next}).ToArray();
            // Succession roots preserve every existing local allegiance without
            // a census of subjects, cities or territorial holdings.
            if(predecessor>=0)a.Empires.Governments[predecessor]=new HistoricalImperialGovernment{SinceYear=s.Years,LastReportYear=s.Years};
        }
        void ImperialDelta(PrehistoryState s,int patron,long population,long area,long army,int count)
        {
            var h=s.History;var a=h.Aggregates;
            for(int depth=0;patron>=0&&depth<HistoricalImperialHierarchy.MaximumDepth;depth++)
            {
                patron=a.Resolve(patron,h);if(patron<0)return;var g=a.Empires.Governments[patron];
                g.SubjectPopulation=Math.Max(0,g.SubjectPopulation+population);g.SubjectAreaKm2=Math.Max(0,g.SubjectAreaKm2+area);g.SubjectArmy=Math.Max(0,g.SubjectArmy+army);g.SubjectCount=Math.Max(0,g.SubjectCount+count);
                // A forwarded delta is also in this actor's report, avoiding a
                // second payment when its own scheduled fiscal event arrives.
                if(g.Patron>=0){g.ReportedPopulation+=population;g.ReportedAreaKm2+=area;g.ReportedArmy+=army;}
                patron=g.Patron;
            }
        }
        void ChangeImperialHeight(PrehistoryState s,int patron,int remove,int add)
        {
            var a=s.History.Aggregates;
            for(int depth=0;patron>=0&&depth<HistoricalImperialHierarchy.MaximumDepth;depth++)
            {
                patron=a.Resolve(patron,s.History);if(patron<0)return;var g=a.Empires.Governments[patron];int previous=g.Height;
                if(remove>=0){if(g.ChildrenByHeight[remove]<=0)throw new InvalidOperationException("Missing imperial height delta.");g.ChildrenByHeight[remove]--;}
                if(add>=0)g.ChildrenByHeight[add]++;
                int next=0;for(int k=0;k<8;k++)if(g.ChildrenByHeight[k]>0)next=k+1;g.Height=next;
                if(next>=8)throw new InvalidOperationException("Imperial hierarchy depth budget exceeded.");
                if(next==previous)return;remove=previous;add=next;patron=HistoricalImperialHierarchy.Patron(s,patron);
            }
        }
        void ReportImperialGovernment(PrehistoryState s,int polity)
        {
            if(!TechnicalHistory(s))return;var h=s.History;var a=h.Aggregates;var g=a.Empires.Governments[polity];var p=h.Polities[polity];
            long population=p.Population+g.SubjectPopulation,area=p.AreaKm2+g.SubjectAreaKm2,army=p.Army+g.SubjectArmy;
            int patron=HistoricalImperialHierarchy.Patron(s,polity);
            if(patron>=0)ImperialDelta(s,patron,population-g.ReportedPopulation,area-g.ReportedAreaKm2,army-g.ReportedArmy,0);
            g.ReportedPopulation=population;g.ReportedAreaKm2=area;g.ReportedArmy=army;g.LastReportYear=s.Years;
        }
        bool SetImperialPatron(PrehistoryState s,int client,int patron,bool imposed,string reason)
        {
            var h=s.History;var a=h.Aggregates;if(!TechnicalHistory(s)||client==patron||h.Polities[client].EndedYear>=0)return false;
            int previous=HistoricalImperialHierarchy.Patron(s,client);if(previous==patron)return false;
            if(patron>=0)
            {
                if(h.Polities[patron].EndedYear>=0)return false;
                int ancestor=patron,parentDepth=0;
                for(int depth=0;depth<HistoricalImperialHierarchy.MaximumDepth;depth++)
                {if(ancestor==client)return false;ancestor=HistoricalImperialHierarchy.Patron(s,ancestor);if(ancestor<0)break;parentDepth++;}
                if(parentDepth+1+a.Empires.Governments[client].Height>=HistoricalImperialHierarchy.MaximumDepth)return false;
            }
            ReportImperialGovernment(s,client);var g=a.Empires.Governments[client];int count=1+g.SubjectCount;
            if(previous>=0){ImperialDelta(s,previous,-g.ReportedPopulation,-g.ReportedAreaKm2,-g.ReportedArmy,-count);ChangeImperialHeight(s,previous,g.Height,-1);}
            g.Patron=patron;g.SinceYear=s.Years;g.Imposed=imposed;g.ArrearsYears=0;g.AnnualTribute=0;g.AutonomyMilli=imposed?650:850;
            if(patron>=0)
            {
                ImperialDelta(s,patron,g.ReportedPopulation,g.ReportedAreaKm2,g.ReportedArmy,count);
                ChangeImperialHeight(s,patron,-1,g.Height);
                var relation=AggregateRelation(s,client,patron);relation.Trade=true;relation.Alliance=true;relation.WarSince=-1;relation.Trust=Math.Max(20,relation.Trust);
                g.LastPatronSuccession=h.Dynamics.States[patron].LastSuccession;
                if(imposed){var cycle=h.Dynamics.States[patron].Cycle;if(cycle!=null){cycle.ConquestDebt=Math.Min(1000,cycle.ConquestDebt+75+Math.Min(100,g.SubjectCount*10));cycle.LastConquestYear=s.Years;}}
            }
            var p=h.Polities[client];
            HistoryEvent(s,patron>=0?"imperial-allegiance":"imperial-independence",p.Name+(patron>=0?" becomes a constituent government of "+h.Polities[patron].Name+", retaining its local court and communities.":" resumes independent foreign policy.")+" "+reason,s.Society.Settlements[p.CapitalId].Cell,p.CultureId,client,patron>=0?patron:previous,category:ChronicleCategory.Politics);
            return true;
        }
        void EndImperialGovernment(PrehistoryState s,int polity)
        {
            if(!TechnicalHistory(s))return;var g=s.History.Aggregates.Empires.Governments[polity];int parent=HistoricalImperialHierarchy.Patron(s,polity);
            if(parent>=0){ImperialDelta(s,parent,-g.ReportedPopulation,-g.ReportedAreaKm2,-g.ReportedArmy,-1-g.SubjectCount);ChangeImperialHeight(s,parent,g.Height,-1);}
            g.Patron=-1;
        }
        void AdvanceImperialGovernment(PrehistoryState s,int polity,int elapsed)
        {
            if(!TechnicalHistory(s))return;var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];var g=a.Empires.Governments[polity];
            int parent=HistoricalImperialHierarchy.Patron(s,polity);
            if(g.Patron>=0&&parent<0){g.Patron=-1;g.SinceYear=s.Years;HistoryEvent(s,"imperial-independence",p.Name+" continues independently after its imperial court loses authority.",s.Society.Settlements[p.CapitalId].Cell,p.CultureId,polity,category:ChronicleCategory.Politics);}
            if(parent>=0)
            {
                var patron=h.Polities[parent];var account=a.Polities[polity];var leader=h.Dynamics.Leaders[h.Dynamics.States[polity].LeaderId];
                long due=Math.Max(1,(long)(account.Receipts*(g.Imposed?.18:.10)))*elapsed;
                long payment=Math.Min(p.Treasury,due);payment=Math.Min(payment,Math.Max(0,1000000000-patron.Treasury));p.Treasury-=payment;patron.Treasury+=payment;g.TotalTribute+=payment;g.AnnualTribute=payment/Math.Max(1,elapsed);
                g.ArrearsYears=Math.Max(0,Math.Min(100,g.ArrearsYears+(payment<due?elapsed:-elapsed)));
                double km=LivingTravelKm(s,s.Society.Settlements[patron.CapitalId].Cell,s.Society.Settlements[p.CapitalId].Cell,patron.CultureId);
                double reach=Math.Max(150,a.Polities[parent].ReachKm*3*(.5+.5*g.AutonomyMilli/1000.0));
                double neglect=D.Clamp(km/reach-1),weakness=D.Clamp((p.Army+g.SubjectArmy*.3)/(double)Math.Max(1,patron.Army+a.Empires.Governments[parent].SubjectArmy*.2)-.5);
                bool succession=h.Dynamics.States[parent].LastSuccession>g.LastPatronSuccession;g.LastPatronSuccession=h.Dynamics.States[parent].LastSuccession;
                double stress=.45*neglect+.3*g.ArrearsYears/100.0+.45*patron.Exhaustion/1000.0+.5*(1-patron.Cohesion/1000.0)+.4*weakness;
                if(succession)stress+=.35*D.Clamp((h.Dynamics.States[parent].Cycle?.ConquestDebt??0)/600.0)+.15;
                double risk=.00015+.025*Math.Pow(Math.Max(0,stress-.45),2)*(.4+leader.Ambition/1000.0);
                if(s.Years-g.SinceYear>35&&HistoryRoll("imperial-separation",polity,s.Years)<1-Math.Exp(-risk*elapsed))SetImperialPatron(s,polity,-1,false,neglect>.5?"Protection and regular communication have broken down.":succession?"A disputed succession gives local institutions room to assert sovereignty.":"Local elites reject obligations as the imperial balance weakens.");
                else if(p.CultureId!=patron.CultureId)LivingCultureContact(s,p.CultureId,patron.CultureId);
            }
            ReportImperialGovernment(s,polity);
        }
        bool TryImperialSubmission(PrehistoryState s,int patron,int client,bool imposed,int elapsed,int theater=-1)
        {
            if(!TechnicalHistory(s))return false;var h=s.History;var a=h.Aggregates;
            if(patron==client||h.Polities[patron].EndedYear>=0||h.Polities[client].EndedYear>=0)return false;
            int sovereign=HistoricalImperialHierarchy.Sovereign(s,patron);
            if(sovereign==HistoricalImperialHierarchy.Sovereign(s,client)||HistoricalImperialHierarchy.Patron(s,client)>=0)return false;
            var p=h.Polities[sovereign];var c=h.Polities[client];var leader=h.Dynamics.Leaders[h.Dynamics.States[sovereign].LeaderId];var relation=AggregateRelation(s,patron,client);
            double strength=(p.Army+a.Empires.Governments[sovereign].SubjectArmy*.3)*HistoricalModernEffects.Military(h.Cultures[p.CultureId],s.Years);
            double defense=Math.Max(1,(c.Army+a.Empires.Governments[client].SubjectArmy*.3)*HistoricalModernEffects.Military(h.Cultures[c.CultureId],s.Years));
            double ratio=strength/defense;if(ratio<(imposed?1.25:1.7)||p.Cohesion<450||p.Treasury<100)return false;
            double km=LivingTravelKm(s,s.Society.Settlements[h.Polities[patron].CapitalId].Cell,s.Society.Settlements[c.CapitalId].Cell,p.CultureId);
            double reach=Math.Max(200,a.Polities[patron].ReachKm*2.5);
            if(theater<0&&km>reach)return false;
            double chance=imposed?.12+.18*D.Clamp(ratio-1):.003*elapsed*(.3+leader.Diplomacy/1000.0)*D.Clamp(ratio-1)*(relation.Trust>20?1:.2);
            if(HistoryRoll(imposed?"imperial-defeated-submission":"imperial-protection",client,s.Years)>chance)return false;
            return SetImperialPatron(s,client,sovereign,imposed,imposed?"A military settlement secures tribute and external allegiance rather than abolishing the local government.":"A protection compact exchanges dues and military support for considerable local autonomy.");
        }
    }
}
