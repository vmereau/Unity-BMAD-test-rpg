// Full-screen composite for InteractionOutlineFeature: dilates the R8 outline mask (_BlitTexture) in a
// ring of _OutlineWidth pixels and blends _InteractionOutlineColor where the dilated mask covers a pixel
// the mask itself does not — i.e. a constant-width edge outside the silhouette, interior left untouched.
Shader "Hidden/Game/InteractionOutlineComposite"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "InteractionOutlineComposite"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #define OUTLINE_DIRECTIONS 12

            float _OutlineWidth;
            float4 _OutlineMaskTexelSize;    // xy = 1 / mask size, set from C#
            half4 _InteractionOutlineColor;  // global, set by InteractionSystem

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                half center = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).r;
                if (center > 0.5h)
                    return half4(0, 0, 0, 0);

                float2 radius = _OutlineWidth * _OutlineMaskTexelSize.xy;
                half edge = 0;

                [unroll]
                for (int i = 0; i < OUTLINE_DIRECTIONS; i++)
                {
                    float angle = i * (TWO_PI / OUTLINE_DIRECTIONS);
                    float2 dir = float2(cos(angle), sin(angle));
                    edge = max(edge, SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv + dir * radius).r);
                    edge = max(edge, SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv + dir * radius * 0.5).r);
                }

                return half4(_InteractionOutlineColor.rgb, edge * _InteractionOutlineColor.a);
            }
            ENDHLSL
        }
    }
}
