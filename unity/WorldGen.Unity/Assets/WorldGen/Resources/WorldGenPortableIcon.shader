Shader "Hidden/WorldGen/PortableIcon"
{
    Properties {_MainTex("Shape",2D)="white"{} _SourceCoats("Coats",2DArray)=""{} _FurRect("Region",Vector)=(0,0,1,1) _Coat("Coat",Float)=0}
    SubShader {Cull Off ZWrite Off ZTest Always Pass {
        HLSLPROGRAM
        #pragma target 3.5
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        struct A{float4 vertex:POSITION;float2 uv:TEXCOORD0;};struct O{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
        sampler2D _MainTex;UNITY_DECLARE_TEX2DARRAY(_SourceCoats);float4 _FurRect;float _Coat;
        O vert(A a){O o;o.pos=UnityObjectToClipPos(a.vertex);o.uv=a.uv;return o;}
        half4 frag(O i):SV_Target{half4 shape=tex2D(_MainTex,i.uv);half3 coat=UNITY_SAMPLE_TEX2DARRAY(_SourceCoats,float3(lerp(_FurRect.xy,_FurRect.zw,i.uv),_Coat)).rgb;return half4(coat*(.7+.3*i.uv.y),shape.a);}
        ENDHLSL
    }}
}
