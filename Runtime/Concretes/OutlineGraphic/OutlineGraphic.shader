// Draws an OutlineGraphic's ring from the distance in the field's R as a UI Graphic, over a shadow: a directional one is
// the silhouette and outline moved one way, sharp from the distance in R, soft from the blur baked in G; a spread one
// fades out all around from the ring's outer edge, from R alone. Stencil, RectMask2D clipping, the canvas's vertex colour
// handling and premultiplied blending follow Unity's UI/Default. The ring's radius and the field's size ride in uv0.zw,
// the shadow's offset and softness (or its reach) and the frame's size in uv1 (in field texels) and its colour in uv2, so
// the material changes only when the shadow is turned on, off or to the other kind: each is compiled in only under its
// keyword, _SHADOW or _SHADOW_SPREAD.
Shader "RiseOn/Outline2D/OutlineGraphic"
{
    Properties
    {
        [PerRendererData] _MainTex ("Distance Field", 2D) = "white" {}

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            // Materials are made at run time, so shader_feature would strip the shadow from builds. Without it this is
            // the ring alone, at the ring's cost.
            #pragma multi_compile_local _ _SHADOW _SHADOW_SPREAD

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float4 texcoord : TEXCOORD0;
                float4 shadow : TEXCOORD1;
                float4 shadowColor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float4 texcoord : TEXCOORD0;
                float4 mask : TEXCOORD1;
                #if defined(_SHADOW) || defined(_SHADOW_SPREAD)
                float4 shadow : TEXCOORD2;
                float4 shadowColor : TEXCOORD3;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Full precision: the field holds distances in texels, far beyond what a fixed sampler holds on mobile.
            sampler2D_float _MainTex;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;

            v2f vert (appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float4 vPosition = UnityObjectToClipPos(v.vertex);
                OUT.vertex = vPosition;

                float2 pixelSize = vPosition.w;
                pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                OUT.mask = float4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));
                OUT.texcoord = v.texcoord;
                #if defined(_SHADOW) || defined(_SHADOW_SPREAD)
                OUT.shadow = v.shadow;
                OUT.shadowColor = v.shadowColor;
                #endif

                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                {
                    v.color.rgb = UIGammaToLinear(v.color.rgb);
                }

                OUT.color = v.color;
                return OUT;
            }

            fixed4 frag (v2f IN) : SV_Target
            {
                // Texels from the nearest silhouette texel centre; the silhouette's own edge is half a texel out.
                float d = tex2D(_MainTex, IN.texcoord.xy).r;

                // The sprite outline's ring: anti-aliasing width from the UV, inner ramp capped at one texel.
                float2 texels = IN.texcoord.xy * IN.texcoord.w;
                float aa = max(max(length(ddx(texels)), length(ddy(texels))), 1e-4);
                float outer = saturate((IN.texcoord.z + 0.5 - d) / aa + 0.5);
                float inner = saturate((d - 0.5) / min(aa, 1) + 0.5);

                // Interpolated alpha rounded to 1/255 steps, as UI/Default does for HDR targets.
                const half alphaPrecision = half(0xff);
                const half invAlphaPrecision = half(1.0 / alphaPrecision);
                IN.color.a = round(IN.color.a * alphaPrecision) * invAlphaPrecision;

                #if defined(_SHADOW_SPREAD)
                // From the ring's outer edge out to the reach in uv1.z, smooth at both ends; under the ring it is full, so
                // the ring's anti-aliased edge blends into it with no gap.
                float spreadShadow = 1 - smoothstep(0, 1, (d - IN.texcoord.z - 0.5) / max(IN.shadow.z, aa));

                float ringA = outer * inner * IN.color.a;
                float shadowA = spreadShadow * inner * IN.shadowColor.a * IN.color.a * (1 - ringA);
                float alpha = ringA + shadowA;
                half4 color = half4(alpha > 0 ? (IN.color.rgb * ringA + IN.shadowColor.rgb * shadowA) / alpha : 0, alpha);
                #elif !defined(_SHADOW)
                // A white ring, like an Image's texture, tinted by Graphic.color through the vertex colour as every
                // Graphic is.
                half4 ring = half4(1, 1, 1, outer * inner);
                half4 color = ring * IN.color;
                #else
                // The grown silhouette moved by the offset, as the sprite outline's: sharp from the distance there up to a
                // texel of softness, the half resolution blur past three, blended between. Texels outside this capture hold
                // another capture's field, so a sample out there is no shadow at all. A field captured without a shadow
                // reads 0 in G, so drawn while the shadow is on it gives the sharp edge alone, right as far as it goes.
                float frameHeight = floor(IN.shadow.w / 4096);
                float2 frameEnd = float2(IN.shadow.w - frameHeight * 4096, frameHeight) - 0.5;
                float2 shadowTexels = texels - IN.shadow.xy;
                float framed = all(shadowTexels >= 0.5) && all(shadowTexels <= frameEnd) ? 1 : 0;
                float2 shifted = tex2D(_MainTex, shadowTexels / IN.texcoord.w).rg;
                float sharp = saturate((IN.texcoord.z + 0.5 - shifted.r) / max(aa, IN.shadow.z) + 0.5);
                float shadow = lerp(saturate(shifted.g), sharp, 1 - saturate((IN.shadow.z - 1) / 2)) * framed;

                // The ring, white like an Image's texture and tinted by Graphic.color through the vertex colour as every
                // Graphic is, over the shadow; neither covers the silhouette, and fading the Graphic fades both.
                float ringA = outer * inner * IN.color.a;
                float shadowA = shadow * inner * IN.shadowColor.a * IN.color.a * (1 - ringA);
                float alpha = ringA + shadowA;
                half4 color = half4(alpha > 0 ? (IN.color.rgb * ringA + IN.shadowColor.rgb * shadowA) / alpha : 0, alpha);
                #endif

                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                color.a *= m.x * m.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}