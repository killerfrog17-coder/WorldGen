using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Security;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public sealed class FoodWebDiagram
    {
        public sealed class Node { public int Id; public string Name=""; public int Column,Row; public bool Basal; public double X=>20+Column*205; public double Y=>40+Row*48; }
        public sealed class Link { public int From,To; }
        public Node[] Nodes=Array.Empty<Node>();public Link[] Links=Array.Empty<Link>();
        public int Width=830,Height=520;
        public static string ShortName(string name,int limit=24)=>name.Length<=limit?name:name.Substring(0,Math.Max(1,limit-1))+"…";
        public static FoodWebDiagram Build(World w,int cell)
        {
            var result=new FoodWebDiagram();
            if(w?.Ecology==null || cell<0 || cell>=w.Ecology.Region.Length)return result;
            var e=w.Ecology;int region=e.Region[cell];if(region<0)return result;
            var r=e.Regions[region];var ids=r.Species.Select(s=>s.TaxonId).Distinct().OrderBy(i=>i).ToArray();
            var level=ids.ToDictionary(id=>id,id=>e.Taxa[id].Producer||e.Taxa[id].BasalResource?0:99);
            var links=r.FoodWeb.Where(l=>level.ContainsKey(l.ResourceId)&&level.ContainsKey(l.ConsumerId)).Select(l=>new Link{From=l.ResourceId,To=l.ConsumerId}).GroupBy(l=>((long)l.From<<32)|(uint)l.To).Select(g=>g.First()).OrderBy(l=>l.From).ThenBy(l=>l.To).ToArray();
            // Layout by the longest basal-rooted dependency, not shortest food
            // path (an omnivore eating fruit must not flatten the whole web).
            // Collapse feeding cycles first; they share a column, not infinite depth.
            var outgoing=ids.ToDictionary(id=>id,id=>links.Where(l=>l.From==id).Select(l=>l.To).ToArray());
            var index=new Dictionary<int,int>();var low=new Dictionary<int,int>();var component=new Dictionary<int,int>();
            var stack=new Stack<int>();var active=new HashSet<int>();int serial=0,components=0;
            void Visit(int id)
            {
                index[id]=low[id]=serial++;stack.Push(id);active.Add(id);
                foreach(int next in outgoing[id]){if(!index.ContainsKey(next)){Visit(next);low[id]=Math.Min(low[id],low[next]);}else if(active.Contains(next))low[id]=Math.Min(low[id],index[next]);}
                if(low[id]!=index[id])return;int member;do{member=stack.Pop();active.Remove(member);component[member]=components;}while(member!=id);components++;
            }
            foreach(int id in ids)if(!index.ContainsKey(id))Visit(id);
            var resources=Enumerable.Range(0,components).Select(_=>new SortedSet<int>()).ToArray();
            foreach(var link in links)if(component[link.From]!=component[link.To])resources[component[link.To]].Add(component[link.From]);
            var depths=Enumerable.Repeat(-1,components).ToArray();
            int Depth(int part){if(depths[part]>=0)return depths[part];return depths[part]=resources[part].Count==0?0:1+resources[part].Max(Depth);}
            foreach(int id in ids)level[id]=Depth(component[id]);
            int columns=ids.Length==0?1:ids.Max(id=>level[id]==99?0:level[id])+1;
            var rows=new int[columns];result.Nodes=ids.OrderBy(id=>level[id]).ThenBy(id=>e.Taxa[id].Name,StringComparer.Ordinal).Select(id=>{int column=level[id]==99?0:level[id];return new Node{Id=id,Name=e.Taxa[id].Name,Basal=level[id]==0,Column=column,Row=rows[column]++};}).ToArray();
            result.Width=Math.Max(830,columns*205+20);result.Links=links;result.Height=Math.Max(520,rows.Max()*48+70);return result;
        }
        public static string Svg(World w,int cell)
        {
            var graph=Build(w,cell);if(graph.Nodes.Length==0)return "<p>No terrestrial food web here.</p>";
            var nodes=graph.Nodes.ToDictionary(n=>n.Id);var b=new StringBuilder();
            b.Append("<svg class='food-web' xmlns='http://www.w3.org/2000/svg' width='").Append(graph.Width).Append("' height='").Append(graph.Height).Append("' role='img' aria-label='Ecoregion food web. Arrows point from food to consumer.'><defs><marker id='food-arrow' markerWidth='7' markerHeight='7' refX='6' refY='3' orient='auto'><path d='M0,0 L6,3 L0,6' fill='#9bb7bd'/></marker></defs><text x='20' y='20' fill='#eee'>Food → consumer · potential feeding links</text>");
            foreach(var link in graph.Links){var a=nodes[link.From];var z=nodes[link.To];b.Append("<path class='food-link' d='M").Append(a.X+160).Append(',').Append(a.Y+15).Append(" L").Append(z.X).Append(',').Append(z.Y+15).Append("' stroke='#9bb7bd' stroke-opacity='.32' fill='none' marker-end='url(#food-arrow)'/>");}
            foreach(var n in graph.Nodes)b.Append("<g class='food-node'><title>").Append(SecurityElement.Escape(n.Name)).Append("</title><rect x='").Append(n.X).Append("' y='").Append(n.Y).Append("' width='160' height='31' rx='5' fill='").Append(n.Basal?"#28513f":"#284858").Append("' stroke='#90a7a4'/><text x='").Append(n.X+6).Append("' y='").Append(n.Y+20).Append("' fill='#f3edda' font-size='11'>").Append(SecurityElement.Escape(ShortName(n.Name))).Append("</text></g>");
            return b.Append("</svg>").ToString();
        }
    }
}
