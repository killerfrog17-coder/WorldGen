using System;
using UnityEngine;
using WorldGen.Core;

static partial class LocalScatterMeshes
{
    public static Mesh BuildPortable(Possession item)
    {
        var d=PortableTechnology.Item(item.Kind);var b=new Builder();
        Color wood=new Color(.43f,.28f,.13f,0),bone=new Color(.87f,.8f,.59f,0),cord=new Color(.58f,.45f,.25f,0),leather=new Color(.55f,.34f,.19f,0),fur=new Color(.83f,.7f,.53f,1);
        V pigment=ToolStone.Color(item.Variant,new V(.43,.43,.4));Color stone=new Color((float)pigment.X,(float)pigment.Y,(float)pigment.Z,0);
        void Quad(Vector3 a,Vector3 c,Vector3 e,Vector3 f,Color color)
        {int i=b.Add(a,color,new Vector2(0,0)),j=b.Add(c,color,new Vector2(1,0)),k=b.Add(e,color,new Vector2(1,1)),l=b.Add(f,color,new Vector2(0,1));b.Triangle(i,j,k);b.Triangle(i,k,l);}
        void Tube(Vector3 a,Vector3 c,float r,Color color,int sides=8)
        {
            var axis=(c-a).normalized;var u=Vector3.Cross(axis,Mathf.Abs(axis.y)<.9f?Vector3.up:Vector3.right).normalized;var v=Vector3.Cross(axis,u);int rings=Vector3.Distance(a,c)>.3f?4:1,first=b.V.Count;
            for(int j=0;j<=rings;j++)for(int i=0;i<=sides;i++)
            {float t=j/(float)rings,angle=i*Mathf.PI*2/sides;float radius=r*(1-.07f*t)*(1+.025f*Mathf.Sin(i*2.3f+j*.7f));b.Add(Vector3.Lerp(a,c,t)+(u*Mathf.Cos(angle)+v*Mathf.Sin(angle))*radius,color,new Vector2(i/(float)sides,t));}
            for(int j=0;j<rings;j++)for(int i=0;i<sides;i++){int p=first+j*(sides+1)+i;b.Triangle(p,p+1,p+sides+1);b.Triangle(p+1,p+sides+2,p+sides+1);}
            int ca=b.Add(a,color,new Vector2(.5f,0)),cc=b.Add(c,color,new Vector2(.5f,1));for(int i=0;i<sides;i++){b.Triangle(ca,first+i+1,first+i);int p=first+rings*(sides+1)+i;b.Triangle(cc,p,p+1);}
        }
        void Arc(Vector3 center,float rx,float rz,float y,Color color,float thickness=.012f,int steps=18)
        {for(int i=0;i<steps;i++){float a=i*Mathf.PI*2/steps,c=(i+1)*Mathf.PI*2/steps;Tube(center+new Vector3(Mathf.Cos(a)*rx,y,Mathf.Sin(a)*rz),center+new Vector3(Mathf.Cos(c)*rx,y,Mathf.Sin(c)*rz),thickness,color);}}
        void Head(float y,float height,float width,Color color,bool barbed=false)
        {
            const int segments=24;var rim=new Vector3[segments];var shoulder=new Vector3[segments];bool round=d.Form==PortableForm.Chopper||item.Kind==PossessionKind.StoneScraper;
            for(int i=0;i<segments;i++){float angle=i*Mathf.PI*2/segments,t=(1+Mathf.Cos(angle))*.5f;float taper=round?1:.35f+.65f*(1-t);float chip=1-.04f*Mathf.Sin(i*2.1f+item.Variant);rim[i]=new Vector3(Mathf.Sin(angle)*width*.5f*taper*chip,y+t*height,0);shoulder[i]=new Vector3(rim[i].x*.63f,y+(t*.7f+.13f)*height,.12f+.04f*Mathf.Sin(i*1.9f));}
            for(int side=0;side<2;side++)for(int i=0;i<segments;i++)
            {
                int j=(i+1)%segments;float sign=side==0?1:-1;Vector3 r0=rim[i],r1=rim[j],s0=shoulder[i],s1=shoulder[j];s0.z*=sign;s1.z*=sign;
                void Facet(Vector3 p0,Vector3 p1,Vector3 p2){int a=b.Add(p0,color,new Vector2(p0.x/width+.5f,(p0.y-y)/height)),c=b.Add(p1,color,new Vector2(p1.x/width+.5f,(p1.y-y)/height)),e=b.Add(p2,color,new Vector2(p2.x/width+.5f,(p2.y-y)/height));if(side==0)b.Triangle(a,c,e);else b.Triangle(a,e,c);}
                Facet(r0,r1,s0);Facet(r1,s1,s0);Facet(s0,s1,new Vector3(0,y+height*.46f,.19f*sign));
            }
            if(barbed)for(int i=0;i<4;i++){float h=y+height*(.18f+i*.17f);Quad(new Vector3(-.25f,h,0),new Vector3(-.58f,h-.05f,0),new Vector3(-.27f,h+.09f,0),new Vector3(-.2f,h+.05f,0),color);}
        }
        void Wrap(float y,float radius,int count=5)
        {
            for(int i=0;i<count*24;i++){float a=i*Mathf.PI*2/24,c=(i+1)*Mathf.PI*2/24;Tube(new Vector3(Mathf.Cos(a)*radius,y+i*.00065f,Mathf.Sin(a)*radius),new Vector3(Mathf.Cos(c)*radius,y+(i+1)*.00065f,Mathf.Sin(c)*radius),.014f,cord,5);}
            Tube(new Vector3(radius,y,0),new Vector3(-radius,y+count*.015f,radius*.3f),.018f,cord,5);
        }
        void AxeHead(bool adze)
        {
            // Broad flaked cutting edge, thick poll and convex cheeks. The axe
            // cuts along its handle plane; an adze presents a transverse edge.
            Vector3 P(float x,float y,float z)=>adze?new Vector3(z,y,x*1.6f):new Vector3(x,y,z);
            Vector3 poll=P(-.46f,.84f,0),top=P(.06f,.99f,0),edgeTop=P(.5f,.94f,0),edgeMid=P(.56f,.82f,0),edgeBottom=P(.48f,.7f,0),bottom=P(-.04f,.69f,0);
            var rim=new[]{poll,top,edgeTop,edgeMid,edgeBottom,bottom};
            for(int side=0;side<2;side++)
            {
                var ridge=P(-.02f,.84f,(side==0?1:-1)*.32f);
                for(int i=0;i<rim.Length;i++)
                {
                    int j=(i+1)%rim.Length;Vector3 shoulder=Vector3.Lerp((rim[i]+rim[j])*.5f,ridge,.42f);
                    shoulder+=P(0,0,(side==0?1:-1)*.025f*Mathf.Sin(i*2.4f));
                    void F(Vector3 a,Vector3 c,Vector3 e)
                    {int v=b.Add(a,stone,new Vector2(a.x+.5f,(a.y-.69f)/.3f)),w=b.Add(c,stone,new Vector2(c.x+.5f,(c.y-.69f)/.3f)),q=b.Add(e,stone,new Vector2(e.x+.5f,(e.y-.69f)/.3f));if(side==0)b.Triangle(v,w,q);else b.Triangle(v,q,w);}
                    F(rim[i],rim[j],shoulder);F(rim[i],shoulder,ridge);F(shoulder,rim[j],ridge);
                }
            }
            for(int i=0;i<6;i++)
            {
                float t=i/5f;Vector3 a=P(-.19f,.76f+t*.15f,-.23f),c=P(-.19f,.76f+t*.15f,.23f);
                Tube(a,P(-.32f,.73f+t*.15f,0),.012f,cord,5);Tube(c,P(-.32f,.73f+t*.15f,0),.012f,cord,5);
            }
        }
        void Container(bool basket,bool bark)
        {
            int sides=bark?4:28,rings=bark?4:10;
            if(!basket&&!bark)
            {
                int first=b.V.Count;bool pouch=d.Form==PortableForm.Pouch,quiver=d.Form==PortableForm.Quiver;
                for(int j=0;j<=rings;j++)for(int i=0;i<=sides;i++)
                {
                    float t=j/(float)rings,angle=i*Mathf.PI*2/sides;float radius=quiver?.31f+.09f*t:pouch?.25f+.22f*Mathf.Sin(t*Mathf.PI):.35f+.1f*Mathf.Sin(t*Mathf.PI*.9f);
                    radius*=1+.035f*Mathf.Sin(angle*7+t*4)+.02f*Mathf.Sin(angle*11-t*8);float y=.04f+t*.84f;
                    b.Add(new Vector3(Mathf.Cos(angle)*radius,y,Mathf.Sin(angle)*radius),fur,new Vector2(i/(float)sides,t));
                }
                for(int j=0;j<rings;j++)for(int i=0;i<sides;i++){int a=first+j*(sides+1)+i;b.Triangle(a,a+1,a+sides+1);b.Triangle(a+1,a+sides+2,a+sides+1);}
                int center=b.Add(new Vector3(0,.04f,0),leather,new Vector2(.5f,.5f));for(int i=0;i<sides;i++)b.Triangle(center,first+i+1,first+i);
            }
            else for(int j=0;j<rings;j++)for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,c=(i+1)*Mathf.PI*2/sides,h0=.08f+j*.76f/rings,h1=h0+.76f/rings;
                float r0=.31f+.13f*Mathf.Sin(h0*Mathf.PI*.8f),r1=.31f+.13f*Mathf.Sin(h1*Mathf.PI*.8f);
                Color color=basket?new Color(.59f,.43f,.22f,0):bark?wood:fur;
                Quad(new Vector3(Mathf.Cos(a)*r0,h0,Mathf.Sin(a)*r0),new Vector3(Mathf.Cos(c)*r0,h0,Mathf.Sin(c)*r0),new Vector3(Mathf.Cos(c)*r1,h1,Mathf.Sin(c)*r1),new Vector3(Mathf.Cos(a)*r1,h1,Mathf.Sin(a)*r1),color);
            }
            Quad(new Vector3(-.3f,.08f,-.3f),new Vector3(.3f,.08f,-.3f),new Vector3(.3f,.08f,.3f),new Vector3(-.3f,.08f,.3f),leather);
            Arc(Vector3.zero,.42f,.42f,.84f,basket?cord:leather,.025f);
            if(basket)
            {
                for(int j=0;j<8;j++)Arc(Vector3.zero,.44f,.44f,.13f+j*.087f,cord,.012f);
                for(int i=0;i<16;i++){float a=i*Mathf.PI/8;Tube(new Vector3(Mathf.Cos(a)*.32f,.1f,Mathf.Sin(a)*.32f),new Vector3(Mathf.Cos(a)*.43f,.84f,Mathf.Sin(a)*.43f),.015f,wood);}
                for(int i=0;i<12;i++){float a=i*Mathf.PI/12,c=(i+1)*Mathf.PI/12;Tube(new Vector3(Mathf.Cos(a)*.42f,.84f+Mathf.Sin(a)*.4f,0),new Vector3(Mathf.Cos(c)*.42f,.84f+Mathf.Sin(c)*.4f,0),.025f,cord);}
            }
            else
            {
                if(!bark&&d.Form!=PortableForm.Pouch&&d.Form!=PortableForm.Quiver)
                {for(int i=0;i<5;i++){float t=i/5f,q=(i+1)/5f;Quad(new Vector3(-.37f,.9f-t*.35f,.33f+.13f*Mathf.Sin(t*1.57f)),new Vector3(.37f,.9f-t*.35f,.33f+.13f*Mathf.Sin(t*1.57f)),new Vector3(.37f,.9f-q*.35f,.33f+.13f*Mathf.Sin(q*1.57f)),new Vector3(-.37f,.9f-q*.35f,.33f+.13f*Mathf.Sin(q*1.57f)),fur);}}
                for(int j=0;j<8;j++){float y=.14f+j*.08f;Tube(new Vector3(-.35f,y,.32f),new Vector3(-.31f,y+.025f,.35f),.008f,cord);Tube(new Vector3(.35f,y,.32f),new Vector3(.31f,y+.025f,.35f),.008f,cord);}
                if(d.Form==PortableForm.Pack||d.Form==PortableForm.Satchel||d.Form==PortableForm.Quiver)
                    foreach(float x in new[]{-.25f,.25f})for(int j=0;j<8;j++)
                    {float t=j/8f,q=(j+1)/8f;Vector3 a=new Vector3(x,.84f-.65f*t,-.35f-.3f*Mathf.Sin(t*Mathf.PI)),c=new Vector3(x,.84f-.65f*q,-.35f-.3f*Mathf.Sin(q*Mathf.PI));Quad(a-Vector3.right*.055f,a+Vector3.right*.055f,c+Vector3.right*.055f,c-Vector3.right*.055f,leather);}
                else if(d.Form==PortableForm.Pouch)
                {Arc(Vector3.zero,.27f,.27f,.86f,cord,.015f,24);Tube(new Vector3(.21f,.86f,.19f),new Vector3(.12f,.55f,.34f),.012f,cord);Tube(new Vector3(.23f,.86f,.18f),new Vector3(.31f,.56f,.25f),.012f,cord);}
                else {Tube(new Vector3(-.38f,.84f,0),new Vector3(0,1.04f,0),.018f,cord);Tube(new Vector3(0,1.04f,0),new Vector3(.38f,.84f,0),.018f,cord);}
            }
        }
        switch(d.Form)
        {
            case PortableForm.Food:
                Color food=item.Burnt?new Color(.09f,.065f,.045f,0):item.Cooked?new Color(.39f,.22f,.11f,0):new Color((float)item.Tint.X,(float)item.Tint.Y,(float)item.Tint.Z,0);
                if(item.Kind==PossessionKind.Mushroom)
                {Tube(Vector3.zero,new Vector3(0,.62f,0),.11f,new Color(.78f,.72f,.58f,0));b.Crown(new Vector3(0,.67f,0),new Vector3(.5f,.22f,.5f),18,6,food,29,.015f);}
                else if(item.Kind==PossessionKind.Berries||item.Kind==PossessionKind.Nuts)
                {for(int i=0;i<9;i++)b.Crown(new Vector3((i%3-1)*.26f,.15f+(i%2)*.23f,(i/3-1)*.26f),Vector3.one*.19f,10,5,food,15+i,.025f);}
                else if(item.Kind==PossessionKind.RawFish)
                {b.Crown(new Vector3(0,.53f,0),new Vector3(.45f,.44f,.32f),14,7,food,22,.025f);Quad(new Vector3(0,.1f,0),new Vector3(-.45f,0,0),new Vector3(0,.17f,.08f),new Vector3(.45f,0,0),food);}
                else b.Crown(new Vector3(0,.48f,0),new Vector3(.48f,.43f,.43f),16,7,food,38,.045f);
                break;
            case PortableForm.Pouch:case PortableForm.Satchel:case PortableForm.Pack:case PortableForm.Quiver:Container(false,false);break;
            case PortableForm.Basket:Container(true,false);break;
            case PortableForm.BarkBox:Container(false,true);break;
            case PortableForm.Frame:
                foreach(float x in new[]{-.4f,.4f})Tube(new Vector3(x,0,0),new Vector3(x,1,0),.035f,wood);
                for(int i=0;i<4;i++){float y=.1f+i*.25f;Tube(new Vector3(-.4f,y,0),new Vector3(.4f,y,0),.035f,wood);foreach(float x in new[]{-.4f,.4f})for(int j=0;j<3;j++)Tube(new Vector3(x-.05f,y+j*.018f,-.06f),new Vector3(x+.05f,y+j*.018f,.06f),.018f,cord);}
                Tube(new Vector3(-.4f,.1f,0),new Vector3(-.4f,.1f,.5f),.035f,wood);Tube(new Vector3(.4f,.1f,0),new Vector3(.4f,.1f,.5f),.035f,wood);Tube(new Vector3(-.4f,.1f,.5f),new Vector3(.4f,.1f,.5f),.035f,wood);
                foreach(float x in new[]{-.3f,.3f}){Tube(new Vector3(x,.9f,0),new Vector3(x,.7f,.5f),.04f,leather);Tube(new Vector3(x,.7f,.5f),new Vector3(x,.2f,0),.04f,leather);}break;
            case PortableForm.Belt:Arc(Vector3.zero,.46f,.46f,.5f,item.Kind==PossessionKind.HideBelt?leather:cord,.09f,28);for(int i=0;i<4;i++)Arc(new Vector3((i-1.5f)*.2f,0,.35f),.06f,.08f,.5f,cord,.02f,8);break;
            case PortableForm.Case:
                Tube(Vector3.zero,Vector3.up,.34f,item.Kind==PossessionKind.NeedleCase?bone:leather,10);Arc(Vector3.zero,.38f,.38f,.95f,cord,.04f,12);if(item.Kind==PossessionKind.KnifeSheath)for(int i=0;i<8;i++)Tube(new Vector3(.34f,i*.1f,.1f),new Vector3(.36f,i*.1f+.03f,-.1f),.025f,cord);break;
            case PortableForm.Hide:
                var outline=new[]{new Vector2(-.2f,0),new Vector2(-.48f,.08f),new Vector2(-.35f,.3f),new Vector2(-.48f,.48f),new Vector2(-.35f,.7f),new Vector2(-.5f,.94f),new Vector2(-.3f,1),new Vector2(-.15f,.83f),new Vector2(.15f,.83f),new Vector2(.3f,1),new Vector2(.5f,.94f),new Vector2(.35f,.7f),new Vector2(.48f,.48f),new Vector2(.35f,.3f),new Vector2(.48f,.08f),new Vector2(.2f,0)};
                for(int i=0;i<outline.Length;i++){var a=outline[i];var c=outline[(i+1)%outline.Length];int v=b.Add(new Vector3(0,.5f,0),fur,new Vector2(.5f,.5f)),e=b.Add(new Vector3(a.x,a.y,Mathf.Sin(i)*.035f),fur,new Vector2(a.x+.5f,a.y)),f=b.Add(new Vector3(c.x,c.y,Mathf.Sin(i+1)*.035f),fur,new Vector2(c.x+.5f,c.y));b.Triangle(v,e,f);}break;
            case PortableForm.Fibre:case PortableForm.Reed:
                for(int i=0;i<12;i++){float x=(i%4-1.5f)*.15f,z=(i/4-1)*.15f;Tube(new Vector3(x,0,z),new Vector3(x+.1f*Mathf.Sin(i),.65f+(i%4)*.1f,z),.025f,d.Form==PortableForm.Reed?new Color(.57f,.56f,.28f,0):cord,5);}Wrap(.3f,.34f,2);break;
            case PortableForm.Cord:
                for(int j=0;j<4;j++)Arc(Vector3.zero,.28f+j*.04f,.28f+j*.04f,.16f+j*.08f,cord,.035f,24);Tube(new Vector3(.3f,.2f,0),new Vector3(.48f,.7f,.1f),.03f,cord);break;
            case PortableForm.Strip:
                for(int i=0;i<4;i++){float x=(i-1.5f)*.18f;Quad(new Vector3(x,0,0),new Vector3(x+.12f,0,.04f),new Vector3(x+.16f,.9f,.1f),new Vector3(x+.02f,1,0),item.Kind==PossessionKind.RawhideStrips?leather:wood);}break;
            case PortableForm.Bone:Tube(new Vector3(0,.1f,0),new Vector3(.05f,.9f,.03f),.18f,bone,10);foreach(float y in new[]{.1f,.9f})b.Crown(new Vector3(0,y,0),new Vector3(.38f,.12f,.35f),10,4,bone,18,.04f);break;
            case PortableForm.Antler:
                if(item.Kind==PossessionKind.AntlerRetoucher)
                {for(int i=0;i<8;i++){float t=i/8f,q=(i+1)/8f;b.Tube(new Vector3(.18f*t*t,t,0),new Vector3(.18f*q*q,q,.02f*q),.27f*(1-t*.75f),.27f*(1-q*.75f),10,bone);}break;}
                for(int i=0;i<5;i++){float y=i*.18f;Tube(new Vector3(Mathf.Sin(y)*.2f,y,0),new Vector3(Mathf.Sin(y+.18f)*.2f,y+.18f,.03f),.09f,bone);if(i>1)Tube(new Vector3(.1f,y,0),new Vector3(i%2==0?-.45f:.45f,y+.25f,.07f),.055f,bone);}break;
            case PortableForm.Needle:
                if(item.Kind==PossessionKind.FishingGorge){b.Tube(Vector3.zero,Vector3.up*.5f,.005f,.35f,10,bone);b.Tube(Vector3.up*.5f,Vector3.up,.35f,.005f,10,bone);Wrap(.46f,.37f,2);}
                else if(item.Kind==PossessionKind.BoneNeedle)
                {b.Tube(Vector3.up*.105f,Vector3.up,.21f,.005f,10,bone);for(int i=0;i<24;i++){float a=i*Mathf.PI/12,c=(i+1)*Mathf.PI/12;Tube(new Vector3(Mathf.Cos(a)*.27f,.06f+Mathf.Sin(a)*.045f,0),new Vector3(Mathf.Cos(c)*.27f,.06f+Mathf.Sin(c)*.045f,0),.055f,bone,6);}}
                else b.Tube(Vector3.zero,Vector3.up,.23f,.005f,10,bone);break;
            case PortableForm.Hook:
                if(item.Kind==PossessionKind.HandLine){for(int j=0;j<3;j++)Arc(Vector3.zero,.3f,.3f,j*.15f,cord,.025f);}
                for(int i=0;i<12;i++){float a=-Mathf.PI*.5f+i*Mathf.PI*1.6f/12,c=-Mathf.PI*.5f+(i+1)*Mathf.PI*1.6f/12;Tube(new Vector3(Mathf.Cos(a)*.36f,.52f+Mathf.Sin(a)*.4f,0),new Vector3(Mathf.Cos(c)*.36f,.52f+Mathf.Sin(c)*.4f,0),.05f,bone);}break;
            case PortableForm.Feather:
                for(int j=0;j<3;j++){float x=(j-1)*.2f;Tube(new Vector3(x,0,0),new Vector3(x,1,0),.012f,bone);for(int i=0;i<8;i++){float y=.2f+i*.08f;Quad(new Vector3(x,y,0),new Vector3(x-.16f,y+.12f,.02f),new Vector3(x,y+.08f,0),new Vector3(x+.16f,y+.12f,.02f),new Color(.74f,.65f,.43f,0));}}break;
            case PortableForm.Blade:case PortableForm.Point:case PortableForm.Chopper:Head(0,1,1,(item.Kind==PossessionKind.BonePoint||item.Kind==PossessionKind.BarbedPoint)?bone:stone,item.Kind==PossessionKind.BarbedPoint);break;
            case PortableForm.Knife:Tube(Vector3.zero,new Vector3(0,.45f,0),.22f,wood,8);Head(.4f,.6f,.9f,stone);Wrap(.38f,.26f,5);break;
            case PortableForm.Axe:
                for(int i=0;i<6;i++){float t=i/6f,q=(i+1)/6f;Tube(new Vector3(-.16f*t*t,t*.87f,0),new Vector3(-.16f*q*q,q*.87f,0),.09f,wood,10);}
                AxeHead(item.Kind==PossessionKind.StoneAdze);Wrap(.73f,.17f,5);break;
            case PortableForm.Shaft:
                if(item.Kind==PossessionKind.HandDrillSet)
                {
                    // A slender spindle rests in the socket of a separate flat
                    // hearthboard; the generic shaft looked like one fat pole.
                    Quad(new Vector3(-.48f,.035f,-.34f),new Vector3(-.48f,.035f,.34f),new Vector3(.48f,.035f,.34f),new Vector3(.48f,.035f,-.34f),wood);
                    Quad(new Vector3(-.48f,0,.34f),new Vector3(-.48f,0,-.34f),new Vector3(.48f,0,-.34f),new Vector3(.48f,0,.34f),wood);
                    foreach(float x in new[]{-.48f,.48f})Quad(new Vector3(x,0,-.34f),new Vector3(x,.035f,-.34f),new Vector3(x,.035f,.34f),new Vector3(x,0,.34f),wood);
                    foreach(float z in new[]{-.34f,.34f})Quad(new Vector3(-.48f,0,z),new Vector3(.48f,0,z),new Vector3(.48f,.035f,z),new Vector3(-.48f,.035f,z),wood);
                    Tube(new Vector3(-.14f,.035f,0),new Vector3(-.1f,1,.035f),.07f,wood,10);
                    b.Crown(new Vector3(-.14f,.037f,0),new Vector3(.085f,.004f,.12f),12,4,new Color(.12f,.075f,.035f,0),18,.03f);
                }
                else{Tube(Vector3.zero,Vector3.up,.3f,wood,8);if(item.Kind==PossessionKind.NotchedShaft){Tube(new Vector3(-.16f,.9f,0),new Vector3(-.16f,1,0),.07f,wood);Tube(new Vector3(.16f,.9f,0),new Vector3(.16f,1,0),.07f,wood);}}break;
            case PortableForm.Spear:Tube(Vector3.zero,new Vector3(0,.9f,0),.16f,wood,8);Head(.88f,.12f,1,item.Kind==PossessionKind.StoneTippedSpear||item.Kind==PossessionKind.LooseStoneSpear?stone:bone,item.Kind==PossessionKind.Harpoon);if(item.Kind!=PossessionKind.LooseStoneSpear)Wrap(.875f,.2f,5);break;
            case PortableForm.Arrow:
                Tube(Vector3.zero,new Vector3(0,.94f,0),Mathf.Clamp(.0035f/(float)item.WidthM,.07f,.45f),wood);if(item.Kind!=PossessionKind.ArrowShaft)Head(.92f,.08f,.65f,stone);
                if(item.Kind==PossessionKind.Arrow||item.Kind==PossessionKind.SpearDart)for(int i=0;i<3;i++){float a=i*Mathf.PI*2/3;Vector3 side=new Vector3(Mathf.Cos(a)*.45f,0,Mathf.Sin(a)*.45f);Quad(new Vector3(0,.04f,0),side+Vector3.up*.09f,side+Vector3.up*.22f,new Vector3(0,.26f,0),bone);}break;
            case PortableForm.Bow:
                for(int i=0;i<16;i++){float a=i/16f,c=(i+1)/16f;Tube(new Vector3(.38f*Mathf.Pow(a*2-1,2),a,0),new Vector3(.38f*Mathf.Pow(c*2-1,2),c,0),.027f,wood,8);}if(item.Kind==PossessionKind.Bow)Tube(new Vector3(.38f,0,0),new Vector3(.38f,1,0),.006f,cord);Wrap(.45f,.06f,5);break;
            case PortableForm.Thrower:Tube(Vector3.zero,Vector3.up,.25f,item.MaterialOrigins?.Count>0?bone:wood,8);Tube(new Vector3(0,.95f,0),new Vector3(.4f,1,.2f),.12f,bone);Wrap(.05f,.3f,5);break;
            case PortableForm.Sling:
                Quad(new Vector3(-.3f,.4f,0),new Vector3(.3f,.4f,0),new Vector3(.2f,.65f,.1f),new Vector3(-.2f,.65f,.1f),leather);Tube(new Vector3(-.3f,.5f,0),new Vector3(-.48f,1,0),.025f,cord);Tube(new Vector3(.3f,.5f,0),new Vector3(.48f,0,0),.025f,cord);break;
            case PortableForm.Net:
                for(int i=0;i<9;i++){float t=i/8f;Tube(new Vector3(-.45f,t,Mathf.Sin(t*8)*.12f),new Vector3(.45f,t,Mathf.Sin(t*8)*.12f),.012f,cord);Tube(new Vector3(t-.5f,0,0),new Vector3(t-.5f,1,.1f),.012f,cord);}break;
        }
        var mesh=b.Finish();mesh.name=d.Name;
        if(d.Form==PortableForm.Bow){var vertices=mesh.vertices;float depth=(float)(item.WidthM/item.DepthM);for(int i=0;i<vertices.Length;i++)vertices[i].z*=depth;mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();}
        var colors=mesh.colors;var tags=new System.Collections.Generic.List<Vector2>(colors.Length);var palette=new[]{stone,wood,new Color(.3f,.22f,.14f,0),new Color(.65f,.5f,.32f,0),leather,bone,cord,new Color(.57f,.56f,.28f,0)};
        foreach(var color in colors){int best=0;float error=float.MaxValue;for(int i=0;i<palette.Length;i++){var delta=(Vector4)(color-palette[i]);float e=delta.x*delta.x+delta.y*delta.y+delta.z*delta.z;if(e<error){best=i;error=e;}}if(color.a>.5f)best=4;if(d.Form==PortableForm.Food)best=8;tags.Add(new Vector2(best,0));}mesh.SetUVs(1,tags);return mesh;
    }
}
