// 바다 셰이더. 구역 바깥 전부를 덮는 평면 하나에 쓴다(SeaSurface가 만든다).
//
// 한 장에서 하는 일
//  - 거스트너 파도 2겹으로 정점을 움직이고, 면 법선을 화면 미분으로 구해 로우폴리처럼 각지게 칠한다.
//  - 물 뒤에 있는 것까지의 거리(깊이 텍스처)로 얕은 색 → 깊은 색, 그리고 해안 거품을 만든다.
//    섬이 물을 뚫고 올라오면 거품이 그 높이를 따라 저절로 생긴다 — 섬과 물은 서로 몰라도 된다.
//  - 섬이 솟을 때 퍼지는 물결 고리. 중심과 시작 시각은 SeaSurface.PlayRipple이 전역 값으로 넣는다.
//
// Shader Graph가 아닌 이유: 노드 그래프는 텍스트로 검토·병합하기 어렵다. 조절할 값은 전부 머티리얼에 노출했다.
// URP 설정에서 Depth Texture가 켜져 있어야 거품·물빛이 동작한다(PC_RPAsset은 켜져 있음).
Shader "ForagerCP/Water"
{
    Properties
    {
        [Header(Color)]
        _ShallowColor ("얕은 물", Color) = (0.36, 0.80, 0.82, 0.55)
        _DeepColor ("깊은 물", Color) = (0.10, 0.36, 0.55, 0.92)
        _DepthFade ("깊어지는 거리(m)", Range(0.1, 8)) = 2.0

        [Header(Foam)]
        _FoamColor ("거품", Color) = (0.95, 0.98, 1, 1)
        _FoamWidth ("거품 폭(m)", Range(0, 1.5)) = 0.35
        _FoamNoiseScale ("거품 무늬 크기", Range(0.5, 8)) = 2.5
        _FoamSpeed ("거품 흐름 속도", Range(0, 2)) = 0.3
        _BandSpacing ("밀려오는 물결 간격(m)", Range(0.3, 4)) = 1.2
        _BandSpeed ("밀려오는 속도", Range(0, 2)) = 0.35
        _BandRange ("물결이 보이는 거리(m)", Range(0, 6)) = 2.2
        _BandWidth ("물결 줄 굵기", Range(0.02, 0.5)) = 0.12

        [Header(Waves)]
        _Wave1 ("파도 1 (방향XZ, 가파름, 파장)", Vector) = (1, 0.3, 0.12, 6)
        _Wave2 ("파도 2 (방향XZ, 가파름, 파장)", Vector) = (-0.4, 1, 0.08, 3.5)
        _WaveSpeed ("파도 속도", Range(0, 3)) = 0.8

        [Header(Light)]
        _Specular ("반짝임", Range(0, 1)) = 0.35
        _Smoothness ("매끈함", Range(8, 256)) = 64
        _ShadowStrength ("그림자 진하기", Range(0, 1)) = 0.6

        [Header(Ripple)]
        _RippleColor ("물결 고리 색", Color) = (1, 1, 1, 0.5)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthFade;
                half4 _FoamColor;
                float _FoamWidth;
                float _FoamNoiseScale;
                float _FoamSpeed;
                float _BandSpacing;
                float _BandSpeed;
                float _BandRange;
                float _BandWidth;
                float4 _Wave1;
                float4 _Wave2;
                float _WaveSpeed;
                float _Specular;
                float _Smoothness;
                float _ShadowStrength;
                half4 _RippleColor;
            CBUFFER_END

            // SeaSurface.PlayRipple이 넣는 전역 값. 머티리얼마다 따로 둘 이유가 없다.
            float4 _SeaRippleCenter;   // xyz 중심, w 시작 시각(_Time.y 기준)
            float4 _SeaRippleParams;   // x 퍼지는 속도, y 고리 폭, z 높이, w 지속 시간

            // SeaSurface가 구역 셀로 만든 해안 거리(m). 바다 +, 땅 아래 −. 내려다보는 카메라에서도
            // 섬 네 면에 고르게 거품이 생기게 하려고 깊이 대신 이걸 주로 쓴다.
            TEXTURE2D(_SeaShoreTex);
            SAMPLER(sampler_SeaShoreTex);
            float4 _SeaShoreRect;      // xy 월드 XZ 시작, zw 크기

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float ripple : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            // 거스트너 파도 하나. 점을 위아래뿐 아니라 마루 쪽으로도 모아서 마루가 뾰족해진다.
            float3 Gerstner(float4 wave, float2 xz, float time)
            {
                float2 dir = normalize(wave.xy);
                float steepness = wave.z;
                float wavelength = max(wave.w, 0.01);

                float k = TWO_PI / wavelength;
                float c = sqrt(9.8 / k);
                float f = k * (dot(dir, xz) - c * time);
                float a = steepness / k;

                return float3(dir.x * a * cos(f), a * sin(f), dir.y * a * cos(f));
            }

            // 고리 하나의 세기(0~1). 시작 전·끝난 뒤에는 0.
            float RippleAt(float2 xz)
            {
                float duration = max(_SeaRippleParams.w, 0.001);
                float elapsed = _Time.y - _SeaRippleCenter.w;
                if (elapsed < 0 || elapsed > duration) return 0;

                float radius = elapsed * _SeaRippleParams.x;
                float width = max(_SeaRippleParams.y, 0.01);
                float d = distance(xz, _SeaRippleCenter.xz);
                float ring = exp(-pow((d - radius) / width, 2));
                return ring * (1 - elapsed / duration);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float time = _Time.y * _WaveSpeed;

                positionWS += Gerstner(_Wave1, positionWS.xz, time);
                positionWS += Gerstner(_Wave2, positionWS.xz, time);

                float ripple = RippleAt(positionWS.xz);
                positionWS.y += ripple * _SeaRippleParams.z;

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.ripple = ripple;
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
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3 - 2 * f);

                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 면 법선. 정점마다 법선을 두지 않고 화면 미분으로 구하면 면이 각지게 칠해진다.
                float3 normalWS = normalize(cross(ddy(input.positionWS), ddx(input.positionWS)));
                if (normalWS.y < 0) normalWS = -normalWS;

                // 해안까지의 거리(m). 땅 아래(음수)는 거품 없이 얕은 물로 둔다.
                float2 shoreUV = (input.positionWS.xz - _SeaShoreRect.xy) / max(_SeaShoreRect.zw, 0.001);
                float shoreSigned = SAMPLE_TEXTURE2D(_SeaShoreTex, sampler_SeaShoreTex, shoreUV).r;
                float shore = max(0, shoreSigned);
                float underLand = step(shoreSigned, -0.05);

                // 땅 칸 안의 물은 그리지 않는다. 파도 마루(+0.1m 남짓)가 몸통 윗면이나 바닥 타일 틈을 뚫고 올라온다.
                clip(shoreSigned + 0.05);

                // 보조: 물 표면부터 그 뒤에 보이는 것까지의 거리. 셀 밖 물체(떨어진 아이템 등)에 닿는 거품용.
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float surfaceEye = input.screenPos.w;
                float thickness = min(max(0, sceneEye - surfaceEye), shore);

                float depth01 = saturate(thickness / _DepthFade);
                half4 water = lerp(_ShallowColor, _DeepColor, depth01);

                // 빛. 물은 반투명이라 그림자를 진하게 받으면 얼룩처럼 보여서 세기를 따로 둔다.
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float shadow = lerp(1, mainLight.shadowAttenuation, _ShadowStrength);
                float ndl = saturate(dot(normalWS, mainLight.direction)) * 0.5 + 0.5;
                half3 ambient = SampleSH(normalWS);

                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float3 halfDir = normalize(mainLight.direction + viewDir);
                float spec = pow(saturate(dot(normalWS, halfDir)), _Smoothness) * _Specular * shadow;

                half3 color = water.rgb * (mainLight.color * ndl * shadow + ambient) + spec * mainLight.color;

                // 거품: 무언가와 맞닿은 곳(두께가 얇은 곳)에서 생긴다. 무늬로 가장자리를 들쑥날쑥하게.
                float2 foamUV = input.positionWS.xz * _FoamNoiseScale + _Time.y * _FoamSpeed;
                float noise = ValueNoise(foamUV) * 0.6 + ValueNoise(foamUV * 2.3 + 17) * 0.4;
                float edge = 1 - saturate(thickness / max(_FoamWidth, 0.001));
                float foam = step(noise, edge * 1.2);

                // 해안으로 밀려오는 물결 줄. 해안에서 멀어질수록 옅어진다.
                float band = frac(shore / max(_BandSpacing, 0.01) + _Time.y * _BandSpeed);
                float bandLine = step(1 - _BandWidth, band) * step(noise * 0.6, 1 - saturate(shore / max(_BandRange, 0.001)));
                foam = max(foam, bandLine) * (1 - underLand);

                // 물결 고리는 거품 색을 섞어 밝게 띄운다.
                float ring = saturate(input.ripple) * _RippleColor.a;

                half3 rgb = lerp(color, _FoamColor.rgb, saturate(foam * _FoamColor.a + ring));
                half alpha = saturate(max(water.a, max(foam * _FoamColor.a, ring)));

                rgb = MixFog(rgb, input.fogFactor);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
