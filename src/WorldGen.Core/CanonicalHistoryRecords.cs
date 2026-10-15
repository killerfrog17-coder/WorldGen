using System;
using System.IO;
namespace WorldGen.Core
{
    public static partial class StateHash
    {
        sealed class IntegerRecordBlock
        {
            public readonly byte[] Bytes;public readonly int[] Offsets;
            public IntegerRecordBlock(bool leaderHeader,params string[] names)
            {
                using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
                {
                    if(leaderHeader){writer.Write((byte)1);writer.Write("WorldGen.Core.HistoricalLeader");}
                    Offsets=new int[names.Length];
                    for(int i=0;i<names.Length;i++){writer.Write(names[i]);writer.Write((byte)1);writer.Write("System.Int32");Offsets[i]=(int)stream.Position;writer.Write(0);}
                    Bytes=stream.ToArray();
                }
            }
        }
        static readonly IntegerRecordBlock LeaderHead=new IntegerRecordBlock(true,"Administration","Ambition","Defeats","Diplomacy","EndedYear","ExpectedEndYear","Id"),
            LeaderMilitary=new IntegerRecordBlock(false,"Military"),LeaderPolity=new IntegerRecordBlock(false,"PolityId"),
            LeaderPredecessor=new IntegerRecordBlock(false,"PredecessorId"),LeaderAchievements=new IntegerRecordBlock(false,"Reforms","Renown"),
            LeaderDates=new IntegerRecordBlock(false,"StartedYear","Victories");
        static void LeaderArrayBody(BinaryWriter writer,HistoricalLeader[] leaders)
        {
            // Identical reflection-format bytes, including optional-field skips.
            // Templates are immutable; every changing value is read each time,
            // and each invocation owns its writable scratch. No integrity cache.
            var head=(byte[])LeaderHead.Bytes.Clone();var military=(byte[])LeaderMilitary.Bytes.Clone();var polity=(byte[])LeaderPolity.Bytes.Clone();
            var predecessor=(byte[])LeaderPredecessor.Bytes.Clone();var achievements=(byte[])LeaderAchievements.Bytes.Clone();var dates=(byte[])LeaderDates.Bytes.Clone();
            foreach(var leader in leaders)
            {
                writer.Write((byte)1);if(leader==null){writer.Write((byte)0);continue;}
                PutInteger(head,LeaderHead.Offsets[0],leader.Administration);PutInteger(head,LeaderHead.Offsets[1],leader.Ambition);PutInteger(head,LeaderHead.Offsets[2],leader.Defeats);
                PutInteger(head,LeaderHead.Offsets[3],leader.Diplomacy);PutInteger(head,LeaderHead.Offsets[4],leader.EndedYear);PutInteger(head,LeaderHead.Offsets[5],leader.ExpectedEndYear);PutInteger(head,LeaderHead.Offsets[6],leader.Id);writer.Write(head);
                writer.Write("Legacy");Write(writer,leader.Legacy);
                PutInteger(military,LeaderMilitary.Offsets[0],leader.Military);writer.Write(military);writer.Write("Name");Write(writer,leader.Name);
                PutInteger(polity,LeaderPolity.Offsets[0],leader.PolityId);writer.Write(polity);
                if(leader.PredecessorId!=-1){PutInteger(predecessor,LeaderPredecessor.Offsets[0],leader.PredecessorId);writer.Write(predecessor);}
                PutInteger(achievements,LeaderAchievements.Offsets[0],leader.Reforms);PutInteger(achievements,LeaderAchievements.Offsets[1],leader.Renown);writer.Write(achievements);
                if(!string.IsNullOrEmpty(leader.Selection)){writer.Write("Selection");Write(writer,leader.Selection);}
                PutInteger(dates,LeaderDates.Offsets[0],leader.StartedYear);PutInteger(dates,LeaderDates.Offsets[1],leader.Victories);writer.Write(dates);
            }
            writer.Write((byte)0);
        }
    }
}
