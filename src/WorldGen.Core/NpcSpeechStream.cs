using System;
using System.Text;

namespace WorldGen.Core
{
    // Extract only a top-level JSON speech string, even when a token splits an
    // escape sequence. Intentions and incomplete JSON never become actions.
    public sealed class NpcSpeechStream
    {
        readonly StringBuilder raw=new StringBuilder(),text=new StringBuilder(),token=new StringBuilder();
        bool quoted,escape,key,value,colon;int depth,unicode;string property="";int hex;
        public string Json=>raw.ToString();
        public string Speech=>text.Length>0&&char.IsHighSurrogate(text[text.Length-1])?text.ToString(0,text.Length-1):text.ToString();
        public void Append(string fragment)
        {
            if(fragment==null)return;if(raw.Length+fragment.Length>32768)throw new ArgumentException("NPC response too large");
            raw.Append(fragment);
            foreach(char c in fragment)
            {
                if(quoted)
                {
                    if(unicode>0){int n=c>='0'&&c<='9'?c-'0':c>='a'&&c<='f'?c-'a'+10:c>='A'&&c<='F'?c-'A'+10:-1;if(n<0)throw new ArgumentException("Invalid streamed JSON escape");hex=hex*16+n;if(--unicode==0)Add((char)hex);continue;}
                    if(escape){escape=false;if(c=='u'){unicode=4;hex=0;}else Add(c=='n'?' ':c=='r'?' ':c=='t'?' ':c=='b'?' ':c=='f'?' ':c);continue;}
                    if(c=='\\'){escape=true;continue;}
                    if(c=='"'){quoted=false;if(key){property=token.ToString();key=false;colon=true;}value=false;continue;}
                    Add(c);continue;
                }
                if(c=='{'||c=='['){depth++;continue;}if(c=='}'||c==']'){depth--;continue;}
                if(c==':'&&colon){colon=false;continue;}
                if(c==','&&depth==1){property="";continue;}
                if(c=='"'){quoted=true;token.Clear();key=depth==1&&property.Length==0;value=depth==1&&!key&&property=="speech";}
            }
        }
        void Add(char c){if(key)token.Append(c);if(value&&text.Length<280)text.Append(char.IsControl(c)?' ':c);}
    }

    public sealed class NpcSpeechPace
    {
        public string Available="";public int Shown;double credit;
        public bool Complete=>Shown>=Available.Length;
        public string Visible=>Available.Substring(0,Shown);
        public void Advance(double seconds,double charactersPerSecond=24)
        {
            credit=Math.Min(3,credit+Math.Max(0,seconds)*charactersPerSecond);
            int count=Math.Min((int)credit,Available.Length-Shown);
            if(count>0&&Shown+count<Available.Length&&char.IsHighSurrogate(Available[Shown+count-1]))count--;
            credit-=count;Shown+=count;
        }
    }
}
