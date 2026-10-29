// Draws an OutlineSprite's outline through its SpriteRenderer, from the field the sprite carries: the ring from the
// silhouette's edge out to the width, in the renderer's colour, from the distance in R, over a shadow of the silhouette
// and outline: sharp from the distance in R, soft from the blur levels baked in G, B and A, one per zoom. Width, shadow
// distance and softness are interpolated in world units from the orthographic size of the camera drawing it, and the
// levels blended by it, so zooming never needs a new capture. The shadow is compiled in only under _SHADOW, which the
// component sets while its shadow is on. Keeps SRP Batcher compatibility.
Shader "RiseOn/Outline2D/OutlineSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Distance Field", 2D) = "white" {}
        _OrthoRange ("Ortho Range", Vector) = (5, 10, 0, 0)
        _WidthRange ("Width Range (world)", Vector) = (0.05, 0.1, 0, 0)
        _TexelSize ("Texel Size (local)", Float) = 1
        _MaxRadius ("Max Radius (texels)", Float) = 1
        _FieldSize ("Field Size (texels)", Float) = 1
        _FrameSize ("Frame Size (texels)", Vector) = (1, 1, 0, 0)
        _ShadowColor ("Shadow Color", Color) = (0, 0, 0, 0)
        _ShadowDirection ("Shadow Direction (world)", Vector) = (0, -1, 0, 0)
        _ShadowDistanceRange ("Shadow Distance Range (world)", Vector) = (0, 0, 0, 0)
        _ShadowSoftnessRange ("Shadow Softness Range (world)", Vector) = (0, 0, 0, 0)
        _ShadowLevels ("Shadow Levels (x: middle zoom, w: field has them)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // Materials are made at run time, so shader_feature would strip the shadow from builds. Without it this is
            // the ring alone, at the ring's cost.
            #pragma multi_compile_local _ _SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _OrthoRange;
                float4 _WidthRange;
                float _TexelSize;
                float _MaxRadius;
                float _FieldSize;
                float4 _FrameSize;
                float4 _ShadowColor;
                float4 _ShadowDirection;
                float4 _ShadowDistanceRange;
                float4 _ShadowSoftnessRange;
                float4 _ShadowLevels;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float radius : TEXCOORD1;
                #ifdef _SHADOW
                float2 shadowOffset : TEXCOORD2;
                float3 shadowWeights : TEXCOORD3;
                float2 shadowEdge : TEXCOORD4;
                #endif
                half4 color : COLOR;
            };

            Varyings vert (Attributes input)
            {
                Varyings output;

                // No flipX / flipY: the quad already sits in this object's space, and mirroring it would move it off the group.
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;

                // The renderer's tint, SpriteRenderer.color, taken the way URP's own sprite shaders take it.
                output.color = input.color * unity_SpriteColor;

                // World width for the camera drawing this, held at the ends of the range, then field texels at
                // this object's current scale; capped so the ring never runs off the capture.
                float span = _OrthoRange.y - _OrthoRange.x;
                float t = span > 0 ? saturate((unity_OrthoParams.y - _OrthoRange.x) / span) : 0;
                float width = lerp(_WidthRange.x, _WidthRange.y, t);
                float worldPerLocal = length(GetObjectToWorldMatrix()._m00_m10_m20);

                output.radius = min(width / (worldPerLocal * _TexelSize), _MaxRadius);

                #ifdef _SHADOW
                // The shadow falls a world distance for this zoom along a world direction, taken into this object's space
                // so it falls the same way whatever the group's rotation or flip; in field texels.
                float distance = lerp(_ShadowDistanceRange.x, _ShadowDistanceRange.y, t);
                float2 offset = mul((float3x3)GetWorldToObjectMatrix(), float3(_ShadowDirection.xy * distance, 0)).xy;
                output.shadowOffset = offset / _TexelSize;

                // The baked levels are three zooms, the near end, the middle in _ShadowLevels.x and the far end; this zoom
                // blends the two around it. A field captured without a shadow (w 0) has a single channel that reads 0 in
                // G and B but 1 in A, so it gets no shadow at all, even if the shadow was turned on while it was in
                // flight.
                float middle = _ShadowLevels.x;
                if (_ShadowLevels.w <= 0)
                {
                    output.shadowWeights = 0;
                    output.shadowEdge = 0;
                    return output;
                }
                float3 weights;
                if (t <= middle)
                {
                    float k = middle > 0 ? t / middle : 1;
                    weights = float3(1 - k, k, 0);
                }
                else
                {
                    float k = middle < 1 ? (t - middle) / (1 - middle) : 1;
                    weights = float3(0, 1 - k, k);
                }

                // The fragment blends edge positions, each level's logit times its blur width, then divides by the blended
                // width; folded into the weights here. The width counts the couple of texels the half resolution adds,
                // so a level of softness 0 still has one.
                float3 levelSoftness = float3(_ShadowSoftnessRange.x, lerp(_ShadowSoftnessRange.x, _ShadowSoftnessRange.y, middle), _ShadowSoftnessRange.y);
                float3 sigma = levelSoftness / (3 * worldPerLocal * _TexelSize);
                float3 spread = sqrt(sigma * sigma + 1) * weights;
                output.shadowWeights = spread / dot(spread, 1);

                // The softness in field texels for this zoom, and how much of the sharp shadow to draw. The blur is baked at
                // half resolution, so its edge is a couple of texels soft whatever the softness; up to a texel the edge is
                // drawn from the distance instead, as the ring's is, and past three texels the blur alone, blended between.
                float softness = lerp(_ShadowSoftnessRange.x, _ShadowSoftnessRange.y, t) / (worldPerLocal * _TexelSize);
                output.shadowEdge = float2(softness, 1 - saturate((softness - 1) / 2));
                #endif
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                // Texels from the nearest silhouette texel centre; the silhouette's own edge is half a texel out.
                float d = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).r;

                // Anti-aliasing width from the UV, not fwidth(d): helper pixels past the quad's edge sample stale texels
                // outside this capture and would turn the edge into a faint dotted rim. A distance changes by at most
                // one texel per texel, so texels per screen pixel is the right width.
                float2 texels = input.uv * _FieldSize;
                float aa = max(max(length(ddx(texels)), length(ddy(texels))), 1e-4);
                float outer = saturate((input.radius + 0.5 - d) / aa + 0.5);

                // Inside the silhouette the field is 0 everywhere, so the inner ramp stops at one texel: as wide as a
                // pixel, it would tint the whole silhouette once a pixel covers more than a texel (zoomed out).
                float inner = saturate((d - 0.5) / min(aa, 1) + 0.5);

                #ifndef _SHADOW
                // A white ring, like a sprite's texture, tinted by the renderer the way every sprite is.
                half4 ring = half4(1, 1, 1, outer * inner);
                return ring * input.color;
                #else
                // The grown silhouette moved by the offset: its edge from the distance there, ramped over the softness or a
                // pixel, and the blurred levels blended for this zoom. Texels outside this capture hold another capture's
                // field, so a sample out there is no shadow at all.
                float2 shadowTexels = texels - input.shadowOffset;
                float2 frameEnd = _FrameSize.xy - 0.5;
                float framed = all(shadowTexels >= 0.5) && all(shadowTexels <= frameEnd) ? 1 : 0;
                float4 shifted = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, shadowTexels / _FieldSize);
                float sharp = saturate((input.radius + 0.5 - shifted.r) / max(aa, input.shadowEdge.x) + 0.5);

                // Two levels blended by coverage show both edges where they were grown or blurred apart, a ramp that
                // widens and narrows as the zoom moves between them. A blurred edge is close to a logistic curve, so its
                // logit is about the distance to the edge over its width: blending those blends where the edge is, and on
                // a straight edge gives close to one blur at the width and softness between. Everywhere it stays between
                // the two levels. Coverage is kept off 0 and 1, where the logit has no end, and the margin taken back after.
                const float MARGIN = 1e-3;
                float3 coverage = clamp(shifted.gba, MARGIN, 1 - MARGIN);
                float logit = dot(log(coverage / (1 - coverage)), input.shadowWeights);
                float blurred = saturate((1 / (1 + exp(-logit)) - MARGIN) / (1 - 2 * MARGIN)) * dot(input.shadowWeights, 1);
                float shadow = lerp(blurred, sharp, input.shadowEdge.y) * framed;

                // The ring, white like a sprite's texture and tinted by the renderer the way every sprite is, over the
                // shadow; neither covers the silhouette, and fading the renderer fades both.
                float ringA = outer * inner * input.color.a;
                float shadowA = shadow * inner * _ShadowColor.a * input.color.a * (1 - ringA);
                float alpha = ringA + shadowA;
                half3 rgb = alpha > 0 ? (input.color.rgb * ringA + _ShadowColor.rgb * shadowA) / alpha : 0;
                return half4(rgb, alpha);
                #endif
            }
            ENDHLSL
        }
    }
}