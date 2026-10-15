using System;
using System.Collections.Generic;
using System.Threading;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        // Execution-only physical paths. Political permission and live shipping
        // are deliberately NOT cached here. The key contains every physical
        // rule input, including historical rule modes and directed endpoints.
        const int MaximumContactPairs=131072,MaximumContactSearchNodes=524288,MaximumContactSources=256;
        readonly Dictionary<((int,bool,bool,bool,int,int),int),LinkedListNode<ContactAnswer>> civicContactCache=new Dictionary<((int,bool,bool,bool,int,int),int),LinkedListNode<ContactAnswer>>();
        readonly LinkedList<ContactAnswer> contactAnswers=new LinkedList<ContactAnswer>();
        readonly Dictionary<(int,bool,bool,bool,int,int),LinkedListNode<ContactSearch>> contactSources=new Dictionary<(int,bool,bool,bool,int,int),LinkedListNode<ContactSearch>>();
        readonly LinkedList<ContactSearch> contactSearchOrder=new LinkedList<ContactSearch>();
        int contactSearchNodes;
        bool[] contactCoastalWater;
        readonly Dictionary<(bool,int),LinkedListNode<ContactCosts>> contactCostModes=new Dictionary<(bool,int),LinkedListNode<ContactCosts>>();
        readonly LinkedList<ContactCosts> contactCostOrder=new LinkedList<ContactCosts>();
        sealed class ContactCosts { public (bool,int) Key;public int[][] Rows; }
        int[][] PhysicalContactCosts(bool roads,int environment)
        {
            var key=(roads,environment);
            if(contactCostModes.TryGetValue(key,out var prior)){contactCostOrder.Remove(prior);contactCostOrder.AddLast(prior);return prior.Value.Rows;}
            if(contactCostOrder.Count>=4){contactCostModes.Remove(contactCostOrder.First.Value.Key);contactCostOrder.RemoveFirst();}
            var costs=new ContactCosts{Key=key,Rows=new int[World.Surface.Cells.Length][]};contactCostModes.Add(key,contactCostOrder.AddLast(costs));return costs.Rows;
        }
        sealed class ContactAnswer
        {
            public ((int,bool,bool,bool,int,int),int) Key;public bool Reachable;
        }
        sealed class ContactSearch
        {
            public (int,bool,bool,bool,int,int) Key;
            public readonly Dictionary<int,int> Distance=new Dictionary<int,int>();
            public readonly Heap Frontier=new Heap();
        }
        bool CachedPhysicalContact(int start,int end,bool roads,bool sailing,bool navigation,int environment,int movement,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var sourceKey=(start,roads,sailing,navigation,environment,movement);var pairKey=(sourceKey,end);
            if(civicContactCache.TryGetValue(pairKey,out var answer))
            {
                contactAnswers.Remove(answer);contactAnswers.AddLast(answer);return answer.Value.Reachable;
            }
            using var measurement=ProfileWork("physical contact search");
            if(!contactSources.TryGetValue(sourceKey,out var node))
            {
                var fresh=new ContactSearch{Key=sourceKey};fresh.Distance.Add(start,0);fresh.Frontier.Push(0,start);
                node=contactSearchOrder.AddLast(fresh);contactSources.Add(sourceKey,node);contactSearchNodes++;
            }
            else {contactSearchOrder.Remove(node);contactSearchOrder.AddLast(node);}
            var search=node.Value;int before=search.Distance.Count;bool reachable;
            try
            {
                if(sailing&&!navigation&&contactCoastalWater==null)
                {
                    var coastal=new bool[World.Surface.Cells.Length];
                    for(int i=0;i<coastal.Length;i++)
                    {
                        if((i&2047)==0)token.ThrowIfCancellationRequested();
                        foreach(int next in World.Surface.Cells[i].Neighbors)if(!World.Hydrology.OceanConnected[next]){coastal[i]=true;break;}
                    }
                    contactCoastalWater=coastal;
                }
                int limit=environment!=0?roads?900000:600000:1200000;
                var weights=PhysicalContactCosts(roads,environment);
                // A discovered node already has a valid path within the horizon.
                // Finish expanding each popped node before returning, so the
                // remaining frontier can answer subsequent targets exactly.
                while(!search.Distance.ContainsKey(end)&&search.Frontier.Count>0)
                {
                    token.ThrowIfCancellationRequested();var item=search.Frontier.Pop();
                    if(search.Distance[item.Id]!=item.Key)continue;var cell=World.Surface.Cells[item.Id];
                    var edges=weights[item.Id];
                    if(edges==null)
                    {
                        edges=new int[cell.Neighbors.Length];
                        for(int k=0;k<edges.Length;k++)
                        {
                            int to=cell.Neighbors[k];bool sea=World.Hydrology.OceanConnected[to];
                            long edge=sea?cell.DistanceM[k]:Math.Max(1,(int)(cell.DistanceM[k]*(1+World.Ecology.Habitat[to].ReliefM/900.0)*(roads?.65:1)));
                            if(environment!=0&&!sea)edge=(long)Math.Round(edge*HistoricalCropDiffusionEffects.EnvironmentalCost(World.Ecology.Habitat[item.Id],World.Ecology.Habitat[to],environment==2));
                            // Even after the maximum4.5x transport multiplier,
                            // an int-overflow edge cannot enter a1.2Mm search.
                            edges[k]=edge>int.MaxValue?int.MaxValue:(int)edge;
                        }
                        weights[item.Id]=edges;
                    }
                    for(int k=0;k<cell.Neighbors.Length;k++)
                    {
                        int to=cell.Neighbors[k];bool sea=World.Hydrology.OceanConnected[to];
                        if(sea&&(!sailing||!navigation&&!contactCoastalWater[to]))continue;
                        long edge=edges[k];if(movement!=0&&!sea)edge=Math.Max(1,(long)Math.Round(edge/(movement/4.0)));
                        long cost=item.Key+edge;
                        if(cost<=limit&&(!search.Distance.TryGetValue(to,out int old)||cost<old)){search.Distance[to]=(int)cost;search.Frontier.Push(cost,to);}
                    }
                }
                reachable=search.Distance.ContainsKey(end);
            }
            finally
            {
                // Cancellation is checked only between complete expansions:
                // even a cancelled query leaves a valid resumable frontier.
                contactSearchNodes+=search.Distance.Count-before;
                while(contactSearchOrder.Count>1&&(contactSources.Count>MaximumContactSources||contactSearchNodes>MaximumContactSearchNodes))
                {
                    var oldest=contactSearchOrder.First;contactSearchNodes-=oldest.Value.Distance.Count;
                    contactSources.Remove(oldest.Value.Key);contactSearchOrder.RemoveFirst();
                }
            }
            if(civicContactCache.Count>=MaximumContactPairs)
            {civicContactCache.Remove(contactAnswers.First.Value.Key);contactAnswers.RemoveFirst();}
            civicContactCache.Add(pairKey,contactAnswers.AddLast(new ContactAnswer{Key=pairKey,Reachable=reachable}));
            return reachable;
        }
    }
}
