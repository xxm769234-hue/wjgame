Shader "Shuyu/Land Scene Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Scene", 2D) = "white" {}
        _MotionMask ("Water, foliage, vapor, fire", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #include "UnitySprites.cginc"
            #include "LandMotion.cginc"
            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 col = SampleLandMotion(_MainTex, input.texcoord) * input.color;
                col.rgb *= col.a;
                return col;
            }
            ENDCG
        }
    }
}
