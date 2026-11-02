using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        public static bool Paleolithic(PrehistoryState s)=>s?.Paleolithic!=null;
        public PrehistoryState PreparePaleolithic(int origin=-1)
        {
            if(origin<0)origin=ChooseOrigin();if(origin<0||origin>=Patches.Length||Patches[origin].Island||Patches[origin].Capacity<25)throw new ArgumentException("Choose a viable ancestral land region.");
            var s=new PrehistoryState{Version="prehistory-7",Origin=origin,WorldHash=World.Stages.Last().Hash,Population=new long[Patches.Length],ArrivalYear=Enumerable.Repeat(-1,Patches.Length).ToArray(),Ancestry=new int[Patches.Length*8],Paleolithic=new PaleolithicState{Version="paleolithic-2"},Society=new SocietyState{Knowledge=new int[Patches.Length],CellCulture=Enumerable.Repeat(-1,World.Surface.Cells.Length).ToArray()}};
            s.HumanRanges=PrepareHumanRanges(s);s.Seal();return s;
        }
        PrehistoryState StartPaleolithic(PrehistoryState source)
        {
            Validate(source);if(source.Started)return source;var s=source.Copy();s.Started=true;
            var founder=NewPaleolithicCommunity(s,s.Origin,null);founder.Residents=Math.Min(2000,Math.Max(180,Patches[s.Origin].Capacity*.06));
            founder.Capacity=PaleolithicCapacity(s,founder);founder.Residents=Math.Min(founder.Residents,founder.Capacity);
            if(founder.Residents<25)throw new InvalidOperationException("The selected ancestral area cannot support a band under the starting climate.");
            s.Paleolithic.Communities=new[]{founder};s.ArrivalYear[s.Origin]=0;SetPaleolithicRates(s,0);PublishPaleolithic(s);s.Seal();return s;
        }
        PaleolithicCommunity NewPaleolithicCommunity(PrehistoryState s,int patch,PaleolithicCommunity parent)
        {
            var p=s.Paleolithic;int id=p.NextIdentity++;var c=new PaleolithicCommunity{Patch=patch,Identity=id,Parent=parent?.Identity??-1,FoundedYear=s.Years,NextSpeechChange=s.Years+1500+(int)(Rng.Value(World.Recipe.Seed,"paleo-speech-period",id)*1500),Language=s.Paleolithic.Version=="paleolithic-2"&&parent!=null?PaleolithicLanguage.Branch(id,s.Years,parent.Language):LanguageGenerator.Create(World.Recipe.Seed,id,s.Years,parent?.Language)};
            if(parent==null)
            {
                for(int k=0;k<8;k++)c.Knowledge[k]=.82+.16*Rng.Value(World.Recipe.Seed,"paleo-foundation-"+k,patch);
                c.Knowledge[(int)PaleolithicTechnique.HideWraps]=.10;c.Speech=new[]{.5,.4,.25,.15};
            }
            else{c.Knowledge=(double[])parent.Knowledge.Clone();c.Speech=(double[])parent.Speech.Clone();c.Discovery=(double[])parent.Discovery.Clone();}
            return c;
        }
        public PaleolithicClimate PaleolithicClimateAt(PrehistoryState s,int cell)
        {
            if(!Paleolithic(s))return new PaleolithicClimate(0,0,1,0);
            return PaleolithicRules.Climate(World.Recipe.Seed,s.Paleolithic.Seconds/(double)SimulationTime.Year,World.Surface.Cells[cell].Position.Z,World.ElevationM[cell],World.Ecology.Province[cell]);
        }
        double PaleolithicCapacity(PrehistoryState s,PaleolithicCommunity c,int destination=-1)
        {
            var patch=Patches[destination<0?c.Patch:destination];var h=World.Ecology.Habitat[patch.Cell];var climate=PaleolithicClimateAt(s,patch.Cell);
            double cold=h.ColdTenthsC/10.0+climate.TemperatureDeltaC;
            double protection=7*c.Knowledge[(int)PaleolithicTechnique.HideWraps]+10*c.Knowledge[(int)PaleolithicTechnique.FittedClothing]+12*c.Knowledge[(int)PaleolithicTechnique.InsulatedClothing]+5*c.Knowledge[(int)PaleolithicTechnique.FireTending];
            double winter=D.Clamp((cold+protection+6)/12,0,1),food=D.Clamp(climate.PrecipitationMultiplier,.4,1.2)*(1-climate.Ice*.28);
            double latitude=Math.Abs(World.Surface.Cells[patch.Cell].Position.Z);
            if(latitude>.55&&cold< -15)winter*=D.Clamp(1-climate.Ice*(latitude-.55)*3,0,1);
            double capacity=patch.Capacity*winter*food*(.7+.25*c.Knowledge[(int)PaleolithicTechnique.Hafting]+.15*c.Knowledge[(int)PaleolithicTechnique.FoodPreservation]);
            int target=destination<0?c.Patch:destination;double other=s.HumanRanges?.Lineages.Sum(l=>HumanRangeState.ResidentsAt(s,l,target))??0;
            return Math.Max(capacity*.1,capacity-.4*other);
        }
        internal long NextPaleolithicEvent(PrehistoryState s)=>s.Paleolithic.BoundarySeconds+PaleolithicRules.DecisionYears*SimulationTime.Year;
        internal PrehistoryState AdvancePaleolithic(PrehistoryState source,long seconds,int maximumEvents,CancellationToken token,bool validate,int maximumWork=int.MaxValue)
        {
            if(validate)Validate(source);if(!source.Started)throw new InvalidOperationException("Start the world first.");
            if(seconds<1||seconds>SimulationTime.Maximum-source.Paleolithic.Seconds||maximumEvents<1||maximumWork<1)throw new ArgumentOutOfRangeException(nameof(seconds));
            var s=source.Copy();long target=s.Paleolithic.Seconds+seconds;int events=0;
            while(NextPaleolithicEvent(s)<=target&&events<Math.Min(maximumEvents,maximumWork))
            {
                token.ThrowIfCancellationRequested();long next=NextPaleolithicEvent(s);s.Paleolithic.Seconds=next;s.Years=(int)(next/SimulationTime.Year);
                foreach(var c in s.Paleolithic.Communities)
                {
                    c.Residents=PaleolithicRules.Residents(s.Paleolithic,c);if(c.Residents<.5)c.Residents=0;
                    var learned=new double[PaleolithicRules.TechniqueCount];for(int k=0;k<learned.Length;k++)learned[k]=PaleolithicRules.Adoption(s.Paleolithic,c,k);
                    for(int k=0;k<learned.Length;k++)if(c.Knowledge[k]<.1&&learned[k]>=.1){var e=new ChronicleEvent{Year=s.Years,Cell=Patches[c.Patch].Cell,CultureId=c.Identity,Category=ChronicleCategory.Discoveries,Text=PaleolithicRules.Names[k]+" is becoming established among local bands."};s.Society.Events=s.Society.Events.Concat(new[]{e}).TakeLast(256).ToArray();}c.Knowledge=learned;
                    for(int k=0;k<4;k++)c.Speech[k]=PaleolithicRules.SpeechAt(s.Paleolithic,c,k);
                }
                MaterializeHumanRanges(s);s.Paleolithic.BoundarySeconds=next;
                foreach(var c in s.Paleolithic.Communities)if(c.Residents>=1)
                {
                    var h=World.Ecology.Habitat[Patches[c.Patch].Cell];var climate=PaleolithicClimateAt(s,Patches[c.Patch].Cell);HumanAppearanceEnvironment(c.Patch,out _,out double uv,out _);
                    for(int k=0;k<8;k++)s.Ancestry[c.Patch*8+k]=HistoryHumanVariation.Evolve(World.Recipe.Seed,c.Patch,s.Years,(HumanAppearanceTrait)k,s.Ancestry[c.Patch*8+k],50,(long)c.Residents,uv,h.MeanTenthsC+(int)(climate.TemperatureDeltaC*10),c.Knowledge[14]);
                }
                MigratePaleolithic(s,token);AdvanceHumanRanges(s);SetPaleolithicRates(s);events++;
            }
            s.Paleolithic.Seconds=NextPaleolithicEvent(s)<=target?Math.Min(target,NextPaleolithicEvent(s)-1):target;
            s.Years=(int)(s.Paleolithic.Seconds/SimulationTime.Year);PublishPaleolithic(s);if(events==0)s.DomainToken=source.DomainToken;token.ThrowIfCancellationRequested();s.Seal();return s;
        }
        void MigratePaleolithic(PrehistoryState s,CancellationToken token)
        {
            var communities=s.Paleolithic.Communities.ToDictionary(c=>c.Patch);var flux=new List<(PaleolithicCommunity from,int to,double number)>();var budgets=new Dictionary<int,double>();
            foreach(var r in Routes)
            {
                if((r.A&1023)==0)token.ThrowIfCancellationRequested();
                void Attempt(int from,int to)
                {
                    if(!communities.TryGetValue(from,out var donor)||donor.Residents<80)return;
                    var climate=PaleolithicClimateAt(s,Patches[from].Cell);if(!PaleolithicRules.RouteOpen(r,climate,donor.Knowledge[(int)PaleolithicTechnique.Rafts]))return;
                    communities.TryGetValue(to,out var recipient);
                    double capacity=PaleolithicCapacity(s,recipient??donor,to),present=recipient?.Residents??0;if(capacity<30||present>=capacity*.9)return;
                    double left=budgets.TryGetValue(from,out var b)?b:donor.Residents*.12;
                    double pressure=D.Clamp(donor.Residents/Math.Max(1,donor.Capacity),.12,2);
                    double travel=Math.Exp(-r.DistanceM/(r.Shallow?350000.0:180000.0));double number=Math.Min(left,donor.Residents*.025*pressure*travel);
                    if(present<1&&number<18)return;number=Math.Min(number,Math.Max(0,capacity-present)*.15);if(number<1)return;
                    budgets[from]=left-number;flux.Add((donor,to,number));
                }
                Attempt(r.A,r.B);Attempt(r.B,r.A);
            }
            foreach(var f in flux)
            {
                if(!communities.TryGetValue(f.to,out var receiver)){receiver=NewPaleolithicCommunity(s,f.to,f.from);communities.Add(f.to,receiver);s.ArrivalYear[f.to]=s.Years;for(int k=0;k<8;k++)s.Ancestry[f.to*8+k]=s.Ancestry[f.from.Patch*8+k];}
                double old=receiver.Residents,total=old+f.number;
                for(int k=0;k<PaleolithicRules.TechniqueCount;k++)receiver.Knowledge[k]=(receiver.Knowledge[k]*old+f.from.Knowledge[k]*f.number)/total;
                for(int k=0;k<4;k++)receiver.Speech[k]=(receiver.Speech[k]*old+f.from.Speech[k]*f.number)/total;
                for(int k=0;k<8;k++)s.Ancestry[f.to*8+k]=HistoryHumanVariation.MixValue(s.Ancestry[f.to*8+k],(long)old,s.Ancestry[f.from.Patch*8+k],(long)f.number);
                receiver.Residents=total;f.from.Residents-=f.number;
            }
            s.Paleolithic.Communities=communities.Values.OrderBy(c=>c.Patch).ToArray();
        }
        void SetPaleolithicRates(PrehistoryState s,int elapsed=PaleolithicRules.DecisionYears)
        {
            var byPatch=s.Paleolithic.Communities.ToDictionary(c=>c.Patch);var contacts=new Dictionary<int,List<PaleolithicCommunity>>();
            foreach(var r in Routes)if(byPatch.TryGetValue(r.A,out var a)&&byPatch.TryGetValue(r.B,out var b)&&a.Residents>=10&&b.Residents>=10&&PaleolithicRules.RouteOpen(r,PaleolithicClimateAt(s,Patches[r.A].Cell),Math.Min(a.Knowledge[20],b.Knowledge[20])))
            {if(!contacts.TryGetValue(r.A,out var ca))contacts[r.A]=ca=new List<PaleolithicCommunity>();if(!contacts.TryGetValue(r.B,out var cb))contacts[r.B]=cb=new List<PaleolithicCommunity>();if(!ca.Contains(b))ca.Add(b);if(!cb.Contains(a))cb.Add(a);}
            foreach(var c in s.Paleolithic.Communities)
            {
                c.Capacity=PaleolithicCapacity(s,c);c.GrowthRate=c.Residents>c.Capacity?.004:.0006;
                var peers=contacts.TryGetValue(c.Patch,out var list)?list:new List<PaleolithicCommunity>();
                c.Contacts=peers.Select(x=>x.Identity).OrderBy(x=>x).ToArray();
                for(int t=0;t<PaleolithicRules.TechniqueCount;t++)
                {
                    double prereq=PaleolithicRules.PrerequisiteStrength(c.Knowledge,t),donor=peers.Select(x=>x.Knowledge[t]).DefaultIfEmpty(0).Max();
                    double opportunity=.35+.65*D.Clamp(c.Residents/1200),pressure=(t==8||t==13||t==14)?1+D.Clamp(-World.Ecology.Habitat[Patches[c.Patch].Cell].ColdTenthsC/200.0):1;
                    if(prereq>.55&&c.Residents>=25)c.Discovery[t]+=elapsed*opportunity*pressure*(.5+.5*c.Speech[2]);
                    double threshold=18000+Rng.Value(World.Recipe.Seed,"paleo-discovery-"+t,c.Identity)*85000;
                    bool invented=t<8||c.Discovery[t]>=threshold;
                    double support=D.Clamp((prereq-.35)/.4),target=(invented?1:donor)*support;
                    if(c.Residents<15){target=0;c.Rates[t]=1/3000.0;}
                    else c.Rates[t]=target>c.Knowledge[t]?(1/6000.0+peers.Count/12000.0)*opportunity:1/20000.0;
                    c.Targets[t]=target;
                }
                for(int k=0;k<4;k++)c.SpeechRates[k]=c.Residents>=25?(1+Math.Min(4,peers.Count)*.15+peers.Select(x=>x.Speech[k]).DefaultIfEmpty(0).Max()*.5)/(55000.0+k*9000):0;
                if(s.Years>=c.NextSpeechChange&&c.Residents>=25)
                {
                    c.PreviousLanguage=c.Language.Copy();c.LanguageChangeSeconds=s.Paleolithic.Seconds;c.Language=s.Paleolithic.Version=="paleolithic-2"?PaleolithicLanguage.Evolve(c.Language,s.Years):LanguageGenerator.Evolve(c.Language,s.Years);c.NextSpeechChange=s.Years+1500+(int)(Rng.Value(World.Recipe.Seed,"paleo-dialect-"+c.Language.Revision,c.Identity)*1500);
                    var contact=peers.OrderByDescending(p=>p.Residents).FirstOrDefault();if(contact!=null){int concept=(c.Language.Revision%4);c.Language=LanguageGenerator.Borrow(c.Language,contact.Language,new[]{"water","fire","hide","spear"}[concept],s.Years);}
                }
            }
        }
        void PublishPaleolithic(PrehistoryState s)
        {
            Array.Clear(s.Population,0,s.Population.Length);Array.Fill(s.Society.CellCulture,-1);
            var cultures=new CultureIdentity[s.Paleolithic.NextIdentity];
            foreach(var c in s.Paleolithic.Communities)
            {
                long residents=(long)Math.Max(0,Math.Round(PaleolithicRules.Residents(s.Paleolithic,c)));s.Population[c.Patch]=residents;
                var patch=Patches[c.Patch];string name=LanguageGenerator.Capitalize(LanguageGenerator.Name(c.Language,LanguageNameKind.People,c.Identity).Text);
                cultures[c.Identity]=new CultureIdentity{Id=c.Identity,Name=name,Stage=SocietyStage.Foragers,FoundedYear=c.FoundedYear,OriginCell=patch.Cell,Population=residents,Hunting=500,Gathering=500,Subsistence="Mobile hunter-gatherers",Contacts=(int[])c.Contacts.Clone(),AreaKm2=(long)patch.Cells.Sum(i=>World.Surface.Cells[i].AreaM2/1e6)};
                if(residents>0)foreach(int cell in patch.Cells)s.Society.CellCulture[cell]=c.Identity;
                if(c.Parent<0&&s.Years==0)InitializePaleolithicAppearance(s,c.Patch);
                s.Society.Knowledge[c.Patch]=(int)(1000*c.Knowledge.Average());
            }
            s.Society.Cultures=cultures;
        }
        void InitializePaleolithicAppearance(PrehistoryState s,int patch)
        {
            var h=World.Ecology.Habitat[Patches[patch].Cell];HumanAppearanceEnvironment(patch,out _,out double uv,out _);
            for(int k=0;k<8;k++)s.Ancestry[patch*8+k]=HistoryHumanVariation.FounderValue(World.Recipe.Seed,patch,(HumanAppearanceTrait)k,uv,h.MeanTenthsC);
        }
        public PaleolithicBand PaleolithicBandAt(PrehistoryState s,int cell,int localBand=0)
        {
            if(!Paleolithic(s)||cell<0||cell>=CellPatch.Length||localBand<0)return null;int patch=CellPatch[cell];var c=s.Paleolithic.Communities.FirstOrDefault(x=>x.Patch==patch);if(c==null||PaleolithicRules.Residents(s.Paleolithic,c)<1)return null;
            // A coherent patch-level rank plus stable cell/band variation. Never
            // read frame time or call mutable RNG; visiting/loading cannot reroll.
            double coarse=Rng.Value(World.Recipe.Seed,"paleo-band-cohort",unchecked(patch*7919+localBand));
            double rank=.8*coarse+.2*Rng.Value(World.Recipe.Seed,"paleo-band-cell",unchecked(cell*397+localBand));
            // Convert the triangular mixture back to uniform CDF: adoption90%
            // really represents90% of a large set, without losing coherence.
            double q=rank<.2?rank*rank/.32:rank<=.8?(rank-.1)/.8:1-(1-rank)*(1-rank)/.32;
            double speechTransition=D.Clamp((s.Paleolithic.Seconds-c.LanguageChangeSeconds)/(1000.0*SimulationTime.Year));var language=c.PreviousLanguage!=null&&q>speechTransition?c.PreviousLanguage:c.Language;
            var dialect=LanguageGenerator.Create(World.Recipe.Seed,unchecked(cell*397+localBand)&int.MaxValue,c.FoundedYear,language);
            var band=new PaleolithicBand{Id=((long)cell<<32)|(uint)localBand,Patch=patch,Cell=cell,Community=c.Identity,Species=c.Species,Name=LanguageGenerator.Capitalize(LanguageGenerator.Name(language,LanguageNameKind.People,unchecked(cell*397+localBand)).Text),LanguageName=LanguageGenerator.Capitalize(LanguageGenerator.Word(dialect,"speech").Text),Language=dialect,Techniques=new bool[PaleolithicRules.TechniqueCount],Practice=new double[PaleolithicRules.TechniqueCount],Speech=new double[4]};
            for(int t=0;t<band.Techniques.Length;t++){double prevalence=PaleolithicRules.Adoption(s.Paleolithic,c,t);band.Techniques[t]=prevalence>=q;band.Practice[t]=D.Clamp(.5+4*(prevalence-q));}
            bool Available(int t)=>band.Techniques[t]&&PaleolithicRules.Prerequisites((PaleolithicTechnique)t).All(k=>Available(k));
            for(int t=0;t<band.Techniques.Length;t++)band.Techniques[t]=Available(t);
            for(int k=0;k<4;k++)band.Speech[k]=D.Clamp(.5+4*(PaleolithicRules.SpeechAt(s.Paleolithic,c,k)-q));
            band.Appearance=HistoryHumanVariation.Sample(HistoryHumanVariation.Read(s,patch,cell,"Paleolithic regional ancestry distribution"),World.Recipe.Seed,unchecked(cell*397+localBand));return band;
        }
        void ValidatePaleolithic(PrehistoryState s)
        {
            var p=s.Paleolithic;
            ValidateHumanRanges(s);
            if(s.Version!="prehistory-7"||p==null||(p.Version!="paleolithic-1"&&p.Version!="paleolithic-2")||s.Species!="Homo sapiens"||s.History!=null||s.Timeline!=null||s.WorldHash!=World.Stages.Last().Hash||s.Origin<0||s.Origin>=Patches.Length||p.StartYearsBeforePresent!=200000||p.NextIdentity<0||p.BoundarySeconds%(50*SimulationTime.Year)!=0||p.Seconds<0||p.Seconds>SimulationTime.Maximum||p.BoundarySeconds<0||p.BoundarySeconds>p.Seconds||p.Seconds-p.BoundarySeconds>=50*SimulationTime.Year||s.Years!=p.Seconds/SimulationTime.Year||s.Population?.Length!=Patches.Length||s.Ancestry?.Length!=Patches.Length*8||s.ArrivalYear?.Length!=Patches.Length||s.Society?.CellCulture?.Length!=World.Surface.Cells.Length||s.Society.Settlements.Length!=0||s.Society.Cultures.Length!=p.NextIdentity||p.Communities.Length!=p.NextIdentity||s.Hash!=s.ComputeHash())throw new ArgumentException("Invalid Paleolithic checkpoint.");
            var ids=new HashSet<int>();int previous=-1;
            foreach(var c in p.Communities)
            {
                bool Values(double[] a,int n)=>a!=null&&a.Length==n&&a.All(v=>double.IsFinite(v)&&v>=0&&v<=1);
                if(c.Patch<=previous||c.Patch>=Patches.Length||!ids.Add(c.Identity)||c.Identity<0||c.Identity>=p.NextIdentity||SapientSpecies.Find(c.Species)==null||!LanguageGenerator.Valid(c.Language)||c.PreviousLanguage!=null&&!LanguageGenerator.Valid(c.PreviousLanguage)||c.Contacts==null||c.Contacts.Any(i=>i<0||i>=p.NextIdentity||i==c.Identity)||c.LanguageChangeSeconds<0||c.LanguageChangeSeconds>p.Seconds||!double.IsFinite(c.Residents)||c.Residents<0||!double.IsFinite(c.Capacity)||c.Capacity<0||!double.IsFinite(c.GrowthRate)||c.GrowthRate<0||!Values(c.Knowledge,24)||!Values(c.Targets,24)||!Values(c.Speech,4)||c.Rates?.Length!=24||c.Rates.Any(r=>!double.IsFinite(r)||r<0)||c.Discovery?.Length!=24||c.Discovery.Any(r=>!double.IsFinite(r)||r<0)||c.SpeechRates?.Length!=4||c.SpeechRates.Any(r=>!double.IsFinite(r)||r<0))throw new ArgumentException("Invalid Paleolithic community.");previous=c.Patch;
            }
            if(s.Population.Any(n=>n<0)||s.Ancestry.Any(n=>n<0||n>10000)||s.Society.Cultures.Any(c=>c==null||c.Stage!=SocietyStage.Foragers||c.Farming!=0))throw new ArgumentException("Paleolithic state cannot contain later-age society.");
        }
    }
}
