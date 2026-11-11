using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        public int ChooseForagerOrigin(int variant=0)
        {
            int best=-1;double score=double.NegativeInfinity;
            for(int patch=0;patch<Patches.Length;patch++)
            {
                var p=Patches[patch];if(p.Island||p.Capacity<200)continue;int cell=p.Cell;var h=World.Ecology.Habitat[cell];var point=World.Surface.Cells[cell].Position;
                var change=PaleolithicRules.Climate(World.Recipe.Seed,0,point.Z,World.ElevationM[cell],World.Ecology.Province[cell]);
                double winter=h.ColdTenthsC/10d+change.TemperatureDeltaC,mean=h.MeanTenthsC/10d+change.TemperatureDeltaC,aridity=h.AridityMilli/1000d*change.PrecipitationMultiplier;
                double warm=ForagerEcology.Sigmoid((winter+1)/3)*D.Clamp((mean-7)/12)*D.Clamp((39-mean)/10);
                double water=D.Clamp(aridity/.7+HistoricalRiverAccess(cell)*2),productive=D.Clamp(h.ProductivityGCM2Year/650d);
                int neighbours=World.Surface.Cells[cell].Neighbors.Where(n=>!World.Hydrology.OceanConnected[n]).Select(n=>World.Ecology.Habitat[n].Biome).Distinct().Count();
                double mosaic=.7+.12*Math.Min(4,neighbours),seasonal=.7+.3*D.Clamp(1-Math.Abs(aridity-1)/1.8);
                double quality=warm*water*(.25+.75*productive)*mosaic*seasonal/(1+h.ReliefM/3000d);
                if(World.Recipe.TerrainSource=="Earth")
                {double lon=Math.Atan2(point.Y,point.X)*180/Math.PI,lat=Math.Asin(point.Z)*180/Math.PI;quality*=lon>-20&&lon<55&&lat>-35&&lat<30?3:1;}
                quality*=.82+.36*Rng.Value(World.Recipe.Seed,"forager-ancestral-mosaic",unchecked(patch+variant*104729));
                if(quality>score){score=quality;best=patch;}
            }
            return best;
        }
        internal (double[] costs,int[] previous) ForagerDistances(ForagerState state,int origin,bool ancestral,bool allowSea=false)
        {
            var dist=Enumerable.Repeat(double.PositiveInfinity,state.Districts.Length).ToArray();var previous=Enumerable.Repeat(-1,dist.Length).ToArray();
            var heap=new Heap();dist[origin]=0;heap.Push(0,origin);
            while(heap.Count>0)
            {
                var at=heap.Pop();if(at.Key>dist[at.Id]+.001)continue;var district=state.Districts[at.Id];
                foreach(var pass in district.Passages)
                {
                    int water=pass.WaterDistanceM>0?pass.WaterDistanceM:pass.DistanceM;
                    if(pass.Sea&&!(ancestral&&pass.Shelf&&pass.SeaFallM<=125)&&!(allowSea&&!pass.Shelf&&water<=25000))continue;
                    double penalty=1+2.5*pass.MountainBarrier+1.3*pass.WaterCost+(pass.Sea&&!pass.Shelf?20:0);
                    double candidate=Math.Ceiling(at.Key+pass.DistanceM*penalty);if(candidate>=dist[pass.To]||candidate>25000000)continue;
                    dist[pass.To]=candidate;previous[pass.To]=at.Id;heap.Push((int)Math.Ceiling(candidate),pass.To);
                }
            }
            return(dist,previous);
        }
        internal HashSet<int> SeedForagerLineages(ForagerState state,int originDistrict)
        {
            var ancestral=ForagerDistances(state,originDistrict,true,true);var available=state.Districts.Where(d=>double.IsFinite(ancestral.costs[d.Id])&&d.Productivity>40&&d.SummerC>12&&d.Water>.12).ToArray();
            var warm=new HashSet<int>(available.Where(d=>ancestral.costs[d.Id]<3500000&&d.WinterC> -2&&d.MeanC>10&&d.MeanC<30).Select(d=>d.Id));warm.Add(originDistrict);
            var sapiens=new HashSet<int>{originDistrict};var frontier=new Queue<int>();frontier.Enqueue(originDistrict);
            while(frontier.Count>0){var d=state.Districts[frontier.Dequeue()];foreach(var p in d.Passages)if(!p.Sea&&warm.Contains(p.To)&&sapiens.Add(p.To))frontier.Enqueue(p.To);}
            var centres=new List<int>{originDistrict};var distances=new List<double[]>{ancestral.costs};
            double area=available.Sum(d=>d.AreaKm2);int desired=Math.Max(2,Math.Min(8,2+(int)(area/10000000)));
            for(int i=1;i<desired;i++)
            {
                int next=-1;double best=0;
                foreach(var d in available)
                {
                    if(sapiens.Contains(d.Id))continue;
                    double nearest=distances.Min(x=>x[d.Id]);if(!double.IsFinite(nearest)||nearest<1800000)continue;
                    double environment=D.Clamp((d.WinterC+24)/25)*D.Clamp((d.SummerC-8)/15)*D.Clamp(d.Productivity/250.0);
                    double value=nearest*Math.Sqrt(d.AreaKm2)*environment*(.8+.4*Rng.Value(World.Recipe.Seed,"hominin-dispersal-centre",d.Id));
                    if(value>best){best=value;next=d.Id;}
                }
                if(next<0)break;centres.Add(next);distances.Add(ForagerDistances(state,next,true,true).costs);
            }
            var lineages=new List<HomininLineage>();
            for(int i=0;i<centres.Count;i++)
            {
                var d=state.Districts[centres[i]];int parent=-1;
                if(i>1){double best=double.PositiveInfinity;for(int j=1;j<i;j++)if(distances[j][d.Id]<best){best=distances[j][d.Id];parent=j;}}
                var ancestor=parent>=0?lineages[parent]:null;
                double Noise(string trait)=>Rng.Value(World.Recipe.Seed,"hominin-"+trait,d.Id)-.5;
                double compact=D.Clamp(.5+(12-d.MeanC)/55+Noise("body")*.2);
                double islandArea=state.Districts.Where(x=>x.Realm==d.Realm).Sum(x=>x.AreaKm2);
                bool smallIsolated=i>0&&islandArea<50000&&ancestral.costs[d.Id]>1800000&&d.MeanC>12;
                double island=smallIsolated?.48+.16*Rng.Value(World.Recipe.Seed,"lineage-island-body",d.Id):1;
                var profile=LanguageGenerator.Create(World.Recipe.Seed^0x47213,i,0);
                string root=LanguageGenerator.Word(profile,"land").Text.Replace(" ","").ToLowerInvariant();
                var l=new HomininLineage{Id=i,Parent=parent,OriginDistrict=d.Id,AncestorOrigin=originDistrict,PlayerSpecies=i==0,Sapient=i==0,
                    CulturalLearning=i==0?1:.72+.22*Rng.Value(World.Recipe.Seed,"lineage-cultural-learning",d.Id),
                    SymbolicPotential=i==0?1:.38+.42*Rng.Value(World.Recipe.Seed,"lineage-symbolic-potential",d.Id),
                    Name=i==0?"Homo sapiens":"Homo "+root+"ensis",ScientificName=i==0?"Homo sapiens":"Homo "+root+"ensis",
                    SeparationYears=i==0?300000:Math.Max(180000,(int)(350000+ancestral.costs[d.Id]/35+100000*Noise("isolation"))),
                    Compactness=compact,AdultMassKg=D.Q((ancestor?.AdultMassKg??(58+18*compact))+9*Noise("mass"),1000)*island,
                    StatureM=D.Q((ancestor?.StatureM??1.67)+.18*Noise("stature")-.08*(compact-.5),1000)*Math.Sqrt(island),
                    LimbRatio=D.Clamp((ancestor?.LimbRatio??(.65-.3*compact))+.18*Noise("limbs")),FaceProjection=D.Clamp((ancestor?.FaceProjection??.6)+.3*Noise("face")),
                    BrowRidge=D.Clamp((ancestor?.BrowRidge??.65)+.3*Noise("brow")),JawBreadth=D.Clamp((ancestor?.JawBreadth??.6)+.3*Noise("jaw")),NoseForm=D.Clamp(.45+.3*compact+.2*Noise("nose")),
                    GenerationYears=D.Q(25+5*Rng.Value(World.Recipe.Seed,"hominin-generation",d.Id),1000)};
                if(i==0){l.AdultMassKg=65;l.StatureM=1.7;l.FaceProjection=.3;l.BrowRidge=.25;l.JawBreadth=.45;}
                if(ancestor!=null)l.SeparationYears=Math.Max(50000,Math.Min(l.SeparationYears,ancestor.SeparationYears-30000));
                var route=new List<int>();var path=ancestor==null?ancestral.previous:ForagerDistances(state,ancestor.OriginDistrict,true,true).previous;
                for(int at=d.Id;at>=0;at=path[at])route.Add(at);route.Reverse();
                if(ancestor!=null)route=ancestor.AncestralRoute.Concat(route.Skip(1)).ToList();l.AncestralRoute=route.ToArray();
                for(int k=0;k<8;k++)l.Ancestry[k]=D.Q(D.Clamp((ancestor?.Ancestry[k]??.5)+Noise("ancestry-"+k)*.4),1000000);
                l.Basis="Procedural descendant of a connected ancestral Homo dispersal; "+l.SeparationYears.ToString("N0")+" years of regional separation. Traits reflect ancestry, isolation and ecology, not a universal thermal category.";
                lineages.Add(l);
            }
            state.Lineages=lineages.ToArray();return sapiens;
        }
        internal ForagerCulture NewForagerCulture(ForagerState state,int district,int year,ForagerCulture parent=null)
        {
            int id=state.NextCulture++;var d=state.Districts[district];
            double Trait(string field,double value)=>D.Q(D.Clamp(value+(Rng.Value(World.Recipe.Seed,"forager-culture-"+field,id)-.5)*(parent==null?.6:.2)),1000000);
            var c=new ForagerCulture{Id=id,Parent=parent?.Id??-1,OriginDistrict=district,FoundedYear=year,Seed=unchecked(World.Recipe.Seed^id*7919),Name="",
                FoodSharing=Trait("sharing",parent?.FoodSharing??.8),GuestAccess=Trait("access",parent?.GuestAccess??.65),KinObligation=Trait("kin",parent?.KinObligation??.7),ExchangeOpenness=Trait("exchange",parent?.ExchangeOpenness??.6),PrestigeCompetition=Trait("prestige",parent?.PrestigeCompetition??.35),ConflictAvoidance=Trait("avoidance",parent?.ConflictAvoidance??.7),MarriageMobility=Trait("marriage",parent?.MarriageMobility??.7),ChildcareCooperation=Trait("childcare",parent?.ChildcareCooperation??.8),TechniquePreference=new double[ForagerTechnology.Count]};
            for(int k=0;k<5;k++)c.FoodPreference[k]=Trait("food-"+k,parent?.FoodPreference[k]??D.Clamp(d.Food[k]));
            for(int k=0;k<c.TechniquePreference.Length;k++)c.TechniquePreference[k]=Trait("practice-"+k,parent?.TechniquePreference[k]??.55);
            c.Residence=parent?.Residence??(d.Coast+d.River>.8?ForagerResidence.SeasonalBaseCamps:d.GrowingDays<180?ForagerResidence.CollectorCamps:ForagerResidence.ResidentialMobility);
            state.Cultures=state.Cultures.Concat(new[]{c}).ToArray();return c;
        }
        internal ForagerCohort NewForagerCohort(ForagerState state,int district,int lineage,int year,ForagerCohort parent=null)
        {
            var d=state.Districts[district];var culture=NewForagerCulture(state,district,year,parent==null?null:state.Cultures[parent.Culture]);
            ForagerLanguage speech;
            if(parent==null)
            {speech=ForagerLanguages.Create(World.Recipe.Seed,state.NextLanguage++,district,year);state.Languages=state.Languages.Concat(new[]{speech}).ToArray();}
            else
            {
                speech=ForagerLanguages.Create(World.Recipe.Seed,state.NextLanguage++,district,year,state.Languages[parent.Language]);
                state.Languages=state.Languages.Concat(new[]{speech}).ToArray();
            }
            culture.Name=LanguageGenerator.Capitalize(LanguageGenerator.WordWithRootSeed(speech.Sounds,"people",culture.Seed).Text)+" "+ForagerLanguages.Word(speech,d.Coast>.5?"sea":d.River>.4?"river":d.Relief>800?"mountain":d.Canopy>.5?"tree":"grass").Text;
            var c=new ForagerCohort{Id=state.NextCohort++,District=district,Lineage=lineage,Culture=culture.Id,Language=speech.Id,Belief=parent?.Belief??-1,Parent=-1,FoundedYear=year,LastMigrationYear=year,LastContactYear=year,LastLanguageReviewYear=year,
                Knowledge=new double[ForagerTechnology.Count],Skill=new double[ForagerTechnology.Count],Use=new double[ForagerTechnology.Count],Teaching=new double[ForagerTechnology.Count],Innovation=new double[ForagerTechnology.Count],Admixture=new double[state.Lineages.Length],HomeCell=d.Cell,FrontierCell=d.Cell,Occupation=.08,FrontierKm=10,Health=.75,FoodSecurity=1};
            c.Speech=parent==null?ForagerAffiliations.Single(speech.Id):ForagerAffiliations.Replace(parent.Speech,parent.Language,speech.Id);
            c.Traditions=parent==null?Array.Empty<ForagerAffiliation>():parent.Traditions.Select(x=>x.Copy()).ToArray();
            c.Admixture[lineage]=1;
            if(parent==null)
            {
                int identity=state.NextPeople++;c.Identity=identity;state.Peoples=state.Peoples.Concat(new[]{new ForagerPeopleIdentity{Id=identity,OriginDistrict=district,FoundedYear=year,Name=culture.Name,HeritageStory="Shared kin and remembered places of the ancestral camps."}}).ToArray();
            }
            else c.Identity=parent.Identity;
            if(parent!=null)
            {c.Knowledge=(double[])parent.Knowledge.Clone();c.Skill=(double[])parent.Skill.Clone();c.Use=(double[])parent.Use.Clone();c.Appearance=(double[])HumanPhenotypes.Complete(parent.Appearance,World.Recipe.Seed,state.Lineages[parent.Lineage],parent.District).Clone();c.Admixture=(double[])parent.Admixture.Clone();}
            else
            {
                var environment=ForagerEcology.At(World.Recipe.Seed,d,0,World.ElevationM[d.Cell]);
                for(int k=0;k<ForagerTechnology.Count;k++)
                {
                    if(k<8||k==8||k==11||k==27||k==28||k==33||k==34||k==35||k==42)
                    {
                        double local=Rng.Value(World.Recipe.Seed,"forager-specialisation-"+k,c.Id),tradition=Rng.Value(World.Recipe.Seed,"ancestral-technical-tradition-"+k,lineage*7919+d.Province);
                        double inherited=k<8?.65+.3*Rng.Value(World.Recipe.Seed,"forager-inherited-"+k,c.Id):k==8?D.Clamp((12-environment.WinterC)/25)*.85:k==27?.55:k==33?.25+.55*tradition:k==34?.35+.45*local:k==35?.2+.55*ForagerTechnology.Demand(d,environment,c,k):k==42?.1+.55*tradition:.15+.35*local;
                        if(k==1)inherited=.25+.5*tradition+.15*local;
                        c.Knowledge[k]=inherited;c.Skill[k]=inherited*.9;c.Use[k]=c.Skill[k]*ForagerTechnology.Demand(d,environment,c,k)*ForagerTechnology.Materials(d,environment,k);
                    }
                }
                double uv=HistoryHumanVariation.UvExposure(d.Center.Z,World.ElevationM[d.Cell],(int)Math.Round(d.AnnualRainMm), (int)(d.Canopy*1000));
                for(int k=0;k<HumanPhenotypes.Count;k++)c.Appearance[k]=k<8?HistoryHumanVariation.FounderValue(World.Recipe.Seed,d.Id,(HumanAppearanceTrait)k,uv,(int)(d.MeanC*10))/10000.0:HumanPhenotypes.Founder(World.Recipe.Seed,state.Lineages[lineage],d.Id,k);
            }
            c.FirstKnownYear=c.Knowledge.Select(v=>v>=.2?year:-1).ToArray();c.FirstSkilledYear=c.Skill.Select(v=>v>=.5?year:-1).ToArray();c.LastLossYear=Enumerable.Repeat(-1,ForagerTechnology.Count).ToArray();
            c.TeachingFrom=new double[ForagerTechnology.Count*2];
            if(parent!=null)
            {
                var belief=ForagerBeliefs.Create(World.Recipe.Seed,state.NextBelief++,district,year,d,speech,state.Beliefs[parent.Belief]);
                state.Beliefs=state.Beliefs.Concat(new[]{belief}).ToArray();c.Traditions=ForagerAffiliations.Replace(c.Traditions,parent.Belief,belief.Id);ForagerAffiliations.Refresh(c);
            }
            return c;
        }
    }
}
