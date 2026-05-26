Shader "Universal Render Pipeline/Decal_Mini"
{
    Properties 
    { 
        _StencilRef ("Stencil Reference", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Float) = 8
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Decal_Pass_Mini" 
            Cull Front
            ZTest Greater 
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            Stencil
            {
                Ref [_StencilRef]
                Comp [_StencilComp]
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTERED_RENDERING
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT 

            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4x4 _InvVP; 
                half4 _DecalFadeParams; 
            CBUFFER_END

            #include "DecalDataMini.generated.hlsl"

            StructuredBuffer<DecalDataMini> _DecalDataBuffer;
            TEXTURE2D_ARRAY(_DecalAtlasArray);
            SAMPLER(sampler_DecalAtlasArray);

            TEXTURE2D_ARRAY(_DecalNormalArray);
            SAMPLER(sampler_DecalNormalArray);
            
            float4x4 GetDecalToWorld(DecalDataMini data) { return float4x4(data.dtw0, data.dtw1, data.dtw2, data.dtw3); }
            float4x4 GetWorldToDecal(DecalDataMini data) { return float4x4(data.wtd0, data.wtd1, data.wtd2, data.wtd3); }

            float2 RotateUV(float2 uv, float rotationSpeed, float scale)
            {
                float angle = fmod(_Time.y * rotationSpeed, 6.2831853);
                float s = sin(angle);
                float c = cos(angle);
                float2 center = 0.5;
                uv -= center;
                uv /= max(0.0001, scale);
                float2 rotatedUV;
                rotatedUV.x = uv.x * c - uv.y * s;
                rotatedUV.y = uv.x * s + uv.y * c;
                rotatedUV += center;
                return rotatedUV;
            }

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            // 工业级过程式立方体查找表
            static float3 CubeVertices[8] = {
                float3(-0.5, -0.5, -0.5), float3(0.5, -0.5, -0.5), float3(0.5, 0.5, -0.5), float3(-0.5, 0.5, -0.5),
                float3(-0.5, -0.5, 0.5),  float3(0.5, -0.5, 0.5),  float3(0.5, 0.5, 0.5),  float3(-0.5, 0.5, 0.5)
            };

            static int CubeIndices[36] = {
                0, 2, 1, 0, 3, 2, // Back
                4, 5, 6, 4, 6, 7, // Front
                0, 1, 5, 0, 5, 4, // Bottom
                2, 3, 7, 2, 7, 6, // Top
                0, 4, 7, 0, 7, 3, // Left
                1, 2, 6, 1, 6, 5  // Right
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.instanceID = input.instanceID;

                DecalDataMini data = _DecalDataBuffer[input.instanceID];
                float4x4 dtw = GetDecalToWorld(data);

                // 核心：根据顶点 ID 自动获取对应的立方体角点
                float3 posOS = CubeVertices[CubeIndices[input.vertexID]];

                float3 centerWS = float3(dtw[0].w, dtw[1].w, dtw[2].w);
                float scaleX = length(float3(dtw[0].x, dtw[1].x, dtw[2].x));
                float scaleY = length(float3(dtw[0].y, dtw[1].y, dtw[2].y));
                float scaleZ = length(float3(dtw[0].z, dtw[1].z, dtw[2].z));
                float maxScale = max(scaleX, max(scaleY, scaleZ));
                float radius = maxScale * 0.866;

                bool isVisible = true;
                float dist = distance(centerWS, _WorldSpaceCameraPos);
                if (dist > _DecalFadeParams.x + radius) isVisible = false;

                float4 positionWS = mul(dtw, float4(posOS, 1.0));
                output.positionWS = positionWS.xyz;
                float4 posCS = TransformWorldToHClip(positionWS.xyz);

                if (isVisible)
                {
                    output.positionCS = posCS;
                }
                else
                {
                    output.positionCS = float4(0, 0, 2, 1); 
                }

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                DecalDataMini data = _DecalDataBuffer[input.instanceID];
                float4x4 wtd = GetWorldToDecal(data);
                float4x4 dtw = GetDecalToWorld(data);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);
                // 1. 跨平台深度重建 (去除深度的魔改偏移，保证世界坐标重建精度)
                #if !defined(UNITY_REVERSED_Z)
                    // OpenGL: 将深度采样 [0, 1] 映射到平台原生的 NDC 范围 [-1, 1]
                    float deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #else
                    float deviceDepth = rawDepth;
                #endif

                float3 positionWS = ComputeWorldSpacePosition(screenUV, deviceDepth, _InvVP);

                float3 positionLS = mul(wtd, float4(positionWS, 1.0)).xyz;
                float3 distToEdge = 0.5 - abs(positionLS);
                
                // 在 clip 阶段施加容差 (0.001)，避免浮点精度或分辨率缩放导致贴花在盒子边界被错误剔除
                clip(min(distToEdge.x, min(distToEdge.y, distToEdge.z)) + 0.001);

                // UV & Fade
                float2 uv = positionLS.xy + 0.5;
                float softFactor = data.fadeParams.w;
                float softFade = 1.0;
                
                if (softFactor > 0.001)
                {
                    float3 s = smoothstep(0, softFactor, distToEdge);
                    softFade = s.x * s.y * s.z;
                }

                // Animation & Mode
                float mode = data.animParams2.z;
                half4 col = 1;
                float index = floor(data.fadeParams.z + 0.5);

                if (mode > 0.5)
                {
                    // [AURA MODE]
                    col = 0;
                    float4 auraColors[4] = { data.color, data.auraColor2, data.auraColor3, data.auraColor4 };
                    float4 rotSpeeds = data.auraRotSpeeds;
                    float4 pulseSpeeds = data.auraPulseParams;
                    float4 scales = data.auraScaleParams;

                    // Layer 1 (R)
                    float2 uv1 = RotateUV(uv, rotSpeeds.x, scales.x);
                    half mask1 = SAMPLE_TEXTURE2D_ARRAY(_DecalAtlasArray, sampler_DecalAtlasArray, uv1, index).r;
                    col += mask1 * (half4)auraColors[0] * (1.0 + sin(_Time.y * pulseSpeeds.x) * data.animParams.z);

                    // Layer 2 (G)
                    float2 uv2 = RotateUV(uv, rotSpeeds.y, scales.y);
                    half mask2 = SAMPLE_TEXTURE2D_ARRAY(_DecalAtlasArray, sampler_DecalAtlasArray, uv2, index).g;
                    col += mask2 * (half4)auraColors[1] * (1.0 + sin(_Time.y * pulseSpeeds.y) * data.animParams.z);

                    // Layer 3 (B)
                    float2 uv3 = RotateUV(uv, rotSpeeds.z, scales.z);
                    half mask3 = SAMPLE_TEXTURE2D_ARRAY(_DecalAtlasArray, sampler_DecalAtlasArray, uv3, index).b;
                    col += mask3 * (half4)auraColors[2] * (1.0 + sin(_Time.y * pulseSpeeds.z) * data.animParams.z);

                    // Layer 4 (A)
                    float2 uv4 = RotateUV(uv, rotSpeeds.w, scales.w);
                    half mask4 = SAMPLE_TEXTURE2D_ARRAY(_DecalAtlasArray, sampler_DecalAtlasArray, uv4, index).a;
                    col += mask4 * (half4)auraColors[3] * (1.0 + sin(_Time.y * pulseSpeeds.w) * data.animParams.z);
                }
                else
                {
                    // [STANDARD MODE]
                    float rotSpeed = data.animParams.x;
                    uv = RotateUV(uv, rotSpeed, 1.0);
                    uv = uv * data.uvScaleOffset.xy + data.uvScaleOffset.zw;

                    float pulseFreq = data.animParams.y;
                    float pulseIntensity = data.animParams.z;
                    float pulse = 1.0;
                    if (abs(pulseFreq) > 0.001)
                    {
                        pulse = 1.0 + sin(_Time.y * pulseFreq) * pulseIntensity;
                    }

                    // Flipbook
                    float baseIndex = data.fadeParams.z;
                    float flipCount = data.animParams2.x;
                    if (flipCount > 1.1)
                    {
                        float flipSpeed = data.animParams2.y;
                        float frameIdx = floor(_Time.y * flipSpeed) % flipCount;
                        baseIndex += frameIdx;
                    }
                    index = floor(baseIndex + 0.5);

                    col = SAMPLE_TEXTURE2D_ARRAY(_DecalAtlasArray, sampler_DecalAtlasArray, uv, index);
                    col *= (half4)data.color;
                    col *= pulse;
                }

                // Normal & Lighting
                float distToCenter = distance(positionLS.xy + 0.5, 0.5);
                float softness = data.animParams.w;
                float isMaskEnabled = saturate(softness * 1000.0); 
                float radialFadeMask = smoothstep(0.5, 0.5 - max(0.001, softness), distToCenter);
                float radialFade = lerp(1.0, radialFadeMask, isMaskEnabled);
                
                float3 decalForwardWS = normalize(float3(dtw[0].z, dtw[1].z, dtw[2].z));
                float3 decalRightWS = normalize(float3(dtw[0].x, dtw[1].x, dtw[2].x));
                
                float3 positionWS_X = ddx(positionWS);
                float3 positionWS_Y = ddy(positionWS);
                
                float3 crossProduct = cross(positionWS_X, positionWS_Y) * _ProjectionParams.x;
                float crossLen = length(crossProduct);
                
                // 1. 获取原始表面法线 (使用安全归一化，避免对角线偏置 Bug)
                float3 rawGeoNormalWS = crossLen > 1e-6 ? (crossProduct / crossLen) : -decalForwardWS;
                
                // 工业级偏导法线平滑稳定技术：当摄像机变远时，屏幕偏导数法线由于深度精度衰减产生严重的像素抖动。
                // 我们基于到摄像机的实际距离 (在 8 米到 25 米之间) 平滑混合到稳定的贴花投影轴向 (-decalForwardWS)
                float dist = distance(positionWS, _WorldSpaceCameraPos);
                float stabilizeWeight = saturate((dist - 8.0) / 17.0);
                rawGeoNormalWS = normalize(lerp(rawGeoNormalWS, -decalForwardWS, stabilizeWeight));
                
                // 2. 修正视点朝向 (使用安全非零判断，避免 dot 为 0 时法线塌缩)
                float3 viewDirWS = normalize(_WorldSpaceCameraPos - positionWS);
                float dotNV = dot(rawGeoNormalWS, viewDirWS);
                half3 finalGeoNormalWS = rawGeoNormalWS * (dotNV >= 0.0 ? 1.0h : -1.0h);
                
                // 3. 法线解包与多平台适配
                float4 packedNormal = SAMPLE_TEXTURE2D_ARRAY(_DecalNormalArray, sampler_DecalNormalArray, uv, index);
                half3 tangentNormal = UnpackNormal(packedNormal);

                // [多平台适配] 处理不同 API 下的 Y 轴翻转问题
                #if UNITY_UV_STARTS_AT_TOP
                    // DirectX 类平台通常需要反转 Y 以匹配图集烘焙顺序
                    tangentNormal.y *= -1.0;
                #endif

                // 4. 构建 TBN 矩阵 (基于已稳定的原始法线，确保切线空间的平滑与连续)
                float3 worldTangent = normalize(decalRightWS - rawGeoNormalWS * dot(decalRightWS, rawGeoNormalWS));
                float3 worldBitangent = cross(rawGeoNormalWS, worldTangent);
                
                // 最终法线合成 (使用 finalGeoNormalWS 确保双面受光正确)
                half3 normalWS = normalize(tangentNormal.x * worldTangent + tangentNormal.y * worldBitangent + tangentNormal.z * finalGeoNormalWS);

                // 3. 完整光照系统 (增强兼容性版本)
                float3 shadowPosWS = positionWS + normalWS * 0.02;
                float4 shadowCoord = TransformWorldToShadowCoord(shadowPosWS);
                
                // --- 主灯 ---
                Light mainLight = GetMainLight(shadowCoord);
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                // 强制忽略主灯的距离衰减 (太阳光不需要)，只保留阴影衰减
                half3 lighting = mainLight.color * (NdotL * mainLight.shadowAttenuation);

                // --- 附加灯 ---
                #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
                {
                    // 修正：附加灯不应该使用主灯的 shadowCoord
                    Light light = GetAdditionalLight(lightIndex, positionWS); 
                    half NdotL_Add = saturate(dot(normalWS, light.direction));
                    lighting += light.color * (NdotL_Add * light.distanceAttenuation);
                }
                #endif

                
                // 4. 环境光与球谐函数增强 (解决自定义 Pass 可能丢失 SH 的问题)
                half3 ambientSH = half3(SampleSH(normalWS));
                
                // 如果球谐采样结果接近全黑 (通常发生在自定义 Pass 丢失上下文时)
                // 采用三色渐变环境光模型作为保底，确保法线贴图的立体感
                if (dot(ambientSH, ambientSH) < 0.0001)
                {
                    float up = saturate(normalWS.y);
                    float side = 1.0 - abs(normalWS.y);
                    float down = saturate(-normalWS.y);
                    ambientSH = unity_AmbientSky.rgb * up + 
                                unity_AmbientEquator.rgb * side + 
                                unity_AmbientGround.rgb * down;
                }

                lighting += ambientSH;
                
                col.rgb *= lighting;

                // Fade & Alpha
                half NdotF = saturate(half(dot(rawGeoNormalWS, -decalForwardWS)));
                half threshold = half(data.fadeParams.x);
                half angleFade = saturate((NdotF - threshold) / (max(0.01h, 1.0h - threshold)));
                angleFade = angleFade * angleFade;

                half distFade = saturate((_DecalFadeParams.x - half(dist)) / _DecalFadeParams.y);
                
                col.a *= distFade * angleFade * softFade * radialFade;

                // ===== DEBUG  =====
                
                // 颜色映射规则：X(红)向右，Y(绿)向上，Z(蓝)向前。0.5代表0，1代表1，0代表-1
                // return half4(rawGeoNormalWS * 0.5 + 0.5, 1.0); // 调试几何法线 (未经法线贴图修改)
                // return half4(normalWS * 0.5 + 0.5, 1.0);    // 调试最终法线 (受贴花法线贴图影响后)

                return col;
            }
            ENDHLSL
        }
    }
}
