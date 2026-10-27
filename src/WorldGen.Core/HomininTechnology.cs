using System;
using System.Collections.Generic;

namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        readonly struct LithicOpportunity
        {
            public readonly LithicKnowledge Skill,Requires;public readonly double Communication,Learning,Rate;
            public LithicOpportunity(LithicKnowledge skill,LithicKnowledge requires,double communication,double learning,double rate)
            {Skill=skill;Requires=requires;Communication=communication;Learning=learning;Rate=rate;}
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
            new LithicOpportunity(LithicKnowledge.Bronze,LithicKnowledge.Copper,.8,.84,.00006)
        };
        void AdvanceLithicTradition(PrehistoryState s,HomininTradition tradition,HomininPopulation representative,double people,double communication,double learning,int dt,List<HomininEvent> events)
        {
            var h=s.DeepPast;int bp=h.YearsBeforePresent(s.Years),seed=World.Recipe.Seed;var patch=Patches[representative.Region];var habitat=World.Ecology.Habitat[patch.Cell];
            double latitude=Math.Asin(World.Surface.Cells[patch.Cell].Position.Z)*180/Math.PI;
            var climate=PleistoceneClimate.At(seed,bp,latitude,0,World.ElevationM[patch.Cell]);
            double density=people/Math.Max(1,patch.Capacity),productive=SurvivalMath.Clamp(habitat.ProductivityGCM2Year/600d),water=SurvivalMath.Clamp(habitat.AridityMilli/650d);
            double suitability=productive*water*SurvivalMath.Clamp((habitat.GrowingDays-70)/150d)*(1-climate.Glacial*.8);
            double sedentaryTarget=SurvivalMath.Clamp((productive+tradition.Cultivation*2+tradition.Herding- .7)*.6);
            tradition.Sedentism+=(sedentaryTarget-tradition.Sedentism)*(1-Math.Exp(-dt/1500d));
            foreach(var opportunity in lithicOpportunities)
            {
                if((tradition.Knowledge&opportunity.Skill)!=0||(tradition.Knowledge&opportunity.Requires)!=opportunity.Requires||communication<opportunity.Communication||learning<opportunity.Learning)continue;
                double special=1;
                if(opportunity.Skill==LithicKnowledge.Fishing&&patch.Lifestyle!=ForagerLifestyle.Coastal&&patch.Lifestyle!=ForagerLifestyle.Wetland&&habitat.FloodDaysPerYear<10)special=.1;
                if(opportunity.Skill==LithicKnowledge.Watercraft&&patch.Lifestyle!=ForagerLifestyle.Coastal&&patch.Lifestyle!=ForagerLifestyle.Wetland)special=.15;
                if(opportunity.Skill==LithicKnowledge.Cultivation)special=suitability*tradition.Sedentism*SurvivalMath.Clamp(density,.1,1.5);
                if(opportunity.Skill==LithicKnowledge.Herding)special=(patch.Lifestyle==ForagerLifestyle.Grassland||patch.Lifestyle==ForagerLifestyle.Highland?.8:.15)*(1-climate.Glacial*.5);
                if(opportunity.Skill==LithicKnowledge.Copper||opportunity.Skill==LithicKnowledge.Bronze)special=tradition.Sedentism*(tradition.Cultivation+tradition.Herding)*.5;
                double network=SurvivalMath.Clamp(Math.Log10(Math.Max(10,people))/4,.15,1.7)*(1+tradition.Network);
                double chance=1-Math.Exp(-dt*opportunity.Rate*network*tradition.Retention*special);
                if(Rng.Value(seed,"lithic-innovation-"+(long)opportunity.Skill,unchecked(tradition.Id*7919+s.Years))>=chance)continue;
                tradition.Knowledge|=opportunity.Skill;
                events.Add(new HomininEvent{Year=s.Years,Region=representative.Region,Lineage=representative.Lineage,Tradition=tradition.Id,Kind="technology",Text=tradition.Name+" established a learned "+LithicSkillName(opportunity.Skill)+" practice."});
            }
            if((tradition.Knowledge&LithicKnowledge.Cultivation)!=0)tradition.Cultivation=SurvivalMath.Clamp(tradition.Cultivation+(suitability*.7-tradition.Cultivation)*(1-Math.Exp(-dt/1500d)));
            if((tradition.Knowledge&LithicKnowledge.Herding)!=0)tradition.Herding=SurvivalMath.Clamp(tradition.Herding+(.6-tradition.Herding)*(1-Math.Exp(-dt/2000d)));
            if(people<35&&tradition.Network<.1)
            {
                foreach(var opportunity in lithicOpportunities)
                    if((tradition.Knowledge&opportunity.Skill)!=0&&opportunity.Communication>.4&&Rng.Value(seed,"lithic-loss-"+(long)opportunity.Skill,unchecked(tradition.Id*397+s.Years))<1-Math.Exp(-dt*.00002*(1-tradition.Retention)))
                    {tradition.Knowledge&=~opportunity.Skill;events.Add(new HomininEvent{Year=s.Years,Region=representative.Region,Tradition=tradition.Id,Lineage=representative.Lineage,Kind="knowledge-loss",Text=tradition.Name+" lost its last practitioners of "+LithicSkillName(opportunity.Skill)+"."});}
            }
        }
        public static string LithicSkillName(LithicKnowledge skill)
        {
            switch(skill){case LithicKnowledge.Flakes:return "stone flaking";case LithicKnowledge.FireKeeping:return "fire maintenance";case LithicKnowledge.FireMaking:return "fire ignition";case LithicKnowledge.PreparedCores:return "prepared-core knapping";case LithicKnowledge.HideWorking:return "hide working";case LithicKnowledge.BoneTools:return "bone-tool manufacture";case LithicKnowledge.FoodStorage:return "food storage";case LithicKnowledge.PlantManagement:return "wild-plant management";case LithicKnowledge.Symbols:return "symbolic communication";default:return skill.ToString().ToLowerInvariant();}
        }
    }
}
