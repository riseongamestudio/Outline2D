// Silhouette capture: drawn only through a CommandBuffer, never by a camera, so it carries no pipeline tag. Sprites
// come through DrawRenderer with their geometry and flipX / flipY already applied, Graphics through DrawMesh with the
// mesh uGUI built for them, both with their texture in _MainTex (pass 0). Any other renderer comes through DrawRenderer
// a submesh at a time, its material's texture set globally in _Outline2DSourceTex (pass 1): this material has its own
// _MainTex, which a global could not override. Only the texture's alpha counts; the target's color and alpha are
// ignored on purpose.
Shader "Hidden/RiseOn/Outline2D/Mask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    CGINCLUDE
    #include "UnityCG.cginc"

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
    ENDCG

    SubShader
    {
        ZTest Always
        ZWrite Off
        Cull Off

        // Union of the silhouettes whatever the draw order.
        BlendOp Max
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            sampler2D _MainTex;

            fixed4 frag (v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv).a > _Cutoff ? 1 : 0;
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            sampler2D _Outline2DSourceTex;

            fixed4 frag (v2f i) : SV_Target
            {
                return tex2D(_Outline2DSourceTex, i.uv).a > _Cutoff ? 1 : 0;
            }
            ENDCG
        }
    }
}