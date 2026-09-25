// Silhouette capture: drawn only through a CommandBuffer, never by a camera, so it carries no pipeline tag. Sprites
// come through DrawRenderer with their geometry and flipX / flipY already applied, Images through DrawMesh with the
// mesh uGUI built for them and their texture in _MainTex. Only the texture's alpha counts; the target's color and
// alpha are ignored on purpose.
Shader "Hidden/RiseOn/Outline2D/Mask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off

            // Union of the silhouettes whatever the draw order.
            BlendOp Max
            Blend One One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Cutoff;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv).a > _Cutoff ? 1 : 0;
            }
            ENDCG
        }
    }
}