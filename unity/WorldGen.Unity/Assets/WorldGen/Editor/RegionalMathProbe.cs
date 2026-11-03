using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEditor;
using WorldGen.Core;

public static class RegionalMathProbe
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)] static double Clamp(double x)=>x<0?0:x>1?1:x;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] static double Quantize(double x)=>Math.Floor(x*1e9+.5)/1e9;
    public static void Run()
    {
        var text=new System.Text.StringBuilder();const int n=2000000;
        void Measure(string name,Func<double,double> f){double sum=0;var timer=Stopwatch.StartNew();for(int i=0;i<n;i++)sum+=f((i%10007)/10001d);text.AppendLine(name+" "+timer.Elapsed.TotalMilliseconds+"ms sum="+sum);}
        Measure("D.Clamp",x=>D.Clamp(x));Measure("conditional clamp",Clamp);
        Measure("D.Q",x=>D.Q(x));Measure("floor quantization",Quantize);
        Measure("Math.Exp",Math.Exp);Measure("Math.Pow",x=>Math.Pow(x,.75));
        Directory.CreateDirectory("../../artifacts/regional-sim-20261103");File.WriteAllText("../../artifacts/regional-sim-20261103/mono-math-probe.txt",text.ToString());EditorApplication.Exit(0);
    }
}
