using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace WorldGen.Core
{
    /// <summary>
    /// Planetary drainage-aware dyadic amplification, adapted from Cortial et al.,
    /// Real-Time Hyper-Amplification of Planets (2020), Arches-Team implementation.
    /// See third_party/hyper-amplification/LICENSE and docs/HYPER_AMPLIFICATION.md.
    /// The shared CPU evaluator owns geometry: render LOD never changes collision,
    /// water topology, randomness, or the accepted simulation world. Disposable
    /// subdivision caches have a fixed working-set budget, not a fine planet map.
    /// </summary>
    public sealed partial class HyperAmplification
    {
        public const string Version = "hyper-amplification-9";
        public const double FinestSpacingM = 8;
        const int WorkingSet = 32768;
        readonly object gate = new object();
        readonly Queue<object> retained = new Queue<object>();
        readonly CoarseFace[] roots;
        readonly CoarseEdge[] coarseEdges;
        readonly Vertex[] rootNodes;
        readonly int[] nodeChannels,nodeRivers;
        readonly WeakReference<Face>[] rootCache;
        readonly WeakReference<Edge>[] edgeCache;
        int rootSweep,edgeSweep;
        readonly double radius, scale;
        readonly int seed;
        readonly int terminalDepth;
        long subdivisions;
        public double PreparationMilliseconds { get; private set; }
        public int RootFaces => roots.Length;
        public int SubdivisionDepth => terminalDepth;
        public int CachedGroups { get { lock (gate) return retained.Count; } }
        public long SubdivisionGroups => Interlocked.Read(ref subdivisions);

        internal enum Kind : byte { Terrain, River, Ocean, Lake, Drainage }
        internal sealed class FaceRef
        {
            readonly int root,depth;
            readonly ulong path;
            WeakReference<Face> target;
            public FaceRef(Face face){root=face.Root;depth=face.Depth;path=face.Path;if(depth>0)target=new WeakReference<Face>(face);}
            public FaceRef(int root){this.root=root;depth=0;path=0;}
            public bool Matches(Face face)=>root==face.Root&&depth==face.Depth&&path==face.Path;
            public void Refresh(Face face){if(target!=null)target.SetTarget(face);}
            public Face Resolve(HyperAmplification owner)
            {
                if(depth==0)return owner.RootFace(root);
                if(target.TryGetTarget(out var face))return face;
                face=owner.RootFace(root);
                for(int level=1;level<=depth;level++)face=owner.Split(face).Faces[(int)((path>>((depth-level)*2))&3)];
                target.SetTarget(face);return face;
            }
        }
        internal sealed class Vertex
        {
            public ulong Key;
            public V Position;
            public Kind Type;
            public double Height, Water, RiverBed, Distance, Flow, Crust, Mountain, Hill, Plateau, Sediment, Age, Dryness,Hardness;
            public ulong LakeKey, ChannelKey;
            public double LakeLevel,LakeWeight,SeaWeight;
            public double BasinFloor=double.NegativeInfinity;
            public Edge River;
            public bool Wet => Type == Kind.River || Type == Kind.Lake || Type == Kind.Ocean;
        }
        internal sealed class Edge
        {
            public Vertex A, B;
            public Kind Type;
            public ulong Key;
            public int Depth;
            public Edge Parent;
            public double BasinLevel=double.NaN;
            public object Lease;
            // Retain addresses, not the whole connected visited mesh. A missing
            // neighbour is reproducibly reconstructed through its dyadic path.
            public FaceRef Left, Right;
            public WeakReference<EdgeSplit> Children;
            public void Attach(Face face)
            {
                if (Left == null) Left = new FaceRef(face);
                else if (Left.Matches(face)) Left.Refresh(face);
                else if (Right == null) Right = new FaceRef(face);
                else if (Right.Matches(face)) Right.Refresh(face);
                else throw new InvalidOperationException("Amplification edge has more than two incident faces.");
            }
        }
        internal sealed class EdgeSplit
        {
            public Vertex Middle;
            public Edge First, Last;
            public Edge At(Vertex endpoint) => ReferenceEquals(First.A, endpoint) || ReferenceEquals(First.B, endpoint) ? First : Last;
        }
        internal sealed class FaceSplit { public Face Parent;public Face[] Faces; }
        // Primitive control arrays are not millions of separately traced heap
        // objects. Actual faces/edges are realized only around queried points.
        struct CoarseEdge { public int A,B,Left,Right;public Kind Type;public ulong Key; }
        struct CoarseFace
        {
            public int A,B,C,AB,BC,CA;
            public double Ax,Ay,Az,Bx,By,Bz,Cx,Cy,Cz;
            public void Initialize(V p,V q,V r)
            {
                double ux=q.X-p.X,uy=q.Y-p.Y,uz=q.Z-p.Z,vx=r.X-p.X,vy=r.Y-p.Y,vz=r.Z-p.Z;
                double d=p.X*(uy*vz-uz*vy)+p.Y*(uz*vx-ux*vz)+p.Z*(ux*vy-uy*vx);
                if(Math.Abs(d)<1e-24)throw new InvalidOperationException("Degenerate coarse amplification triangle.");
                Ax=(q.Y*r.Z-q.Z*r.Y)/d;Ay=(q.Z*r.X-q.X*r.Z)/d;Az=(q.X*r.Y-q.Y*r.X)/d;
                Bx=(r.Y*p.Z-r.Z*p.Y)/d;By=(r.Z*p.X-r.X*p.Z)/d;Bz=(r.X*p.Y-r.Y*p.X)/d;
                Cx=(p.Y*q.Z-p.Z*q.Y)/d;Cy=(p.Z*q.X-p.X*q.Z)/d;Cz=(p.X*q.Y-p.Y*q.X)/d;
            }
            public double Coordinates(V p,out double a,out double b,out double c)
            {a=p.X*Ax+p.Y*Ay+p.Z*Az;b=p.X*Bx+p.Y*By+p.Z*Bz;c=p.X*Cx+p.Y*Cy+p.Z*Cz;double sum=a+b+c;a/=sum;b/=sum;c/=sum;return Math.Min(a,Math.Min(b,c));}
        }
        internal sealed class Face
        {
            public Vertex A, B, C;
            public Edge AB, BC, CA;
            public ulong Key;
            public int Depth;
            public int Root;
            public ulong Path;
            public object Lease;
            public WeakReference<FaceSplit> Children;
            // Inverse spherical triangle matrix. Do not use quantized V.Cross:
            // its 1e-9 rounding is inappropriate for metre-size determinants.
            double ax, ay, az, bx, by, bz, cx, cy, cz;
            public double Size;
            public double Orientation;
            public void Initialize(double physicalRadius)
            {
                V p = A.Position, q = B.Position, r = C.Position;
                double ux=q.X-p.X,uy=q.Y-p.Y,uz=q.Z-p.Z,vx=r.X-p.X,vy=r.Y-p.Y,vz=r.Z-p.Z;
                double d=p.X*(uy*vz-uz*vy)+p.Y*(uz*vx-ux*vz)+p.Z*(ux*vy-uy*vx);
                if (Math.Abs(d) < 1e-24) throw new InvalidOperationException("Degenerate amplified triangle.");
                Orientation=d;
                bool Has(Edge e,Vertex x,Vertex y)=>(ReferenceEquals(e.A,x)&&ReferenceEquals(e.B,y))||(ReferenceEquals(e.A,y)&&ReferenceEquals(e.B,x));
                if(!Has(AB,A,B)||!Has(BC,B,C)||!Has(CA,C,A))throw new InvalidOperationException("Amplified face/edge connectivity mismatch.");
                ax = (q.Y * r.Z - q.Z * r.Y) / d; ay = (q.Z * r.X - q.X * r.Z) / d; az = (q.X * r.Y - q.Y * r.X) / d;
                bx = (r.Y * p.Z - r.Z * p.Y) / d; by = (r.Z * p.X - r.X * p.Z) / d; bz = (r.X * p.Y - r.Y * p.X) / d;
                cx = (p.Y * q.Z - p.Z * q.Y) / d; cy = (p.Z * q.X - p.X * q.Z) / d; cz = (p.X * q.Y - p.Y * q.X) / d;
                Size = Math.Max((p - q).Length, Math.Max((q - r).Length, (r - p).Length)) * physicalRadius;
                AB.Attach(this); BC.Attach(this); CA.Attach(this);
            }
            public double Coordinates(V p, out double a, out double b, out double c)
            {
                a = p.X * ax + p.Y * ay + p.Z * az;
                b = p.X * bx + p.Y * by + p.Z * bz;
                c = p.X * cx + p.Y * cy + p.Z * cz;
                double sum = a + b + c; a /= sum; b /= sum; c /= sum;
                return Math.Min(a, Math.Min(b, c));
            }
            public Vertex Opposite(Edge edge) => !ReferenceEquals(A, edge.A) && !ReferenceEquals(A, edge.B) ? A :
                !ReferenceEquals(B, edge.A) && !ReferenceEquals(B, edge.B) ? B : C;
            public V FieldGradient(V p,double va,double vb,double vc)
            {
                double a=p.X*ax+p.Y*ay+p.Z*az,b=p.X*bx+p.Y*by+p.Z*bz,c=p.X*cx+p.Y*cy+p.Z*cz;
                double sum=a+b+c,value=(a*va+b*vb+c*vc)/sum;
                return new V{X=(ax*va+bx*vb+cx*vc-value*(ax+bx+cx))/sum,
                    Y=(ay*va+by*vb+cy*vc-value*(ay+by+cy))/sum,Z=(az*va+bz*vb+cz*vc-value*(az+bz+cz))/sum};
            }
        }
        sealed class EdgeKeyComparer : IEqualityComparer<ulong>
        {
            public bool Equals(ulong a, ulong b) => a == b;
            public int GetHashCode(ulong x) => unchecked((int)Rng.Mix((uint)x ^ Rng.Mix((uint)(x >> 32))));
        }
        static ulong Pair(int a, int b) => ((ulong)(uint)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);
        static ulong Mix(ulong a)
        {
            unchecked { a ^= a >> 30; a *= 0xbf58476d1ce4e5b9UL; a ^= a >> 27; a *= 0x94d049bb133111ebUL; return a ^ (a >> 31); }
        }
        static double Random(ulong key, uint stream) => (Rng.Mix((uint)key ^ Rng.Mix((uint)(key >> 32)) ^ stream) >> 8) / 16777216.0;
        static double Lerp(double a, double b, double t) => a + (b - a) * t;

        internal HyperAmplification(World world, LocalTerrainProfile[] profiles, double horizontalScale, CancellationToken token)
        {
            var watch = Stopwatch.StartNew(); radius = world.Recipe.RadiusKm * 1000; scale = horizontalScale; seed = world.Recipe.Seed;
            int count = profiles.Length; var nodes = rootNodes = new Vertex[count]; var hydro = world.Hydrology;
            var heights = new double[count]; var flows = new double[count];
            for (int i = 0; i < count; i++)
            {
                if ((i & 4095) == 0) token.ThrowIfCancellationRequested();
                flows[i] = i < hydro.NorthernSummerDischargeM3PerS.Length ? .25 * (hydro.NorthernSpringDischargeM3PerS[i] + hydro.NorthernSummerDischargeM3PerS[i] + hydro.NorthernAutumnDischargeM3PerS[i] + hydro.NorthernWinterDischargeM3PerS[i]) : 0;
                heights[i] = world.ElevationM[i];
                int body = i < hydro.WaterBody.Length ? hydro.WaterBody[i] : -1;
                if (body >= 0 && body < hydro.Waters.Length && hydro.LakeCoverageFraction[i] > .5)
                    heights[i] = Math.Min(heights[i], hydro.Waters[body].SurfaceElevationM - 2);
            }
            // Preserve source drainage connectivity, but do not fill valley floors
            // up to spill heights. The derived channel ceiling only cuts the base.
            // Hydrology.Topology stores sources FIRST (not priority-flood order).
            for (int k = 0; k < hydro.VisitOrder.Length; k++)
            {
                int i = hydro.VisitOrder[k], r = hydro.Receiver[i];
                if (r >= 0 && !hydro.OceanConnected[r] && flows[i] >= .04) heights[r] = Math.Min(heights[r], heights[i]);
            }
            for (int i = 0; i < count; i++)
            {
                var p = profiles[i]; bool ocean = i < hydro.OceanConnected.Length && hydro.OceanConnected[i];
                int body = i < hydro.WaterBody.Length ? hydro.WaterBody[i] : -1;
                bool lake = !ocean && body >= 0 && body < hydro.Waters.Length && hydro.LakeCoverageFraction[i] >= .5;
                bool river = !ocean && !lake && flows[i] >= .04 && i < hydro.Receiver.Length && hydro.Receiver[i] >= 0;
                double depth = river ? Depth(flows[i]) : 0;
                double valley = river ? Math.Min(p.MountainHeightM * .24 + p.HillHeightM * .3, Math.Max(4, (p.ElevationM - heights[i]) + p.CoarseReliefM * .07)) : 0;
                double bed = ocean ? world.ElevationM[i] : lake ? heights[i] : heights[i] - valley - depth;
                if(river&&world.ElevationM[i]>=0)bed=Math.Max(.02,bed);
                nodes[i] = new Vertex { Key = Mix((ulong)(uint)seed << 32 | (uint)i), Position = world.Surface.Cells[i].Position,
                    Type = ocean ? Kind.Ocean : lake ? Kind.Lake : river ? Kind.River : Kind.Terrain,
                    Height = bed, Water = ocean ? 0 : lake ? hydro.Waters[body].SurfaceElevationM : bed + depth,
                    RiverBed = bed, Distance = ocean || lake || river ? 0 : Math.Sqrt(world.Surface.Cells[i].AreaM2) * scale,
                    Flow = flows[i], Crust = p.ElevationM, Mountain = p.MountainHeightM, Hill = p.HillHeightM,
                    LakeKey=lake?(ulong)(uint)(body+1):0,LakeLevel=lake?hydro.Waters[body].SurfaceElevationM:0,LakeWeight=lake?1:0,
                    SeaWeight=ocean?1:0,
                    ChannelKey=Mix(((ulong)(uint)seed<<32)^(uint)i^0x72546e43UL),
                    Plateau = p.Plateau, Sediment = p.Sediment,
                    Age = world.Recipe.TerrainSource=="Earth"?Lerp(350,20,OrogenMorphology.Smooth(450,1800,p.CoarseReliefM)):D.Clamp((1.95-p.Sharpness)*333,0,600),
                    Hardness=D.Clamp(p.Ground.RockMixA.X+p.Ground.RockMixA.Y+p.Ground.RockMixB.Z),
                    Dryness = p.Biome == Biome.Desert ? 1 : p.Biome == Biome.Savanna ? .55 : p.Biome == Biome.Grassland ? .35 : .1 };
            }
            // Reconcile actual confluences once. Fine subdivision inherits these
            // ordered endpoint values; it never performs a catchment walk.
            for (int k = hydro.VisitOrder.Length - 1; k >= 0; k--)
            {
                int i = hydro.VisitOrder[k], r = hydro.Receiver[i];
                if (r < 0 || nodes[i].Type != Kind.River || !nodes[r].Wet) continue;
                double floor=nodes[r].Water;
                nodes[i].Water=Math.Max(nodes[i].Water,floor);
                nodes[i].Height=nodes[i].RiverBed=nodes[i].Water-Depth(flows[i]);
                // A supplied lake can inundate a below-surface tributary control;
                // this is standing water, not a raised river suspended over it.
                if(nodes[r].LakeKey!=0&&floor>nodes[i].Crust)
                {nodes[i].Type=Kind.Lake;nodes[i].LakeKey=nodes[r].LakeKey;nodes[i].LakeLevel=floor;nodes[i].LakeWeight=1;}
            }
            nodeChannels=new int[count];nodeRivers=new int[count];
            for(int i=0;i<count;i++)nodeChannels[i]=nodeRivers[i]=-1;
            var edges = new Dictionary<ulong,int>(world.Surface.Edges.Length,new EdgeKeyComparer());
            var controls=new List<CoarseEdge>(world.Surface.Edges.Length);
            int MakeEdge(int a,int b,int face)
            {
                ulong key=Pair(a,b);
                if(edges.TryGetValue(key,out int found)){var previous=controls[found];previous.Right=face;controls[found]=previous;return found;}
                bool drain = a < hydro.Receiver.Length && hydro.Receiver[a] == b && !hydro.OceanConnected[a] && flows[a]>=.04 ||
                    b < hydro.Receiver.Length && hydro.Receiver[b] == a && !hydro.OceanConnected[b] && flows[b]>=.04;
                bool sameLake=nodes[a].Type==Kind.Lake&&nodes[b].Type==Kind.Lake&&nodes[a].LakeKey==nodes[b].LakeKey;
                int id=controls.Count;
                controls.Add(new CoarseEdge{A=a,B=b,Left=face,Right=-1,Type=drain&&!sameLake?Kind.River:Kind.Terrain,Key=Mix(key^((ulong)(uint)seed<<32))});
                if(drain){if(hydro.Receiver[a]==b)nodeChannels[a]=id;if(hydro.Receiver[b]==a)nodeChannels[b]=id;}
                edges.Add(key,id);return id;
            }
            roots=new CoarseFace[world.Surface.Triangles.Length/3];
            rootCache=new WeakReference<Face>[roots.Length];
            // One topological terminal depth across adjacent faces. Stopping
            // independently by triangle size produces a nonconforming HEIGHT
            // field, even if streamed render tiles sample canonical coordinates.
            double nominalArea=4*Math.PI*radius*radius*scale*scale/roots.Length;
            int targetDepth=0;
            while(nominalArea>FinestSpacingM*FinestSpacingM&&targetDepth<20){nominalArea*=.25;targetDepth++;}
            terminalDepth=targetDepth;
            for (int t = 0; t < roots.Length; t++)
            {
                if ((t & 4095) == 0) token.ThrowIfCancellationRequested();
                int a = world.Surface.Triangles[t * 3], b = world.Surface.Triangles[t * 3 + 1], c = world.Surface.Triangles[t * 3 + 2];
                roots[t]=new CoarseFace{A=a,B=b,C=c,AB=MakeEdge(a,b,t),BC=MakeEdge(b,c,t),CA=MakeEdge(c,a,t)};
                roots[t].Initialize(nodes[a].Position,nodes[b].Position,nodes[c].Position);
            }
            coarseEdges=controls.ToArray();edgeCache=new WeakReference<Edge>[coarseEdges.Length];
            var incoming=new bool[count];
            for(int i=0;i<count;i++)if(nodeChannels[i]>=0)
            {
                nodeRivers[i]=nodeChannels[i];int r=hydro.Receiver[i];if(r>=0){incoming[r]=true;if(nodeRivers[r]<0)nodeRivers[r]=nodeChannels[i];}
            }
            for(int i=0;i<count;i++)if(nodes[i].Type==Kind.River&&!incoming[i])
            {
                int r=hydro.Receiver[i];
                double level=Math.Max(nodes[i].Height,r>=0&&nodes[r].Wet?nodes[r].Water:nodes[i].Height);
                nodes[i].Water=nodes[i].Height=nodes[i].RiverBed=level;
            }
            // Connected tributaries share a basin identity, not an arbitrary
            // nearest segment's ID. Separate coastal mouths/lake inlets cannot
            // be blended into the same sheet by a coarse render triangle.
            for(int k=hydro.VisitOrder.Length-1;k>=0;k--)
            {
                int i=hydro.VisitOrder[k],r=hydro.Receiver[i];
                if(r>=0&&nodes[r].Type!=Kind.Ocean&&nodes[r].Type!=Kind.Lake)nodes[i].ChannelKey=nodes[r].ChannelKey;
            }
            BuildLakeBasins(world,token);
            BuildRiverscapes(world,token);
            PreparationMilliseconds = watch.Elapsed.TotalMilliseconds;
        }

        Edge RootEdge(int id)
        {
            var lookup=edgeCache[id];if(lookup!=null&&lookup.TryGetTarget(out var cached))return cached;
            lock(gate)
            {
                if(edgeCache[id]!=null&&edgeCache[id].TryGetTarget(out cached))return cached;
                var source=coarseEdges[id];var edge=NewEdge(rootNodes[source.A],rootNodes[source.B],source.Type,0,source.Key);
                edge.BasinLevel=LakeDivideLevel(source);
                edge.Left=new FaceRef(source.Left);if(source.Right>=0)edge.Right=new FaceRef(source.Right);
                if(edgeCache[id]==null)edgeCache[id]=new WeakReference<Edge>(edge);else edgeCache[id].SetTarget(edge);
                Retain(edge);
                // Drop expired weak wrappers gradually too, rather than leaving
                // one live GC handle for every place ever visited on the planet.
                for(int k=0;k<8;k++){int old=edgeSweep++;if(edgeSweep==edgeCache.Length)edgeSweep=0;var weak=edgeCache[old];if(weak!=null&&!weak.TryGetTarget(out _))edgeCache[old]=null;}
                return edge;
            }
        }
        Face RootFace(int id)
        {
            var lookup=rootCache[id];if(lookup!=null&&lookup.TryGetTarget(out var cached))return cached;
            lock(gate)
            {
                if(rootCache[id]!=null&&rootCache[id].TryGetTarget(out cached))return cached;
                var s=roots[id];
                void BindRiver(int node){if(rootNodes[node].River==null&&nodeRivers[node]>=0)rootNodes[node].River=RootEdge(nodeRivers[node]);}
                BindRiver(s.A);BindRiver(s.B);BindRiver(s.C);
                var face=NewFace(rootNodes[s.A],rootNodes[s.B],rootNodes[s.C],RootEdge(s.AB),RootEdge(s.BC),RootEdge(s.CA),Mix((ulong)(uint)id+0x9e3779b97f4a7c15UL),0,null,id,0);
                if(rootCache[id]==null)rootCache[id]=new WeakReference<Face>(face);else rootCache[id].SetTarget(face);
                Retain(face);
                for(int k=0;k<8;k++){int old=rootSweep++;if(rootSweep==rootCache.Length)rootSweep=0;var weak=rootCache[old];if(weak!=null&&!weak.TryGetTarget(out _))rootCache[old]=null;}
                return face;
            }
        }

        Edge NewEdge(Vertex a, Vertex b, Kind type, int depth, ulong key) => new Edge { A = a, B = b, Type = type, Depth = depth, Key = key };
        Face NewFace(Vertex a, Vertex b, Vertex c, Edge ab, Edge bc, Edge ca, ulong key, int depth, object lease,int root,ulong path)
        {
            var f = new Face { A = a, B = b, C = c, AB = ab, BC = bc, CA = ca, Key = key, Depth = depth, Lease = lease,Root=root,Path=path };
            f.Initialize(radius * scale); return f;
        }
        void Retain(object group)
        {
            retained.Enqueue(group); if (retained.Count > WorkingSet) retained.Dequeue();
            Interlocked.Increment(ref subdivisions);
        }
        FaceSplit Split(Face face)
        {
            if (face.Children != null && face.Children.TryGetTarget(out var ready)) return ready;
            lock (gate)
            {
                if (face.Children != null && face.Children.TryGetTarget(out ready)) return ready;
                var ab = Split(face.AB); var bc = Split(face.BC); var ca = Split(face.CA);
                var a = ab.Middle; var b = bc.Middle; var c = ca.Middle; int depth = face.Depth + 1;
                var e0 = NewEdge(a, b, Kind.Terrain, depth, Mix(face.Key ^ 0x846ca68bUL));
                var e1 = NewEdge(b, c, Kind.Terrain, depth, Mix(face.Key ^ 0x7feb352dUL));
                var e2 = NewEdge(c, a, Kind.Terrain, depth, Mix(face.Key ^ 0x5197UL));
                // R6/R7: a tributary (or dry gully) is a new internal edge, not
                // an unrelated spline added after the land was generated.
                Edge candidate = null; double candidateScore = double.MinValue;
                foreach (var e in new[] { e0, e1, e2 })
                {
                    if (e.A.Wet == e.B.Wet || e.A.Type == Kind.Ocean || e.B.Type == Kind.Ocean) continue;
                    var wet = e.A.Wet ? e.A : e.B; var land = e.A.Wet ? e.B : e.A;
                    double length = (land.Position - wet.Position).Length * radius * scale;
                    if (length < 100 || land.Height <= wet.Water + .1) continue;
                    double slope = (land.Height - wet.Water) / length;
                    double score = Random(e.Key, 0x1837) - slope * 2;
                    if (score > candidateScore) { candidate = e; candidateScore = score; }
                }
                if (candidate != null && depth <= 7)
                {
                    var wet = candidate.A.Wet ? candidate.A : candidate.B;
                    double chance = (.25 + .45 * (1 - wet.Dryness)) * (1 - OrogenMorphology.Smooth(4, 8, depth));
                    candidate.Type = Random(candidate.Key, 0x6719) < chance && wet.Flow > .02 ? Kind.River : Kind.Drainage;
                }
                var group = new FaceSplit{Parent=face};
                e0.Lease = e1.Lease = e2.Lease = group;
                group.Faces = new[] {
                    NewFace(face.A,a,c,ab.At(face.A),e2,ca.At(face.A),Mix(face.Key^1),depth,group,face.Root,face.Path<<2),
                    NewFace(a,face.B,b,ab.At(face.B),bc.At(face.B),e0,Mix(face.Key^2),depth,group,face.Root,(face.Path<<2)|1),
                    NewFace(c,b,face.C,e1,bc.At(face.C),ca.At(face.C),Mix(face.Key^3),depth,group,face.Root,(face.Path<<2)|2),
                    NewFace(a,b,c,e0,e1,e2,Mix(face.Key^4),depth,group,face.Root,(face.Path<<2)|3) };
                foreach(var child in group.Faces)if(child.Orientation*face.Orientation<=0)
                {
                    string Point(Vertex x)=>x.Position.X.ToString("R")+","+x.Position.Y.ToString("R")+","+x.Position.Z.ToString("R");
                    throw new InvalidOperationException("Inverted child triangle at depth "+depth+", root "+face.Root+", path "+face.Path+", parent size "+face.Size+", parent determinant "+face.Orientation+", child "+child.Orientation+", child path "+child.Path+", A "+Point(face.A)+", B "+Point(face.B)+", C "+Point(face.C)+", ab "+Point(a)+", bc "+Point(b)+", ca "+Point(c));
                }
                if(face.Children==null)face.Children=new WeakReference<FaceSplit>(group);else face.Children.SetTarget(group);
                Retain(group); return group;
            }
        }
        EdgeSplit Split(Edge edge)
        {
            if (edge.Children != null && edge.Children.TryGetTarget(out var ready)) return ready;
            // gate is reentrant. A missing neighbour at this level is realized
            // by subdividing its parent, never by changing a midpoint according
            // to which tile arrived first.
            lock (gate)
            {
                if (edge.Children != null && edge.Children.TryGetTarget(out ready)) return ready;
                if (edge.Parent != null)
                {
                    if (edge.Parent.Left != null) Split(edge.Parent.Left.Resolve(this));
                    if (edge.Parent.Right != null) Split(edge.Parent.Right.Resolve(this));
                }
                var middle = Midpoint(edge);
                Kind first = edge.Type, last = edge.Type;
                if (edge.Type == Kind.River || edge.Type == Kind.Drainage)
                {
                    // R4b ends at a true source: the final dry ridge-side half
                    // is not a water ribbon with a square, full-width endpoint.
                    if (!edge.A.Wet && edge.A.Type != Kind.Drainage) first = Kind.Terrain;
                    if (!edge.B.Wet && edge.B.Type != Kind.Drainage) last = Kind.Terrain;
                }
                var group = new EdgeSplit { Middle = middle,
                    First = NewEdge(edge.A, middle, first, edge.Depth + 1, Mix(edge.Key ^ 0x3811UL)),
                    Last = NewEdge(middle, edge.B, last, edge.Depth + 1, Mix(edge.Key ^ 0x3812UL)) };
                group.First.Parent = group.Last.Parent = edge;
                group.First.BasinLevel=group.Last.BasinLevel=edge.BasinLevel;
                group.First.Lease = group.Last.Lease = group;
                if(edge.Children==null)edge.Children=new WeakReference<EdgeSplit>(group);else edge.Children.SetTarget(group);
                Retain(group); return group;
            }
        }
        Vertex Midpoint(Edge edge)
        {
            // Sorting makes rule inputs independent of incident face ordering.
            var a = edge.A.Key < edge.B.Key ? edge.A : edge.B; var b = ReferenceEquals(a, edge.A) ? edge.B : edge.A;
            double u = Random(edge.Key, 0x1837), v = Random(edge.Key, 0x7917), w = Random(edge.Key, 0x2573);
            var node = new Vertex { Key = Mix(edge.Key ^ 0x5bf03635UL), Type = Kind.Terrain,
                Crust = (a.Crust + b.Crust) * .5, Mountain = (a.Mountain + b.Mountain) * .5,
                Hill = (a.Hill + b.Hill) * .5, Plateau = (a.Plateau + b.Plateau) * .5,
                Sediment = (a.Sediment + b.Sediment) * .5, Age = (a.Age + b.Age) * .5, Dryness = (a.Dryness + b.Dryness) * .5,
                Hardness=(a.Hardness+b.Hardness)*.5,
                Height = (a.Height + b.Height) * .5, Water = (a.Water + b.Water) * .5,
                RiverBed = (a.RiverBed + b.RiverBed) * .5, Flow = (a.Flow + b.Flow) * .5,SeaWeight=(a.SeaWeight+b.SeaWeight)*.5 };
            if(a.LakeKey!=0||b.LakeKey!=0)
            {
                var lake=a.LakeWeight>b.LakeWeight||a.LakeWeight==b.LakeWeight&&a.LakeKey<b.LakeKey?a:b;
                node.LakeKey=lake.LakeKey;node.LakeLevel=lake.LakeLevel;
                node.LakeWeight=.5*((a.LakeKey==lake.LakeKey?a.LakeWeight:0)+(b.LakeKey==lake.LakeKey?b.LakeWeight:0));
            }
            // The upstream shader's barycentric safe region, not independent
            // vertex jitter. Both incident faces share precisely this position.
            double fraction=.45+.1*u;
            double px=a.Position.X*fraction+b.Position.X*(1-fraction);
            double py=a.Position.Y*fraction+b.Position.Y*(1-fraction);
            double pz=a.Position.Z*fraction+b.Position.Z*(1-fraction);
            double baseX=px,baseY=py,baseZ=pz;
            Vertex opposite = null,otherOpposite=null;
            var leftFace=edge.Left?.Resolve(this);var rightFace=edge.Right?.Resolve(this);
            if (leftFace != null) opposite = leftFace.Opposite(edge);
            if (rightFace != null)
            {
                var other = rightFace.Opposite(edge);
                if (opposite == null) opposite = other;
                else { if (opposite.Key > other.Key) { var swap = opposite; opposite = other; other = swap; } otherOpposite=other; }
            }
            if (opposite != null)
            {
                // Great-circle altitude, using unquantized intermediates.
                double nx=a.Position.Y*b.Position.Z-a.Position.Z*b.Position.Y;
                double ny=a.Position.Z*b.Position.X-a.Position.X*b.Position.Z;
                double nz=a.Position.X*b.Position.Y-a.Position.Y*b.Position.X;
                double normalLength=Math.Sqrt(nx*nx+ny*ny+nz*nz);
                if(normalLength>1e-15)
                {
                    nx/=normalLength;ny/=normalLength;nz/=normalLength;
                    double altitude=opposite.Position.X*nx+opposite.Position.Y*ny+opposite.Position.Z*nz;
                    double safe=Math.Min(Math.Abs(altitude),(a.Position-b.Position).Length*.35),sign=Math.Sign(altitude);
                    if(otherOpposite!=null)
                    {
                        double other=otherOpposite.Position.X*nx+otherOpposite.Position.Y*ny+otherOpposite.Position.Z*nz;
                        safe=Math.Min(safe,Math.Abs(other));if(v>.5)sign=Math.Sign(other);
                    }
                    double shift=sign*safe*(.08+.08*w)*(1-OrogenMorphology.Smooth(7,11,edge.Depth));
                    px+=nx*shift;py+=ny*shift;pz+=nz*shift;
                }
            }
            // Altitude alone is insufficient for obtuse/sliver triangles: a
            // perpendicular offset can cross an END of the adjacent face.
            // Intersect BOTH barycentric safe regions, preserving wide endpoint
            // weights and a small signed opposite weight in each incident face.
            double safeFraction=1;
            void Constrain(Face face)
            {
                if(face==null)return;
                face.Coordinates(new V{X=baseX,Y=baseY,Z=baseZ},out double ba,out double bb,out double bc);
                face.Coordinates(new V{X=px,Y=py,Z=pz},out double ta,out double tb,out double tc);
                void Limit(Vertex vertex,double start,double end)
                {
                    bool endpoint=ReferenceEquals(vertex,a)||ReferenceEquals(vertex,b);
                    double lower=endpoint?.35:-.08,upper=endpoint?.73:.08,delta=end-start;
                    if(delta<0&&end<lower)safeFraction=Math.Min(safeFraction,(start-lower)/-delta);
                    if(delta>0&&end>upper)safeFraction=Math.Min(safeFraction,(upper-start)/delta);
                }
                Limit(face.A,ba,ta);Limit(face.B,bb,tb);Limit(face.C,bc,tc);
            }
            Constrain(leftFace);Constrain(rightFace);
            safeFraction=D.Clamp(safeFraction);if(safeFraction<1)safeFraction*=.98;
            px=baseX+(px-baseX)*safeFraction;py=baseY+(py-baseY)*safeFraction;pz=baseZ+(pz-baseZ)*safeFraction;
            double norm=Math.Sqrt(px*px+py*py+pz*pz);
            node.Position = new V{X=D.Q(px/norm,1e12),Y=D.Q(py/norm,1e12),Z=D.Q(pz/norm,1e12)};
            if(edge.Type==Kind.River)node.River=edge;
            else if(a.River==null)node.River=b.River;
            else if(b.River==null||ReferenceEquals(a.River,b.River))node.River=a.River;
            else node.River=SegmentDistance2(node.Position,a.River,out _)<SegmentDistance2(node.Position,b.River,out _)?a.River:b.River;
            double length = (a.Position - b.Position).Length * radius * scale;
            node.Distance = (a.Distance + b.Distance) * .5;
            bool aRiver = a.Type == Kind.River || a.Type == Kind.Lake, bRiver = b.Type == Kind.River || b.Type == Kind.Lake;
            node.ChannelKey=aRiver&&!bRiver?a.ChannelKey:bRiver&&!aRiver?b.ChannelKey:a.Flow>=b.Flow?a.ChannelKey:b.ChannelKey;
            double rough = (node.Hill + node.Mountain * .65) * (1 - .88 * node.Plateau) * (1 - .8 * node.Sediment);
            if (a.Type == Kind.Ocean && b.Type == Kind.Ocean)
            {
                node.Type = Kind.Ocean; node.Height += (u - .5) * Math.Min(25, length * .02); node.Water = 0; node.Distance = 0;
            }
            else if (a.Type == Kind.Lake && b.Type == Kind.Lake && Math.Abs(a.Water - b.Water) < .01)
            {
                node.Type = Kind.Lake; node.Water = a.Water; node.Height = Math.Min(node.Height, node.Water - 2); node.Distance = 0;
            }
            else if (edge.Type == Kind.River)
            {
                node.Type = Kind.River; node.Distance = 0;
                if (aRiver && bRiver || a.Type == Kind.Ocean || b.Type == Kind.Ocean)
                {
                    // R1. Paired bed/water interpolation preserves downhill flow.
                    // Restore the accepted lake7/ocean terrain substrate.
                    // Local pool/drop profiles now belong to the separate
                    // paired riverscape representation, not this hierarchy.
                    double t=.5;
                    node.Height=Lerp(a.Height,b.Height,t);node.Water=Lerp(a.Water,b.Water,t);
                }
                else
                {
                    // R4: a branching edge grows into a genuine headwater, with
                    // zero water depth at its source, not a flat-width runway.
                    var wet = aRiver ? a : b; var land = aRiver ? b : a;
                    node.Height = Math.Max(wet.Water + .01, Math.Min(wet.Height + length * .07, Lerp(wet.Height, land.Height, .55)));
                    node.Water = node.Height;
                    // A short local tributary cannot inherit twelve percent
                    // of a continental river's discharge. Bound it by a small
                    // contributing-area/runoff estimate in physical space.
                    node.Flow = wet.Flow*.12;
                }
                node.RiverBed = node.Height;
            }
            else if (edge.Type == Kind.Drainage)
            {
                node.Type = Kind.Drainage; node.Height = Lerp(Math.Min(a.Height, b.Height), Math.Max(a.Height, b.Height), .4 + .1 * u);
                node.RiverBed = node.Height; node.Distance = Math.Max(Width(node.Flow), length * .3);
            }
            else if (aRiver && bRiver)
            {
                // R2: an actual divide separates two rivers; it is part of the
                // recursively generated land, not a periodic ridge overlay.
                node.Height = Math.Max(a.Height, b.Height) + Math.Min(length * .2, Math.Max(3, rough)) * (.3 + .7 * (1 - u * u));
                node.Distance = length * .5;
            }
            else if (aRiver || bRiver)
            {
                // R3: river-to-upland slope, plateau-biased where appropriate.
                var wet = aRiver ? a : b; var land = aRiver ? b : a;
                double t = Lerp(u, 1 - u * u * u, node.Plateau);
                node.Height = Lerp(wet.Height, Math.Max(wet.Water + .2, land.Height), .1 + .9 * t);
                node.Water = wet.Water; node.RiverBed = wet.RiverBed;
                node.Distance = (node.Position - wet.Position).Length * radius * scale + wet.Distance;
                // A random lowered shore point is not an enclosed pond. Only
                // validated basins supply standing water; do not spawn an
                // independent hillside lake merely from an R3 random roll.
            }
            else
            {
                // R5: ported mountain-age/moisture nodes, peak node, hills and
                // plateau blend. Local relief has a physical slope envelope;
                // horizontal world compression cannot make kilometre spires.
                double h = AmplifyLand(a.Height, b.Height, node.Crust, rough, node.Hill, node.Age, node.Dryness, node.Plateau, edge.Depth, length, u, v, w);
                node.Height = h;
                if (a.Type == Kind.Ocean || b.Type == Kind.Ocean) { node.Water = 0; node.Distance = Math.Abs(h); }
            }
            if(node.Type==Kind.Terrain||node.Type==Kind.Drainage)
            {
                // Detail must refine the parent surface, not repeatedly redraw
                // its entire elevation interval. In particular R3's random
                // river-bank interpolation otherwise makes jagged slopes at
                // every subdivision, even on floodplains and plateau interiors.
                Face parent=null;double best=double.NegativeInfinity,plane=node.Height;
                void ParentPlane(Face face)
                {
                    if(face==null)return;
                    face.Coordinates(node.Position,out double x,out double y,out double z);
                    double inside=Math.Min(x,Math.Min(y,z));
                    if(inside>best){best=inside;parent=face;plane=x*face.A.Height+y*face.B.Height+z*face.C.Height;}
                }
                ParentPlane(leftFace);ParentPlane(rightFace);
                if(parent!=null)
                {
                    double activity=OrogenMorphology.Smooth(12,100,node.Hill+node.Mountain*.25);
                    double gain=(.12+.68*activity)*(1-.8*node.Plateau)*(1-.6*node.Sediment)
                        *Lerp(1,.28,OrogenMorphology.Smooth(2,7,edge.Depth));
                    // A long edge of a sliver triangle is not permission for a
                    // cliff: bound new detail by the incident face's altitude.
                    double altitude=double.MaxValue;
                    void LimitSlope(Face face)
                    {
                        if(face==null)return;var oppositeNode=face.Opposite(edge);
                        double nx=a.Position.Y*b.Position.Z-a.Position.Z*b.Position.Y;
                        double ny=a.Position.Z*b.Position.X-a.Position.X*b.Position.Z;
                        double nz=a.Position.X*b.Position.Y-a.Position.Y*b.Position.X;
                        double norm2=nx*nx+ny*ny+nz*nz;
                        if(norm2>1e-30)altitude=Math.Min(altitude,Math.Abs(oppositeNode.Position.X*nx+oppositeNode.Position.Y*ny+oppositeNode.Position.Z*nz)/Math.Sqrt(norm2)*radius*scale);
                    }
                    LimitSlope(leftFace);LimitSlope(rightFace);
                    double limit=Math.Min(length*.35,altitude*.45);
                    node.Height=plane+Math.Max(-limit,Math.Min(limit,(node.Height-plane)*gain));
                    if(node.Type==Kind.Drainage)node.RiverBed=node.Height;
                }
            }
            if(a.Height>=0&&b.Height>=0&&node.Type!=Kind.Lake)node.Height=Math.Max(.001,node.Height);
            PreserveLakeRim(node,leftFace,rightFace);
            if(!double.IsNaN(edge.BasinLevel))
            {
                // The shared divide itself must stay dry even when lateral
                // edge displacement moves it outside the original root face.
                double baseline=fraction*a.Height+(1-fraction)*b.Height;
                node.BasinFloor=Math.Max(node.BasinFloor,edge.BasinLevel+.2*Math.Max(0,baseline-edge.BasinLevel));
                node.Height=Math.Max(node.Height,node.BasinFloor);
            }
            return node;
        }

        /// <summary>Adapted edgeSplit.comp procedural land nodes; metres throughout.</summary>
        public static double AmplifyLand(double a, double b, double crust, double rough, double hills, double age, double dry, double plateau,
            int depth, double length, double u, double v, double w)
        {
            double low = Math.Min(a, b), high = Math.Max(a, b);
            double lod = D.Clamp(depth / 10.0), spread = 1 - .4 * lod;
            double old = D.Clamp(OrogenMorphology.Smooth(0, 200, age) + .38 * dry);
            double oldDry = Lerp(low, high, .5 - .4 * spread + .4 * spread * u);
            double oldWet = Lerp(low, high, 1 - .6 * spread + .6 * spread * u);
            double young = Lerp(low, high, .5 - .5 * spread + spread * Math.Pow(u, 1.5));
            double mountain = Lerp(young, Lerp(oldWet, oldDry, dry), old * (1 - lod));
            // The original peak node samples the whole sea-to-crust interval.
            // Our input is a mean elevation map (not a crust maximum), so use
            // the same age/wetness shapes within a local physical relief budget.
            double peakShape = Lerp(v * v, Lerp(1 - v * v, v, dry), old);
            double peak = (a + b) * .5 + (peakShape - .45) * 2 * Math.Min(rough, length * .20);
            double hill = (1 - w * w) * (u < .5 ? -.15 : .5) * Math.Min(length, hills * 3);
            double fading = 1 - OrogenMorphology.Smooth(4, 11, depth);
            double h = Lerp(mountain, peak + hill * fading, fading * (1 - .88 * plateau));
            double offset = Math.Min(length * .16, rough * .35) * (u - .5) * (1 - .9 * plateau);
            // Avoid aliasing-looking microspikes on flat controls.
            return h + offset;
        }
        static double Width(double flow) => Math.Min(600, Math.Max(1, 2.4 * Math.Sqrt(Math.Max(0, flow))));
        static double Depth(double flow) => Math.Min(15, .3 + .6 * Math.Pow(Math.Max(0, flow), .3));

        struct WaterProfile
        {
            public double Height,Water;
            public LocalWaterKind Kind;
        }
        // Terrain-only bed/bank profiles at shared hierarchy vertices.
        // Water surfaces are selected independently below from basin/reach
        // data. Never blend their elevation toward the dry ground field.
        static WaterProfile Profile(Vertex vertex)
        {
            double height=vertex.Height,water=vertex.Water;
            if(vertex.SeaWeight>0&&height<0)return new WaterProfile{Height=height,Water=0,Kind=LocalWaterKind.Ocean};
            if(vertex.Type==Kind.Lake)return new WaterProfile{Height=height,Water=vertex.LakeKey!=0?vertex.LakeLevel:water,Kind=LocalWaterKind.Lake};
            bool lake=vertex.LakeKey!=0&&(vertex.Type!=Kind.River||water<=vertex.LakeLevel+.01);
            // A standing basin cannot retire its water datum toward the dry
            // ground: that creates an uphill ramp across its submerged shore.
            // Retain the lake's supplied level through its inherited basin;
            // the unraised terrain, not a distance collar, finds the bank.
            if(lake)return new WaterProfile{Height=height,Water=vertex.LakeLevel,Kind=LocalWaterKind.Lake};
            bool supplied=vertex.River!=null||lake||vertex.Type==Kind.River;
            if(!supplied)return new WaterProfile{Height=height,Water=water,Kind=LocalWaterKind.None};
            double depth=Math.Max(0,water-vertex.RiverBed);
            double half=Width(vertex.Flow)*Math.Sqrt(D.Clamp(depth/Math.Max(.001,Depth(vertex.Flow))));
            double extent=Math.Max(half*5,Math.Min(3000,80+vertex.Mountain*.9+vertex.Sediment*400));
            // Only TERRAIN blends at the shaping collar. Never move water
            // toward terrain to force a bank intersection.
            double raw=height;
            double distance=vertex.Distance;
            if(half>.001&&distance<extent)
            {
                double profile;
                if(distance<=half)profile=water-depth*(1-OrogenMorphology.Smooth(0,half,distance));
                else profile=water+.25+(distance-half)*Lerp(.061,.25,D.Clamp(vertex.Mountain/2000));
                double blend=OrogenMorphology.Smooth(Math.Max(half,extent*.25),extent,distance);
                height=Lerp(Math.Min(raw,profile),raw,blend);
            }
            height=Math.Max(height,vertex.BasinFloor);
            return new WaterProfile{Height=height,Water=water,Kind=lake?LocalWaterKind.Lake:LocalWaterKind.River};
        }

        static double SegmentDistance2(V point,Edge edge,out double along)
        {
            double x=edge.B.Position.X-edge.A.Position.X,y=edge.B.Position.Y-edge.A.Position.Y,z=edge.B.Position.Z-edge.A.Position.Z;
            double px=point.X-edge.A.Position.X,py=point.Y-edge.A.Position.Y,pz=point.Z-edge.A.Position.Z;
            along=D.Clamp((px*x+py*y+pz*z)/Math.Max(1e-30,x*x+y*y+z*z));
            px-=along*x;py-=along*y;pz-=along*z;return px*px+py*py+pz*pz;
        }
        struct RiverHit {public double Square,Along;public Edge Edge;}
        void NearestRiver(V point,Edge edge,double maximumDistance,ref RiverHit best,int depth=0)
        {
            if(edge==null||edge.Type!=Kind.River)return;
            double square=SegmentDistance2(point,edge,out double along);
            double length=(edge.A.Position-edge.B.Position).Length;
            // Safe lower bound includes all future lateral midpoint offsets.
            if(Math.Sqrt(square)-length*.3>Math.Min(maximumDistance,Math.Sqrt(best.Square)))return;
            if(edge.Depth>=terminalDepth||depth>=20)
            {if(square<best.Square){best.Square=square;best.Edge=edge;best.Along=along;}return;}
            var split=Split(edge);
            if(along<.5){NearestRiver(point,split.First,maximumDistance,ref best,depth+1);NearestRiver(point,split.Last,maximumDistance,ref best,depth+1);}
            else{NearestRiver(point,split.Last,maximumDistance,ref best,depth+1);NearestRiver(point,split.First,maximumDistance,ref best,depth+1);}
        }

        /// <summary>Position on the actual recursively displaced major channel,
        /// for inspection/site selection. Does not trace or solve a catchment.</summary>
        public V RiverPoint(int from,double fraction)
        {
            if(from<0||from>=nodeChannels.Length||nodeChannels[from]<0)throw new ArgumentOutOfRangeException(nameof(from));
            var edge=RootEdge(nodeChannels[from]);
            // Root vertex keys are sufficient to determine source orientation.
            ulong sourceKey=Mix((ulong)(uint)seed<<32|(uint)from);
            double t=ReferenceEquals(edge.A,edge.B)||edge.A.Key==sourceKey?D.Clamp(fraction):1-D.Clamp(fraction);
            for(int depth=0;depth<20&&edge.Depth<terminalDepth;depth++)
            {
                var parts=Split(edge);
                if(t<=.5){edge=parts.First;t*=2;}else{edge=parts.Last;t=t*2-1;}
            }
            return (edge.A.Position*(1-t)+edge.B.Position*t).Unit;
        }

        internal sealed partial class Patch
        {
            readonly HyperAmplification owner;
            readonly int[] faces;
            Face last;
            public Patch(HyperAmplification owner, int[] triangleOffsets)
            {
                this.owner=owner;faces=new int[triangleOffsets.Length];
                for(int i=0;i<faces.Length;i++)faces[i]=triangleOffsets[i]/3;
            }
            public Surface Sample(V point,bool bare=false)
            {
                Face found = null; double a = 0, b = 0, c = 0;
                if (last != null && last.Coordinates(point, out a, out b, out c) >= -1e-7) found = last;
                if(found==null)foreach(int id in faces)if(owner.roots[id].Coordinates(point,out a,out b,out c)>=-1e-7){found=owner.RootFace(id);break;}
                if (found == null) throw new InvalidOperationException("Hyper-amplification patch has no containing base face.");
                last = found;
                while (found.Depth < owner.terminalDepth)
                {
                    var split = owner.Split(found); Face next = null; double best = -1e10;
                    double ba = 0, bb = 0, bc = 0;
                    foreach (var child in split.Faces)
                    {
                        double distance = child.Coordinates(point, out double x, out double y, out double z);
                        if (distance > best) { best = distance; next = child; ba = x; bb = y; bc = z; }
                        if (distance >= -1e-7) break;
                    }
                    // Bent child boundaries need point-location on the refined
                    // mesh, not containment in the obsolete straight parent.
                    // Walk the *child's* crossed edge; the nearest parent edge
                    // is not necessarily the boundary crossed by that child.
                    for(int walk=0;best< -1e-6&&walk<32;walk++)
                    {
                        Edge boundary = ba <= bb && ba <= bc ? next.BC : bb <= bc ? next.CA : next.AB;
                        if(boundary.Parent!=null)
                        {
                            lock(owner.gate)
                            {
                                if(boundary.Parent.Left!=null)owner.Split(boundary.Parent.Left.Resolve(owner));
                                if(boundary.Parent.Right!=null)owner.Split(boundary.Parent.Right.Resolve(owner));
                            }
                        }
                        Face neighbour = null;
                        if (boundary.Left != null && !boundary.Left.Matches(next)) neighbour = boundary.Left.Resolve(owner);
                        if (boundary.Right != null && !boundary.Right.Matches(next)) neighbour = boundary.Right.Resolve(owner);
                        if(neighbour==null)throw new InvalidOperationException("Missing neighbour in amplified point location.");
                        next=neighbour;best=next.Coordinates(point,out ba,out bb,out bc);
                    }
                    if(best< -1e-4)throw new InvalidOperationException("Amplified point location did not converge: depth "+found.Depth+", size "+found.Size+", best "+best+", abc "+ba+","+bb+","+bc+", point "+point.X+","+point.Y+","+point.Z+", next depth "+next.Depth+", next size "+next.Size);
                    found = next; a = ba; b = bb; c = bc;
                }
                // Neighbouring fine-mesh samples, standing still and repeated
                // controller/sky queries commonly remain in this same terminal
                // face. Keep it (and its leases), not just its coarse ancestor:
                // no repeated root-to-leaf walk or locking of background splits.
                last=found;
                a = Math.Max(0, a); b = Math.Max(0, b); c = Math.Max(0, c); double sum = a + b + c; a /= sum; b /= sum; c /= sum;
                var pa=Profile(found.A);var pb=Profile(found.B);var pc=Profile(found.C);
                double pre=a*found.A.Height+b*found.B.Height+c*found.C.Height;
                double height=a*pa.Height+b*pb.Height+c*pc.Height;
                // Preserve accepted standing-water ground exactly; elsewhere
                // the paired riverscape primitives, not inherited broad river
                // potentials, own local channel shaping.
                int standingBasin=owner.lakeFaces[found.Root];
                var standingRoot=owner.roots[found.Root];
                bool standingOcean=owner.rootNodes[standingRoot.A].Type==Kind.Ocean||owner.rootNodes[standingRoot.B].Type==Kind.Ocean||owner.rootNodes[standingRoot.C].Type==Kind.Ocean;
                if(standingBasin<0&&!standingOcean)height=pre;
                double water=double.NaN,minimum=double.NaN,maximum=double.NaN,footprint=-1e6,mask=-1e6;
                V gradient=default,waterFlow=default;double speed=0,turbulence=0;
                var kind=LocalWaterKind.None;ulong bodyKey=0;
                // Connected basin ownership includes its dry bank collar; do
                // not terminate the surface where inherited vertex IDs vanish.
                int basin=owner.lakeFaces[found.Root];
                if(basin>=0&&(height<owner.lakeLevels[basin]||double.IsNaN(water)||mask<0))
                {water=owner.lakeLevels[basin];kind=LocalWaterKind.Lake;bodyKey=(ulong)(uint)(basin+1);gradient=default;minimum=maximum=water;footprint=double.PositiveInfinity;mask=water-height;}
                // Preserve the ocean checkpoint: source-connected submerged
                // shelves are sea even when refined nodes are ordinary terrain.
                var root=owner.roots[found.Root];
                bool ocean=owner.rootNodes[root.A].Type==Kind.Ocean||owner.rootNodes[root.B].Type==Kind.Ocean||owner.rootNodes[root.C].Type==Kind.Ocean;
                if(ocean&&(height<0||kind==LocalWaterKind.None))
                {water=0;kind=LocalWaterKind.Ocean;bodyKey=1;gradient=default;minimum=maximum=0;footprint=double.PositiveInfinity;mask=-height;}
                double influence=OrogenMorphology.Smooth(0,.04,a*found.A.Flow+b*found.B.Flow+c*found.C.Flow);
                var result=new Surface{Height=height,UnshapedHeight=pre,Water=water,Mask=mask,RiverInfluence=influence,
                    Kind=kind,BodyKey=bodyKey,Gradient=gradient,Footprint=footprint,Minimum=minimum,Maximum=maximum,
                    Flow=waterFlow,Speed=speed,Turbulence=turbulence};
                if(!bare)ApplyRiverscape(point,ref result);
                return result;
            }
        }
        internal struct Surface
        {
            public double Height, UnshapedHeight, Water, Mask, RiverInfluence,Footprint,Minimum,Maximum,Speed,Turbulence;
            public LocalWaterKind Kind;public ulong BodyKey;public V Gradient,Flow;
        }
        internal Patch Region(int[] triangleOffsets) => new Patch(this, triangleOffsets);
    }
}
