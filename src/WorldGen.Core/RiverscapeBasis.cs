namespace WorldGen.Core
{
    // Immutable reach geometry. Keep the original arithmetic, but prepare the
    // point-independent vectors once instead of rebuilding them at every query.
    internal readonly struct RiverscapeBasis
    {
        public readonly V D,TangentA,TangentB,Axis,Side;
        public readonly double DD,BX,BZ,LAX,LAZ,LBX,LBZ,RAX,RAZ,RBX,RBZ;
        static double Dot(V a,V b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
        public RiverscapeBasis(RiverscapeSpan span,double radius)
        {
            D=span.B-span.A;DD=Dot(D,D);
            TangentA=V.Cross(span.SideA,span.A).Unit;TangentB=V.Cross(span.SideB,span.B).Unit;
            Axis=(D-span.A*Dot(D,span.A)).Unit;Side=V.Cross(span.A,Axis).Unit;
            BX=Dot(D,Axis)*radius;BZ=Dot(D,Side)*radius;
            LAX=Dot(span.SideA,Axis)*span.WidthA;LAZ=Dot(span.SideA,Side)*span.WidthA;
            LBX=BX+Dot(span.SideB,Axis)*span.WidthB;LBZ=BZ+Dot(span.SideB,Side)*span.WidthB;
            RAX=-LAX;RAZ=-LAZ;RBX=2*BX-LBX;RBZ=2*BZ-LBZ;
        }
    }
}
