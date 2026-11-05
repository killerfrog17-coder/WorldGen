using System;
using System.Collections.Generic;
using System.IO;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Worker-owned external sound catalogue snapshot. Poll metadata
    /// without reconstructing unchanged catalogue strings, banks and lists.
    /// Missing/replaced files and explicit reloads still rebuild the snapshot.</summary>
    public sealed class WildlifeSoundFileIndex
    {
        sealed class Stamp
        {
            public readonly FileInfo Info;readonly bool exists;readonly long bytes,ticks;
            public Stamp(string path){Info=new FileInfo(path);exists=Info.Exists;bytes=exists?Info.Length:0;ticks=exists?Info.LastWriteTimeUtc.Ticks:0;}
            public bool Changed(){Info.Refresh();bool present=Info.Exists;return present!=exists||present&&(Info.Length!=bytes||Info.LastWriteTimeUtc.Ticks!=ticks);}
            public bool ValidAudio=>exists&&bytes>=100&&bytes<=12*1024*1024;
            public bool Exists=>exists;
        }
        readonly Stamp manifest;
        readonly List<Stamp> files=new List<Stamp>();
        public readonly string Root;
        public readonly WildlifeSoundCatalogue Catalogue=new WildlifeSoundCatalogue();
        public readonly int Missing;
        WildlifeSoundFileIndex(string root)
        {
            Root=root;manifest=new Stamp(Path.Combine(root,"library.tsv"));
            if(!manifest.Exists)return;
            var parsed=WildlifeSoundCatalogue.Parse(File.ReadAllText(manifest.Info.FullName));
            foreach(var entry in parsed.Takes)
            {
                var stamp=new Stamp(Path.Combine(root,entry.File));files.Add(stamp);
                if(stamp.ValidAudio)Catalogue.Add(entry);else Missing++;
            }
        }
        bool Changed(string root)
        {
            if(!string.Equals(root,Root,StringComparison.Ordinal)||manifest.Changed())return true;
            foreach(var file in files)if(file.Changed())return true;
            return false;
        }
        public static WildlifeSoundFileIndex Scan(string root,WildlifeSoundFileIndex? previous=null)
            =>previous!=null&&!previous.Changed(root)?previous:new WildlifeSoundFileIndex(root);
    }
}
