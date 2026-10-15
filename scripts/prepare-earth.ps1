# Reproducible conversion of the documented NOAA ERDDAP CSV subset into a bundled asset.
param([string]$InputCsv = "$PSScriptRoot/../artifacts/earth-data/etopo2022-10min.csv")
$ErrorActionPreference = 'Stop'
$destination = [IO.Path]::GetFullPath("$PSScriptRoot/../src/WorldGen.Core/Data/earth-etopo2022-10min.bin")
New-Item -ItemType Directory -Force ([IO.Path]::GetDirectoryName($destination)) | Out-Null
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Globalization;
public static class EarthGridConverter {
 public static void Convert(string input, string output) {
  using(var reader = new StreamReader(input)) using(var stream = File.Create(output)) using(var writer = new BinaryWriter(stream)) {
   if(reader.ReadLine() != "latitude,longitude,z") throw new Exception("Unexpected NOAA columns");
   reader.ReadLine(); writer.Write(2160); writer.Write(1080);
   writer.Write(-89.90833333333333); writer.Write(0.09166666666666856); writer.Write(1.0/6);
   int count=0; string line;
   while((line=reader.ReadLine())!=null) {
    var parts=line.Split(','); if(parts.Length!=3) throw new Exception("Invalid source row");
    double latitude=double.Parse(parts[0],CultureInfo.InvariantCulture), longitude=double.Parse(parts[1],CultureInfo.InvariantCulture);
    double value=double.Parse(parts[2],CultureInfo.InvariantCulture);
    if(Math.Abs(latitude-(-89.90833333333333+(count/2160)/6.0))>1e-6 || Math.Abs(longitude-(0.09166666666666856+(count%2160)/6.0))>1e-6) throw new Exception("Grid coordinate/order mismatch");
    if(double.IsNaN(value)||double.IsInfinity(value)||value < -12000 || value>9000) throw new Exception("Invalid/missing elevation");
    writer.Write((short)Math.Round(value,MidpointRounding.AwayFromZero)); count++;
   }
   if(count!=2160*1080) throw new Exception("Incomplete global grid");
  }
 }
}
'@
[EarthGridConverter]::Convert([IO.Path]::GetFullPath($InputCsv), $destination)
Get-FileHash -LiteralPath $destination -Algorithm SHA256
