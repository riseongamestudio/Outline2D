// Draws an OutlineSprite's outline through its SpriteRenderer, from the distance field the sprite carries: the ring from
// the silhouette's edge out to the width, in the renderer's colour. The width is interpolated in world units from the
// orthographic size of the camera drawing it, so zooming never needs a new capture. Keeps SRP Batcher compatibility.
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _OrthoRange;
                float4 _WidthRange;
                float _TexelSize;
                float _MaxRadius;
                float _FieldSize;
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

                // A white ring, like a sprite's texture, tinted by the renderer the way every sprite is.
                half4 ring = half4(1, 1, 1, outer * inner);
                return ring * input.color;
            }
            ENDHLSL
        }
    }
}