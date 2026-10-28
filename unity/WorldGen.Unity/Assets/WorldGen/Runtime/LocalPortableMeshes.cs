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
        void Tube(Vector3 a,Vector3 c,float r,Color color,int sides=6)=>b.Tube(a,c,r,r*.95f,sides,color);
        void Arc(Vector3 center,float rx,float rz,float y,Color color,float thickness=.012f,int steps=18)
        {for(int i=0;i<steps;i++){float a=i*Mathf.PI*2/steps,c=(i+1)*Mathf.PI*2/steps;Tube(center+new Vector3(Mathf.Cos(a)*rx,y,Mathf.Sin(a)*rz),center+new Vector3(Mathf.Cos(c)*rx,y,Mathf.Sin(c)*rz),thickness,color);}}
        void Head(float y,float height,float width,Color color,bool barbed=false)
        {
            var rim=new[]{new Vector3(0,y+height,0),new Vector3(width*.5f,y+height*.4f,0),new Vector3(width*.3f,y,0),new Vector3(-width*.3f,y,0),new Vector3(-width*.5f,y+height*.4f,0)};
            for(int i=0;i<rim.Length;i++){int j=(i+1)%rim.Length;Vector3 front=new Vector3(0,y+height*.45f,.21f),back=new Vector3(0,y+height*.45f,-.17f);int a=b.Add(front,color*(i%2==0?1:.8f),new Vector2(.5f,.5f)),c=b.Add(rim[i],color,new Vector2(rim[i].x/width+.5f,(rim[i].y-y)/height)),e=b.Add(rim[j],color,new Vector2(rim[j].x/width+.5f,(rim[j].y-y)/height));b.Triangle(a,c,e);int z=b.Add(back,color*.85f,new Vector2(.5f,.5f));b.Triangle(z,e,c);}
            if(barbed)for(int i=0;i<4;i++){float h=y+height*(.18f+i*.17f);Quad(new Vector3(-.25f,h,0),new Vector3(-.58f,h-.05f,0),new Vector3(-.27f,h+.09f,0),new Vector3(-.2f,h+.05f,0),color);}
        }
        void Wrap(float y,float radius,int count=5){for(int i=0;i<count;i++)Arc(Vector3.zero,radius,radius,y+i*.015f,cord,.014f,12);}
        void Container(bool basket,bool bark)
        {
            int sides=bark?4:16,rings=4;
            for(int j=0;j<rings;j++)for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,c=(i+1)*Mathf.PI*2/sides,h0=.08f+j*.19f,h1=h0+.19f;
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
                if(!bark)Quad(new Vector3(-.38f,.88f,.35f),new Vector3(.38f,.88f,.35f),new Vector3(.32f,.5f,.46f),new Vector3(-.32f,.5f,.46f),fur);
                for(int j=0;j<8;j++){float y=.14f+j*.08f;Tube(new Vector3(-.35f,y,.32f),new Vector3(-.31f,y+.025f,.35f),.008f,cord);Tube(new Vector3(.35f,y,.32f),new Vector3(.31f,y+.025f,.35f),.008f,cord);}
                if(d.Form==PortableForm.Pack||d.Form==PortableForm.Satchel||d.Form==PortableForm.Quiver)
                    foreach(float x in new[]{-.27f,.27f}){Tube(new Vector3(x,.82f,-.28f),new Vector3(x,1.18f,.14f),.035f,leather);Tube(new Vector3(x,1.18f,.14f),new Vector3(x,.25f,-.28f),.035f,leather);}
                else {Tube(new Vector3(-.38f,.84f,0),new Vector3(0,1.18f,0),.018f,cord);Tube(new Vector3(0,1.18f,0),new Vector3(.38f,.84f,0),.018f,cord);}
            }
        }
        switch(d.Form)
        {
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
                for(int i=0;i<5;i++){float y=i*.18f;Tube(new Vector3(Mathf.Sin(y)*.2f,y,0),new Vector3(Mathf.Sin(y+.18f)*.2f,y+.18f,.03f),.09f,bone);if(i>1)Tube(new Vector3(.1f,y,0),new Vector3(i%2==0?-.45f:.45f,y+.25f,.07f),.055f,bone);}break;
            case PortableForm.Needle:
                b.Tube(Vector3.zero,Vector3.up,.23f,.005f,8,bone);if(item.Kind==PossessionKind.BoneNeedle){Arc(new Vector3(0,.08f,0),.2f,.3f,0,new Color(.2f,.15f,.1f,0),.06f,12);}break;
            case PortableForm.Hook:
                if(item.Kind==PossessionKind.HandLine){for(int j=0;j<3;j++)Arc(Vector3.zero,.3f,.3f,j*.15f,cord,.025f);}
                for(int i=0;i<12;i++){float a=-Mathf.PI*.5f+i*Mathf.PI*1.6f/12,c=-Mathf.PI*.5f+(i+1)*Mathf.PI*1.6f/12;Tube(new Vector3(Mathf.Cos(a)*.36f,.52f+Mathf.Sin(a)*.4f,0),new Vector3(Mathf.Cos(c)*.36f,.52f+Mathf.Sin(c)*.4f,0),.05f,bone);}break;
            case PortableForm.Feather:
                for(int j=0;j<3;j++){float x=(j-1)*.2f;Tube(new Vector3(x,0,0),new Vector3(x,1,0),.012f,bone);for(int i=0;i<8;i++){float y=.2f+i*.08f;Quad(new Vector3(x,y,0),new Vector3(x-.16f,y+.12f,.02f),new Vector3(x,y+.08f,0),new Vector3(x+.16f,y+.12f,.02f),new Color(.74f,.65f,.43f,0));}}break;
            case PortableForm.Blade:case PortableForm.Point:case PortableForm.Chopper:Head(0,1,1,(item.Kind==PossessionKind.BonePoint||item.Kind==PossessionKind.BarbedPoint)?bone:stone,item.Kind==PossessionKind.BarbedPoint);break;
            case PortableForm.Knife:Tube(Vector3.zero,new Vector3(0,.45f,0),.22f,wood,8);Head(.4f,.6f,.9f,stone);Wrap(.38f,.26f,5);break;
            case PortableForm.Axe:Tube(Vector3.zero,new Vector3(.12f,.9f,0),.1f,wood,8);Head(.72f,.26f,1,stone);Wrap(.73f,.25f,7);break;
            case PortableForm.Shaft:Tube(Vector3.zero,Vector3.up,.3f,wood,8);if(item.Kind==PossessionKind.NotchedShaft){Tube(new Vector3(-.16f,.9f,0),new Vector3(-.16f,1,0),.07f,wood);Tube(new Vector3(.16f,.9f,0),new Vector3(.16f,1,0),.07f,wood);}break;
            case PortableForm.Spear:Tube(Vector3.zero,new Vector3(0,.9f,0),.16f,wood,8);Head(.88f,.12f,1,item.Kind==PossessionKind.StoneTippedSpear||item.Kind==PossessionKind.LooseStoneSpear?stone:bone,item.Kind==PossessionKind.Harpoon);if(item.Kind!=PossessionKind.LooseStoneSpear)Wrap(.875f,.2f,5);break;
            case PortableForm.Arrow:
                Tube(Vector3.zero,new Vector3(0,.94f,0),.09f,wood);if(item.Kind!=PossessionKind.ArrowShaft)Head(.92f,.08f,.65f,stone);
                if(item.Kind==PossessionKind.Arrow||item.Kind==PossessionKind.SpearDart)for(int i=0;i<3;i++){float a=i*Mathf.PI*2/3;Vector3 side=new Vector3(Mathf.Cos(a)*.45f,0,Mathf.Sin(a)*.45f);Quad(new Vector3(0,.04f,0),side+Vector3.up*.09f,side+Vector3.up*.22f,new Vector3(0,.26f,0),bone);}break;
            case PortableForm.Bow:
                for(int i=0;i<16;i++){float a=i/16f,c=(i+1)/16f;Tube(new Vector3(.38f*Mathf.Pow(a*2-1,2),a,0),new Vector3(.38f*Mathf.Pow(c*2-1,2),c,0),.027f,wood,8);}if(item.Kind==PossessionKind.Bow)Tube(new Vector3(.38f,0,0),new Vector3(.38f,1,0),.006f,cord);Wrap(.45f,.06f,5);break;
            case PortableForm.Thrower:Tube(Vector3.zero,Vector3.up,.25f,item.MaterialOrigins?.Count>0?bone:wood,8);Tube(new Vector3(0,.95f,0),new Vector3(.4f,1,.2f),.12f,bone);Wrap(.05f,.3f,5);break;
            case PortableForm.Sling:
                Quad(new Vector3(-.3f,.4f,0),new Vector3(.3f,.4f,0),new Vector3(.2f,.65f,.1f),new Vector3(-.2f,.65f,.1f),leather);Tube(new Vector3(-.3f,.5f,0),new Vector3(-.48f,1,0),.025f,cord);Tube(new Vector3(.3f,.5f,0),new Vector3(.48f,0,0),.025f,cord);break;
            case PortableForm.Net:
                for(int i=0;i<9;i++){float t=i/8f;Tube(new Vector3(-.45f,t,Mathf.Sin(t*8)*.12f),new Vector3(.45f,t,Mathf.Sin(t*8)*.12f),.012f,cord);Tube(new Vector3(t-.5f,0,0),new Vector3(t-.5f,1,.1f),.012f,cord);}break;
        }
        var mesh=b.Finish();mesh.name=d.Name;return mesh;
    }
}
