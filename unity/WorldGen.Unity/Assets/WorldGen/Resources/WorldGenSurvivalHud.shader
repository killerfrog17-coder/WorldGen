Shader "WorldGen/Survival HUD"
{
    Properties
    {
        [PerRendererData] _MainTex("UI texture",2D)="white"{}
        _Shapes("Approved shape distance fields",2D)="white"{}
        _Resources("Health energy stamina hydration",Vector)=(1,1,1,1)
        _Thermal("Fullness felt temperature signed body stress",Vector)=(1,22,0,0)
        _StencilComp("Stencil Comparison",Float)=8
        _Stencil("Stencil ID",Float)=0
        _StencilOp("Stencil Operation",Float)=0
        _StencilWriteMask("Stencil Write Mask",Float)=255
        _StencilReadMask("Stencil Read Mask",Float)=255
        _ColorMask("Color Mask",Float)=15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip",Float)=0
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False"}
        Stencil {Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]}
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct A{float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            struct V{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;float4 local:TEXCOORD1;};
            sampler2D _Shapes;float4 _Resources,_Thermal,_ClipRect,_Wind;
            V vert(A a){V o;o.local=a.vertex;o.vertex=UnityObjectToClipPos(a.vertex);o.uv=a.uv;o.color=a.color;return o;}
            float Smooth(float x){x=saturate(x);return x*x*(3-2*x);}
            float Coverage(float d,float aa){return 1-smoothstep(-aa,aa,d);}
            float3 Rgb(float3 c)
            {
                #if !defined(UNITY_COLORSPACE_GAMMA)
                return GammaToLinearSpace(c);
                #else
                return c;
                #endif
            }
            void Over(inout float4 c,float3 rgb,float alpha)
            {alpha=saturate(alpha);c.rgb=Rgb(rgb)*alpha+c.rgb*(1-alpha);c.a=alpha+c.a*(1-alpha);}
            float Arc(float2 p,float radius,float start,float end)
            {
                float angle=atan2(p.y,p.x);if(start>0&&angle<0)angle+=6.28318530718;
                float a=clamp(angle,start,end);float2 nearest=radius*float2(cos(a),sin(a));return length(p-nearest)-5;
            }
            float WindLine(float2 q,float y,float extent)
            {
                float line=length(q-float2(clamp(q.x,-extent,4),y))-1;
                float2 curl=q-float2(4,y-3);float ring=abs(length(curl)-3)-1;
                if(curl.x<0&&curl.y>0)ring=100;
                return min(line,ring);
            }
            float4 frag(V i):SV_Target
            {
                float2 position=float2(i.uv.x,1-i.uv.y)*600,p=position-300;
                float aa=max(.05,max(fwidth(p.x),fwidth(p.y))*.65);
                float4 shape=(tex2D(_Shapes,i.uv)-.5)*32;
                float4 c=0;float stress=saturate(abs(_Thermal.z)),d=length(p)/236;
                float3 cold=lerp(float3(131,221,235),float3(37,139,235),Smooth(stress))/255;
                float3 hot=lerp(float3(245,163,59),float3(239,68,68),Smooth(stress))/255;
                float glow=.99*pow(stress,.72)*exp(-13*pow(1-stress,1.7)*d*d)*(1-.06*d*d);
                Over(c,_Thermal.z<0?cold:hot,glow*step(d,1));
                float t=_Thermal.y;
                if(t<19)Over(c,lerp(float3(131,221,235),float3(37,139,235),Smooth((8-t)/20))/255,Coverage(shape.a,aa)*Smooth((19-t)/11));
                else if(t>24)Over(c,lerp(float3(245,163,59),float3(239,68,68),Smooth((t-33)/14))/255,Coverage(shape.b,aa)*Smooth((t-24)/9));
                const float radians=.0174532925199433;
                float left=Arc(p,231,95*radians,265*radians),right=Arc(p,231,-85*radians,85*radians),inner=Arc(p,214,-85*radians,85*radians);
                Over(c,float3(39,42,50)/255,Coverage(min(left,min(right,inner)),aa));
                if(_Resources.x>0)Over(c,float3(255,48,56)/255,Coverage(Arc(p,231,95*radians,(95+170*saturate(_Resources.x))*radians),aa));
                if(_Resources.y>0)Over(c,float3(41,70,255)/255,Coverage(Arc(p,231,(85-170*saturate(_Resources.y))*radians,85*radians),aa));
                if(_Resources.z>0)Over(c,float3(0,229,244)/255,Coverage(Arc(p,214,(85-170*saturate(min(_Resources.y,_Resources.z)))*radians,85*radians),aa));
                float water=Coverage(shape.r,aa),food=Coverage(shape.g,aa);
                Over(c,float3(89,99,109)/255,water);
                if(_Resources.w>0)Over(c,float3(36,156,255)/255,water*smoothstep(-aa,aa,position.y-(329.57142857-saturate(_Resources.w)*120)));
                Over(c,float3(181,234,255)/255,Coverage(abs(shape.r)-1.0,aa));
                Over(c,float3(119,122,128)/255,food);
                if(_Thermal.x>0)Over(c,1,food*smoothstep(-aa,aa,position.y-(411.65755556-saturate(_Thermal.x)*221.31511112)));
                Over(c,1,Coverage(abs(shape.g)-.675,aa));
                if(_Wind.y>.001)
                for(int sector=0;sector<6;sector++)
                {
                    float angle=(-90+sector*60)*radians;
                    float delta=atan2(sin(angle-_Wind.x),cos(angle-_Wind.x));
                    float weight=exp(-pow(delta/max(.35,_Wind.z),2)*2.5)*_Wind.y;
                    float2 centre=195*float2(cos(angle),sin(angle)),q=p-centre;
                    float stroke=min(WindLine(q,-7,9),min(WindLine(q,0,13),WindLine(q,7,7)));
                    float pulse=.82+.18*sin(_Time.y*(1+_Wind.y*2)+sector);
                    Over(c,float3(.84,.9,.91),Coverage(stroke,aa)*weight*pulse*.68);
                }
                float alpha=i.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                alpha*=UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                c*=alpha;
                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a-.001);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
