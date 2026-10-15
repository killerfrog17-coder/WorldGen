using System;
using System.Collections.Generic;
namespace WorldGen.Core
{
        public sealed class CellLocator
        {
            readonly Cell[] cells; readonly int[] order;
            public CellLocator(Cell[] cells)
            {
                this.cells = cells; order = new int[cells.Length];
                for (int i = 0; i < order.Length; i++) order[i] = i;
                Split(0, order.Length, 0);
            }
            static double Axis(V p, int axis) => axis == 0 ? p.X : axis == 1 ? p.Y : p.Z;
            void Split(int start, int count, int axis)
            {
                if (count <= 1) return;
                Array.Sort(order, start, count, Comparer<int>.Create((a, b) =>
                { int result = Axis(cells[a].Position, axis).CompareTo(Axis(cells[b].Position, axis)); return result == 0 ? a.CompareTo(b) : result; }));
                int half = count / 2;
                Split(start, half, (axis + 1) % 3); Split(start + half + 1, count - half - 1, (axis + 1) % 3);
            }
            public int Nearest(V point)
            {
                int best = 0; double distance = double.PositiveInfinity;
                void Visit(int start, int count, int axis)
                {
                    if (count == 0) return;
                    int half = count / 2, middle = start + half, id = order[middle]; V p = cells[id].Position;
                    double x = p.X - point.X, y = p.Y - point.Y, z = p.Z - point.Z, d = x * x + y * y + z * z;
                    if (d < distance || d == distance && id < best) { best = id; distance = d; }
                    double delta = Axis(point, axis) - Axis(p, axis); int next = (axis + 1) % 3;
                    if (delta < 0)
                    { Visit(start, half, next); if (delta * delta <= distance) Visit(middle + 1, count - half - 1, next); }
                    else
                    { Visit(middle + 1, count - half - 1, next); if (delta * delta <= distance) Visit(start, half, next); }
                }
                Visit(0, order.Length, 0); return best;
            }
            // Immutable spatial index; ordered output makes local feature application deterministic.
            public int[] Within(V point, double squaredChord)
            {
                var found = new List<int>();
                void Visit(int start,int count,int axis)
                {
                    if(count==0)return;
                    int half=count/2,middle=start+half,id=order[middle];V delta=cells[id].Position-point;
                    if(V.Dot(delta,delta)<=squaredChord)found.Add(id);
                    double d=Axis(point,axis)-Axis(cells[id].Position,axis);int next=(axis+1)%3;
                    if(d<=0 || d*d<=squaredChord)Visit(start,half,next);
                    if(d>=0 || d*d<=squaredChord)Visit(middle+1,count-half-1,next);
                }
                Visit(0,order.Length,0);found.Sort();return found.ToArray();
            }
        }
}
