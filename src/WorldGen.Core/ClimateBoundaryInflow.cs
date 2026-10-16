using System;
using System.Threading;

namespace WorldGen.Core
{
    [Serializable] public sealed class ClimateBoundaryInflow
    {
        // Rayleigh lower-layer relaxation and a transparent experimental gain.
        // Units are hours and dimensionless; no Earth-region coefficients.
        public double DragHours=24,Strength=1;
        public void Validate()
        {
            if(double.IsNaN(DragHours)||double.IsInfinity(DragHours)||DragHours<6||DragHours>96||double.IsNaN(Strength)||double.IsInfinity(Strength)||Strength<0||Strength>4)
                throw new ArgumentException("Invalid experimental boundary-layer inflow profile.");
            DragHours=D.Q(DragHours,1000000);Strength=D.Q(Strength,1000000);
        }
        public ClimateBoundaryInflow Copy()=>(ClimateBoundaryInflow)MemberwiseClone();
    }
    internal static class AtmosphericBoundaryInflow
    {
        internal static V Response(V pressureGradient,double latitude,ClimateBoundaryInflow profile)
        {
            double drag=1/(Math.Max(6,profile?.DragHours??24)*3600),strength=profile?.Strength??1;
            double f=.000146*D.Sin(latitude);
            // Finite equatorial boundary-layer response; the geostrophic gyre
            // remains separately represented, so no extra rotational term.
            double scale=-drag*strength/(1.2*(drag*drag+f*f+.000015*.000015));
            return pressureGradient*scale;
        }
        internal static V[] Solve(Surface surface,double[] thermalPressure,ClimateBoundaryInflow profile,CancellationToken token)
        {
            int n=surface.Cells.Length;var flow=new V[n];double maximum=0;
            for(int i=0;i<n;i++)
            {
                if((i&255)==0)token.ThrowIfCancellationRequested();
                var cell=surface.Cells[i];V east=new V(-cell.Position.Y,cell.Position.X,0).Unit;
                if(east.Length<.1)east=new V(0,1,0);V north=V.Cross(cell.Position,east).Unit;
                double xx=.000001,xy=0,yy=.000001,gx=0,gy=0;
                for(int k=0;k<cell.Neighbors.Length;k++)
                {
                    int j=cell.Neighbors[k];V direction=V.Tangent(cell.Position,surface.Cells[j].Position);
                    double x=V.Dot(direction,east),y=V.Dot(direction,north),derivative=(thermalPressure[j]-thermalPressure[i])/Math.Max(1,cell.DistanceM[k]);
                    xx+=x*x;xy+=x*y;yy+=y*y;gx+=x*derivative;gy+=y*derivative;
                }
                double determinant=xx*yy-xy*xy;
                V gradient=east*((gx*yy-gy*xy)/determinant)+north*((gy*xx-gx*xy)/determinant);
                double latitude=D.Pi/2-D.Angle(cell.Position,new V(0,0,1));
                flow[i]=Response(gradient,latitude,profile);maximum=Math.Max(maximum,flow[i].Length);
            }
            // One global scaling keeps a smooth pressure-gradient field rather
            // than independently clipping cells and creating artificial fronts.
            double limit=maximum>6?6/maximum:1;
            for(int i=0;i<n;i++)flow[i]*=limit/5; // existing wind unit=5m/s
            return flow;
        }
    }
}
