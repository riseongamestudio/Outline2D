// The GPU field build of OutlineQuality.Fast, drawn only through a CommandBuffer, never by a camera, so it carries no
// pipeline tag. Every pass draws a quad over the frame, the bottom-left _JumpFrame.xy texels of _JumpFrame.w-sized
// targets, and works in texel indices of that frame. Seed: silhouette texels of the mask point at themselves, the rest
// at nowhere. Jump: each texel keeps the nearest of the seeds it and its eight neighbours _JumpStep away point at.
// Resolve: the distance to that seed in texels in R, as the exact build writes it, and in G, B and A each shadow
// level's coverage, the silhouette grown by _JumpGrowths and faded over _JumpSigmas as a Gaussian blur fades a
// straight edge.
Shader "Hidden/RiseOn/Outline2D/JumpFlood"
{
    CGINCLUDE
    #include "UnityCG.cginc"

    // Farther than any texel of the largest frame, and still a half.
    #define NOWHERE 60000.0

    // What the exact build writes where no silhouette is in reach.
    #define BEYOND 4096.0

    sampler2D _JumpSource;
    float4 _JumpFrame;
    float _JumpStep;
    float3 _JumpGrowths;
    float3 _JumpSigmas;
    float _JumpLevels;

    struct appdata
    {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
    };

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 texel : TEXCOORD0;
    };

    v2f vert (appdata v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.texel = v.uv * _JumpFrame.xy;
        return o;
    }

    float4 Fetch (float2 index)
    {
        return tex2Dlod(_JumpSource, float4((index + .5) * _JumpFrame.z, 0, 0));
    }
    ENDCG

    SubShader
    {
        ZTest Always
        ZWrite Off
        Cull Off
        Blend Off

        // 0: seed from the mask.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float4 frag (v2f i) : SV_Target
            {
                float2 index = floor(i.texel);

                return Fetch(index).r > .5 ? float4(index, 0, 0) : float4(NOWHERE, NOWHERE, 0, 0);
            }
            ENDCG
        }

        // 1: one jump.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float4 frag (v2f i) : SV_Target
            {
                float2 index = floor(i.texel);
                float2 best = Fetch(index).xy;
                float2 offset = best - index;
                float bestDistance = dot(offset, offset);

                for (int y = -1; y <= 1; ++y)
                {
                    for (int x = -1; x <= 1; ++x)
                    {
                        float2 neighbour = index + float2(x, y) * _JumpStep;

                        if (any(neighbour < 0) || any(neighbour >= _JumpFrame.xy)) continue;

                        float2 seed = Fetch(neighbour).xy;
                        float2 toSeed = seed - index;
                        float distance = dot(toSeed, toSeed);

                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            best = seed;
                        }
                    }
                }

                return float4(best, 0, 0);
            }
            ENDCG
        }

        // 2: resolve to the field.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // The coverage a level leaves at distance d: the silhouette's edge sits half a texel out from its texels'
            // centres, grown by g, then blurred at standard deviation sigma, which across a straight edge is the normal
            // CDF, here its logistic fit 1 / (1 + exp(-1.702 x)). Unblurred, the one-texel ramp of the exact build.
            float Coverage (float d, float g, float sigma)
            {
                if (sigma < .5) return saturate(g + 1 - d);

                return 1 / (1 + exp(-1.702 * (g + .5 - d) / sigma));
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 index = floor(i.texel);
                float distance = min(length(Fetch(index).xy - index), BEYOND);

                float3 levels = float3(
                    _JumpLevels > 0 ? Coverage(distance, _JumpGrowths.x, _JumpSigmas.x) : 0,
                    _JumpLevels > 1 ? Coverage(distance, _JumpGrowths.y, _JumpSigmas.y) : 0,
                    _JumpLevels > 2 ? Coverage(distance, _JumpGrowths.z, _JumpSigmas.z) : 0);

                return float4(distance, levels);
            }
            ENDCG
        }
    }
}