using System;
using System.Linq;

namespace WorldGen.Core
{
    // Local social outcomes do not depend on generating a transcript.
    public static class NpcSocialExchange
    {
        public static bool ShareOne(NpcSocialState source,NpcSocialState listener,long speaker,string name,double day,double closeness=0)
        {
            if(ReferenceEquals(source,listener))return false;
            foreach(var fact in source.Incidents.OrderByDescending(e=>e.SolarDay).ThenByDescending(e=>e.Sequence))
            {
                if(day<fact.SolarDay||day-fact.SolarDay>2||fact.Confidence<.45)continue;
                if(fact.Kind!=NpcIncidentKind.Assault&&fact.Kind!=NpcIncidentKind.Death&&fact.Kind!=NpcIncidentKind.Theft&&fact.Kind!=NpcIncidentKind.Missing&&fact.Kind!=NpcIncidentKind.Returned&&fact.Kind!=NpcIncidentKind.AnimalSeen)continue;
                long origin=fact.OriginPerson>0?fact.OriginPerson:speaker,sequence=fact.OriginSequence>0?fact.OriginSequence:fact.Sequence;
                if(listener.Incidents.Any(e=>e.OriginPerson==origin&&e.OriginSequence==sequence||e.Kind==fact.Kind&&e.Actor==fact.Actor&&e.Subject==fact.Subject&&e.Animal==fact.Animal&&e.SolarDay>=fact.SolarDay))continue;
                var heard=fact.Copy();heard.Sequence=++listener.EventSequence;heard.FirstHand=false;heard.Reporter=speaker;heard.OriginPerson=origin;heard.OriginSequence=sequence;heard.Confidence=fact.Confidence*.75;
                string account=fact.Detail.Replace("You personally","The witness personally").Replace("You saw","The witness saw").Replace("You only know","The witness only knows");
                heard.Detail=NpcDecision.Clean(name+" reported (not your own observation): "+account,220);
                listener.Incidents.Add(heard);while(listener.Incidents.Count>24)listener.Incidents.RemoveAt(0);
                listener.Advance(day);
                if(heard.Kind==NpcIncidentKind.Death&&heard.Subject>0&&heard.Confidence>=.5)
                {
                    var known=listener.KnownPeople.FirstOrDefault(p=>p.Person==heard.Subject);
                    if(known==null)
                    {
                        known=new NpcKnownPerson{Person=heard.Subject,LastSeenSolarDay=heard.SolarDay,LastPosition=heard.Position,ReportedPresence=true};listener.KnownPeople.Add(known);
                        if(listener.KnownPeople.Count>64)listener.KnownPeople.Remove(listener.KnownPeople.Where(p=>p!=known).OrderBy(p=>p.LastSeenSolarDay).First());
                    }
                    known.SeenDead=true;listener.Grief=Math.Max(listener.Grief,.1+.5*Math.Max(0,Math.Min(1,closeness)));
                }
                else if(heard.Kind==NpcIncidentKind.Assault||heard.Kind==NpcIncidentKind.Theft)
                {
                    if(heard.Actor>0){var tie=listener.Tie(heard.Actor,day);tie.Trust=Math.Max(-1,tie.Trust-.06*heard.Confidence);}
                    listener.Fear=Math.Max(listener.Fear,.1*heard.Confidence);
                }
                return true;
            }
            return false;
        }
    }
}
