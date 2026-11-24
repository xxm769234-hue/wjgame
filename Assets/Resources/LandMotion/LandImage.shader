Shader "Shuyu/Land Archive Image"
{
    Properties
    {
        [PerRendererData] _MainTex ("Scene", 2D) = "white" {}
        _MotionMask ("Water, foliage, vapor, fire", 2D) = "black" {}
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "LandMotion.cginc"
            sampler2D _MainTex;
            float4 _ClipRect;
            struct Varyings { float4 pos : SV_POSITION; float4 local : TEXCOORD1; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            Varyings vert(appdata_full v)
            {
                Varyings o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.local = v.vertex;
                o.uv = v.texcoord.xy;
                o.color = v.color;
                return o;
            }
            fixed4 frag(Varyings input) : SV_Target
            {
                fixed4 col = SampleLandMotion(_MainTex,input.uv) * input.color;
                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(input.local.xy,_ClipRect);
                #endif
                return col;
            }
            ENDCG
        }
    }
}
