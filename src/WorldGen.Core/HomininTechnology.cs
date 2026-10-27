using System;
using System.Collections.Generic;

namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        readonly struct LithicOpportunity
        {
            public readonly LithicKnowledge Skill,Requires;public readonly double Communication,Learning,Rate;public readonly string InnovationStream,LossStream;
            public LithicOpportunity(LithicKnowledge skill,LithicKnowledge requires,double communication,double learning,double rate)
            {Skill=skill;Requires=requires;Communication=communication;Learning=learning;Rate=rate;InnovationStream="lithic-innovation-"+(long)skill;LossStream="lithic-loss-"+(long)skill;}
        }
        static readonly LithicOpportunity[] lithicOpportunities={
            new LithicOpportunity(LithicKnowledge.Woodworking,LithicKnowledge.Flakes,.15,.2,.000035),
            new LithicOpportunity(LithicKnowledge.Handaxes,LithicKnowledge.Flakes,.25,.3,.000012),
            new LithicOpportunity(LithicKnowledge.FireKeeping,LithicKnowledge.Woodworking,.3,.35,.000018),
            new LithicOpportunity(LithicKnowledge.Spears,LithicKnowledge.Woodworking,.3,.35,.00003),
            new LithicOpportunity(LithicKnowledge.HideWorking,LithicKnowledge.Flakes,.35,.4,.000025),
            new LithicOpportunity(LithicKnowledge.FireMaking,LithicKnowledge.FireKeeping,.45,.5,.000012),
            new LithicOpportunity(LithicKnowledge.Hafting,LithicKnowledge.Woodworking|LithicKnowledge.Spears,.45,.5,.000018),
            new LithicOpportunity(LithicKnowledge.PreparedCores,LithicKnowledge.Handaxes,.5,.55,.00002),
            new LithicOpportunity(LithicKnowledge.Adhesives,LithicKnowledge.FireKeeping|LithicKnowledge.Hafting,.5,.55,.000015),
            new LithicOpportunity(LithicKnowledge.Blades,LithicKnowledge.PreparedCores,.6,.65,.000025),
            new LithicOpportunity(LithicKnowledge.BoneTools,LithicKnowledge.HideWorking,.6,.65,.000025),
            new LithicOpportunity(LithicKnowledge.Fishing,LithicKnowledge.Spears,.5,.55,.00002),
            new LithicOpportunity(LithicKnowledge.Watercraft,LithicKnowledge.Woodworking|LithicKnowledge.Hafting,.6,.65,.000012),
            new LithicOpportunity(LithicKnowledge.Projectiles,LithicKnowledge.Hafting|LithicKnowledge.Adhesives,.65,.7,.00002),
            new LithicOpportunity(LithicKnowledge.Needles,LithicKnowledge.BoneTools|LithicKnowledge.HideWorking,.68,.72,.000025),
            new LithicOpportunity(LithicKnowledge.Symbols,LithicKnowledge.None,.65,.7,.000025),
            new LithicOpportunity(LithicKnowledge.FoodStorage,LithicKnowledge.FireKeeping|LithicKnowledge.HideWorking,.65,.7,.00002),
            new LithicOpportunity(LithicKnowledge.Microliths,LithicKnowledge.Blades|LithicKnowledge.Adhesives,.7,.75,.00003),
            new LithicOpportunity(LithicKnowledge.PlantManagement,LithicKnowledge.FoodStorage,.72,.75,.000015),
            new LithicOpportunity(LithicKnowledge.Cultivation,LithicKnowledge.PlantManagement,.815,.78,.000025),
            new LithicOpportunity(LithicKnowledge.Herding,LithicKnowledge.FoodStorage,.815,.78,.000018),
            new LithicOpportunity(LithicKnowledge.Pottery,LithicKnowledge.FireMaking|LithicKnowledge.FoodStorage,.75,.78,.000016),
            new LithicOpportunity(LithicKnowledge.Copper,LithicKnowledge.FireMaking|LithicKnowledge.Pottery,.8,.82,.00008),
            new LithicOpportunity(LithicKnowledge.MetalSmelting,LithicKnowledge.Copper|LithicKnowledge.FireMaking|LithicKnowledge.Pottery,.8,.84,.00006),
            new LithicOpportunity(LithicKnowledge.Bronze,LithicKnowledge.Copper|LithicKnowledge.MetalSmelting,.8,.84,.00006)
        };
        void AdvanceLithicTradition(PrehistoryState s,HomininTradition tradition,HomininPopulation representative,double people,double communication,double learning,int dt,List<HomininEvent> events)
        {
            var h=s.DeepPast;int bp=h.YearsBeforePresent(s.Years),seed=World.Recipe.Seed;var patch=Patches[representative.Region];var habitat=World.Ecology.Habitat[patch.Cell];
            double latitude=Math.Asin(World.Surface.Cells[patch.Cell].Position.Z)*180/Math.PI;
            var climate=PleistoceneClimate.At(seed,bp,latitude,0,World.ElevationM[patch.Cell]);
            double density=people/Math.Max(1,patch.Capacity),productive=SurvivalMath.Clamp(habitat.ProductivityGCM2Year/600d),water=SurvivalMath.Clamp(habitat.AridityMilli/650d);
            double suitability=productive*water*SurvivalMath.Clamp((habitat.GrowingDays-70)/150d)*(1-climate.Glacial*.8);
            double sedentaryTarget=SurvivalMath.Clamp((productive+tradition.Cultivation*2+tradition.Herding- .7)*.6);
            if(h.RulesVersion>=3)
            {
                bool fishing=(tradition.Knowledge&LithicKnowledge.Fishing)!=0;
                double concentrated=fishing&&(patch.Lifestyle==ForagerLifestyle.Coastal||patch.Lifestyle==ForagerLifestyle.Wetland)?water*.25:0;
                double storage=(tradition.Knowledge&LithicKnowledge.FoodStorage)!=0?.12:0;
                double tending=(tradition.Knowledge&LithicKnowledge.PlantManagement)!=0?.1:0;
                // Recurrent camps can precede cultivation. Otherwise the old
                // dependency made low-NPP annual-plant habitats unable to settle
                // until they had already farmed, while farming required settling.
                sedentaryTarget=SurvivalMath.Clamp((productive-.4)*.5+concentrated+storage+tending+SurvivalMath.Clamp(density-.5)*.2+tradition.Cultivation*.8+tradition.Herding*.4,0,.95);
            }
            tradition.Sedentism+=(sedentaryTarget-tradition.Sedentism)*(1-Math.Exp(-dt/1500d));
            foreach(var opportunity in lithicOpportunities)
            {
                if(h.RulesVersion<3&&opportunity.Skill==LithicKnowledge.MetalSmelting)continue;
                var requires=h.RulesVersion<3&&opportunity.Skill==LithicKnowledge.Bronze?LithicKnowledge.Copper:
                    h.RulesVersion>=3&&opportunity.Skill==LithicKnowledge.Copper?LithicKnowledge.Flakes:opportunity.Requires;
                double communicationNeeded=h.RulesVersion>=3&&(opportunity.Skill==LithicKnowledge.Cultivation||opportunity.Skill==LithicKnowledge.Herding)?Math.Min(.78,opportunity.Communication):opportunity.Communication;
                if((tradition.Knowledge&opportunity.Skill)!=0||(tradition.Knowledge&requires)!=requires||communication<communicationNeeded||learning<opportunity.Learning)continue;
                double special=1;
                if(opportunity.Skill==LithicKnowledge.Fishing&&patch.Lifestyle!=ForagerLifestyle.Coastal&&patch.Lifestyle!=ForagerLifestyle.Wetland&&habitat.FloodDaysPerYear<10)special=.1;
                if(opportunity.Skill==LithicKnowledge.Watercraft&&patch.Lifestyle!=ForagerLifestyle.Coastal&&patch.Lifestyle!=ForagerLifestyle.Wetland)special=.15;
                if(opportunity.Skill==LithicKnowledge.Cultivation)special=suitability*tradition.Sedentism*SurvivalMath.Clamp(density,.1,1.5);
                if(opportunity.Skill==LithicKnowledge.Herding)special=(patch.Lifestyle==ForagerLifestyle.Grassland||patch.Lifestyle==ForagerLifestyle.Highland?.8:.15)*(1-climate.Glacial*.5);
                if(opportunity.Skill==LithicKnowledge.Copper||opportunity.Skill==LithicKnowledge.MetalSmelting||opportunity.Skill==LithicKnowledge.Bronze)special=tradition.Sedentism*(tradition.Cultivation+tradition.Herding)*.5;
                if(h.RulesVersion>=3&&opportunity.Skill==LithicKnowledge.Copper)special=.08+.5*tradition.Sedentism;
                double network=SurvivalMath.Clamp(Math.Log10(Math.Max(10,people))/4,.15,1.7)*(1+tradition.Network);
                double chronology=h.RulesVersion>=3?HomininChronology.Weight(opportunity.Skill,bp,h.EarthLikeTrajectory):1;
                double chance=1-Math.Exp(-dt*opportunity.Rate*network*tradition.Retention*special*chronology);
                int rollId=unchecked(tradition.Id*7919+s.Years);
                double roll=h.RulesVersion>=3?Rng.OpenValue(seed,opportunity.InnovationStream,rollId):Rng.Value(seed,opportunity.InnovationStream,rollId);
                if(roll>=chance)continue;
                tradition.Knowledge|=opportunity.Skill;
                events.Add(new HomininEvent{Year=s.Years,Region=representative.Region,Lineage=representative.Lineage,Tradition=tradition.Id,Kind="technology",Text=tradition.Name+" established a learned "+LithicSkillName(opportunity.Skill,h.RulesVersion<3)+" practice."});
            }
            if((tradition.Knowledge&LithicKnowledge.Cultivation)!=0)tradition.Cultivation=SurvivalMath.Clamp(tradition.Cultivation+(suitability*.7-tradition.Cultivation)*(1-Math.Exp(-dt/1500d)));
            if((tradition.Knowledge&LithicKnowledge.Herding)!=0)tradition.Herding=SurvivalMath.Clamp(tradition.Herding+(.6-tradition.Herding)*(1-Math.Exp(-dt/2000d)));
            if(people<35&&tradition.Network<.1)
            {
                foreach(var opportunity in lithicOpportunities)
                    if((tradition.Knowledge&opportunity.Skill)!=0&&opportunity.Communication>.4&&(h.RulesVersion>=3?Rng.OpenValue(seed,opportunity.LossStream,unchecked(tradition.Id*397+s.Years)):Rng.Value(seed,opportunity.LossStream,unchecked(tradition.Id*397+s.Years)))<1-Math.Exp(-dt*.00002*(1-tradition.Retention)))
                    {tradition.Knowledge&=~opportunity.Skill;events.Add(new HomininEvent{Year=s.Years,Region=representative.Region,Tradition=tradition.Id,Lineage=representative.Lineage,Kind="knowledge-loss",Text=tradition.Name+" lost its last practitioners of "+LithicSkillName(opportunity.Skill,h.RulesVersion<3)+"."});}
            }
        }
        public static string LithicSkillName(LithicKnowledge skill)
            =>LithicSkillName(skill,false);
        static string LithicSkillName(LithicKnowledge skill,bool legacyLabels)
        {
            if(legacyLabels&&skill==LithicKnowledge.Copper)return "copper";
            switch(skill){case LithicKnowledge.Flakes:return "stone flaking";case LithicKnowledge.FireKeeping:return "fire maintenance";case LithicKnowledge.FireMaking:return "fire ignition";case LithicKnowledge.PreparedCores:return "prepared-core knapping";case LithicKnowledge.HideWorking:return "hide working";case LithicKnowledge.BoneTools:return "bone-tool manufacture";case LithicKnowledge.FoodStorage:return "food storage";case LithicKnowledge.PlantManagement:return "wild-plant management";case LithicKnowledge.Symbols:return "symbolic communication";case LithicKnowledge.Copper:return "native copper working";case LithicKnowledge.MetalSmelting:return "extractive metal smelting";default:return skill.ToString().ToLowerInvariant();}
        }
    }
}
