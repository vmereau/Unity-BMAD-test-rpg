// Surface of a Water_Volume. Everything is in world space, so scaling the prefab never stretches the ripples, and no
// texture is needed: the ripple normal is the gradient of scrolling value noise. Needs the URP depth and opaque textures
// (both enabled on PC_RPAsset). Only the top face is drawn; the water is invisible from below.
Shader "Game/Water"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.20, 0.30, 0.26, 1)
        _DeepColor ("Deep Color", Color) = (0.05, 0.10, 0.10, 1)
        _DepthRange ("Depth Range (m)", Float) = 2.5
        _Clarity ("Clarity (m)", Float) = 1.5
        _FoamColor ("Foam Color", Color) = (0.75, 0.75, 0.68, 1)
        _FoamWidth ("Foam Width (m)", Float) = 0.15
        _RippleScale ("Ripple Scale", Float) = 0.8
        _RippleStrength ("Ripple Strength", Range(0, 1)) = 0.06
        _RippleSpeed ("Ripple Speed", Float) = 0.35
        _Refraction ("Refraction", Range(0, 0.1)) = 0.02
        _SpecularPower ("Specular Power", Float) = 180
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 0.45
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }

        Pass
        {
            Name "WaterSurface"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthRange;
                float _Clarity;
                half4 _FoamColor;
                float _FoamWidth;
                float _RippleScale;
                float _RippleStrength;
                float _RippleSpeed;
                float _Refraction;
                float _SpecularPower;
                float _ReflectionStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float fogFactor : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), u.x),
                            lerp(Hash(cell + float2(0, 1)), Hash(cell + float2(1, 1)), u.x), u.y);
            }

            // Two layers scrolling in different directions, so the pattern never reads as a sliding sheet.
            float RippleHeight(float2 p)
            {
                float t = _Time.y * _RippleSpeed;
                return ValueNoise(p * _RippleScale + float2(t, t * 0.6)) * 0.6
                     + ValueNoise(p * _RippleScale * 2.7 + float2(-t * 1.3, t)) * 0.4;
            }

            float3 RippleNormal(float2 p)
            {
                const float eps = 0.05;
                float h = RippleHeight(p);
                float dx = RippleHeight(p + float2(eps, 0)) - h;
                float dz = RippleHeight(p + float2(0, eps)) - h;
                return normalize(float3(-dx / eps * _RippleStrength, 1.0, -dz / eps * _RippleStrength));
            }

            // Eye-space distance from the water surface to the opaque surface behind this screen position.
            float DepthBehind(float2 uv, float surfaceEyeDepth)
            {
                return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams) - surfaceEyeDepth;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normalWS = RippleNormal(input.positionWS.xz);
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float surfaceEyeDepth = LinearEyeDepth(input.positionWS, GetWorldToViewMatrix());

                // Refracted lookup. An object in front of the water must not bleed in: fall back to the straight UV.
                float2 sceneUV = screenUV + normalWS.xz * _Refraction;
                float depth = DepthBehind(sceneUV, surfaceEyeDepth);
                if (depth < 0.0)
                {
                    sceneUV = screenUV;
                    depth = DepthBehind(screenUV, surfaceEyeDepth);
                }
                depth = max(depth, 0.0);

                half3 scene = SampleSceneColor(sceneUV);
                Light mainLight = GetMainLight();
                half3 lighting = mainLight.color * saturate(dot(normalWS, mainLight.direction)) * 0.6 + SampleSH(normalWS);

                half3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate(depth / max(_DepthRange, 0.001))) * lighting;
                half3 color = lerp(scene, waterColor, saturate(depth / max(_Clarity, 0.001)));

                // Fresnel on a flatter normal: the full ripple normal flips it pixel by pixel at grazing angles (blotches).
                float3 fresnelNormal = normalize(lerp(float3(0, 1, 0), normalWS, 0.3));
                float fresnel = pow(1.0 - saturate(dot(fresnelNormal, viewDirWS)), 5.0);
                half3 reflection = GlossyEnvironmentReflection(reflect(-viewDirWS, normalWS), input.positionWS, 0.05, 1.0, screenUV);
                color = lerp(color, reflection, saturate(fresnel * _ReflectionStrength + 0.02));

                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                color += mainLight.color * pow(saturate(dot(normalWS, halfDir)), _SpecularPower) * 0.5;

                float foamBreakup = ValueNoise(input.positionWS.xz * 4.0 + _Time.y * 0.1);
                float foam = saturate(1.0 - depth / max(_FoamWidth, 0.001)) * step(0.35, foamBreakup);
                color = lerp(color, _FoamColor.rgb * lighting, foam);

                color = MixFog(color, input.fogFactor);

                // Fade out on contact so the mesh edge never shows a hard line against the shore.
                return half4(color, saturate(depth / 0.05));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
