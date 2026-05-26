Shader "Hidden/AnimePostProcess"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off

        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // Input textures
        TEXTURE2D_X(_MaskTex);
        TEXTURE2D_X(_BloomTex);
        
        // Settings
        float4 _SourceSize; // z = 1/w, w = 1/h
        
        // Bloom
        float4 _BloomTint;
        float _BloomThreshold;
        float _BloomRadius;
        
        // Outline
        #if defined(_CHARACTER_OUTLINE_ON)
            TEXTURE2D_X_FLOAT(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);
        #endif

        float _OutlineIntensity;
        float4 _OutlineColor;
        float _OutlineThickness;
        float _OutlineDepthThreshold;
        float _OutlineNormalThreshold;
        
        // Cinematic
        float _RadialBlurIntensity;
        float2 _RadialBlurCenter;
        float _BackgroundDesat;

        float Luma(float3 c)
        {
            return dot(c, float3(0.2126, 0.7152, 0.0722));
        }

        float4 SampleBlit(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
        }

        float SampleAnimeSilhouette(float2 uv)
        {
            float4 mask = SAMPLE_TEXTURE2D_X(_MaskTex, sampler_LinearClamp, uv);
            return saturate(max(mask.r, max(mask.g, mask.b)));
        }

        ENDHLSL

        // Pass 0: Extract
        Pass
        {
            Name "Extract"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float4 color = SampleBlit(uv);
                
                // MaskTex: RGB = Silhouette (1.0), A = BloomMask (postMask)
                float4 maskData = SAMPLE_TEXTURE2D_X(_MaskTex, sampler_LinearClamp, uv);
                float silhouette = saturate(max(maskData.r, max(maskData.g, maskData.b)));
                float bloomMask = saturate(maskData.a);
                
                // Pre-filter bloom (Threshold & Knee)
                float brightness = max(max(color.r, color.g), color.b);
                float threshold = saturate((brightness - _BloomThreshold) / max(0.0001, 1.0 - _BloomThreshold));
                
                float3 extractedBloom = color.rgb * threshold * bloomMask * _BloomTint.rgb;
                
                // Output Bloom RGB, and Silhouette in Alpha
                return float4(extractedBloom, silhouette);
            }
            ENDHLSL
        }

        // Pass 1: Downsample (Dual Kawase)
        Pass
        {
            Name "Downsample"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float4 d = _SourceSize.zwzw * float4(-1.0, -1.0, 1.0, 1.0);
                
                float4 s = SampleBlit(uv) * 4.0;
                s += SampleBlit(uv + d.xy);
                s += SampleBlit(uv + d.zy);
                s += SampleBlit(uv + d.xw);
                s += SampleBlit(uv + d.zw);
                
                return s * 0.125;
            }
            ENDHLSL
        }

        // Pass 2: Upsample (Dual Kawase)
        Pass
        {
            Name "Upsample"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float4 d = _SourceSize.zwzw * float4(-1.0, -1.0, 1.0, 1.0) * _BloomRadius;
                
                float4 s = SampleBlit(uv + float2(d.x, 0.0)) * 2.0;
                s += SampleBlit(uv + float2(0.0, d.y)) * 2.0;
                s += SampleBlit(uv + float2(d.z, 0.0)) * 2.0;
                s += SampleBlit(uv + float2(0.0, d.w)) * 2.0;
                
                s += SampleBlit(uv + d.xy);
                s += SampleBlit(uv + d.zy);
                s += SampleBlit(uv + d.xw);
                s += SampleBlit(uv + d.zw);
                
                return s * 0.0833333; // 1/12
            }
            ENDHLSL
        }

        // Pass 3: Composite
        Pass
        {
            Name "Composite"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ _CHARACTER_OUTLINE_ON
            #pragma multi_compile_local _ _CINEMATIC_MODE_ON

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float4 sceneColor = SampleBlit(uv);
                
                // _BloomTex contains Bloom in RGB, Silhouette in Alpha
                float4 bloomData = SAMPLE_TEXTURE2D_X(_BloomTex, sampler_LinearClamp, uv);
                float3 bloom = bloomData.rgb;
                float silhouette = saturate(bloomData.a);
                
                float3 finalColor = sceneColor.rgb;

                // Cinematic Background Treatment
                #if defined(_CINEMATIC_MODE_ON)
                    if (silhouette < 0.1) // Background
                    {
                        // Radial Blur
                        float2 dir = _RadialBlurCenter - uv;
                        float dist = length(dir);
                        dir = normalize(dir);
                        float blurAmount = smoothstep(0.1, 0.8, dist) * _RadialBlurIntensity;
                        
                        float3 radialColor = finalColor;
                        if (blurAmount > 0.0)
                        {
                            radialColor = 0;
                            const int samples = 8;
                            for (int i = 0; i < samples; i++)
                            {
                                float offset = (i / (float)samples) * blurAmount;
                                radialColor += SampleBlit(uv + dir * offset).rgb;
                            }
                            radialColor /= samples;
                        }
                        
                        // Desaturation & Darken
                        float luma = Luma(radialColor);
                        float3 desatColor = float3(luma, luma, luma);
                        finalColor = lerp(radialColor, desatColor, _BackgroundDesat);
                        finalColor *= (1.0 - _BackgroundDesat * 0.5); // Darken slightly
                    }
                #endif

                // Add Bloom
                finalColor += bloom;

                // Multi-Layer Outline
                #if defined(_CHARACTER_OUTLINE_ON)
                    float outlineWidth = _OutlineThickness;
                    float2 deltaX = float2(_SourceSize.z, 0.0) * outlineWidth;
                    float2 deltaY = float2(0.0, _SourceSize.w) * outlineWidth;

                    float centerMask = SampleAnimeSilhouette(uv);
                    float s0 = SampleAnimeSilhouette(uv - deltaX - deltaY);
                    float s1 = SampleAnimeSilhouette(uv + deltaX + deltaY);
                    float s2 = SampleAnimeSilhouette(uv + deltaX - deltaY);
                    float s3 = SampleAnimeSilhouette(uv - deltaX + deltaY);
                    float s4 = SampleAnimeSilhouette(uv - deltaX);
                    float s5 = SampleAnimeSilhouette(uv + deltaX);
                    float s6 = SampleAnimeSilhouette(uv - deltaY);
                    float s7 = SampleAnimeSilhouette(uv + deltaY);

                    float dilatedMask = max(centerMask, max(max(max(s0, s1), max(s2, s3)), max(max(s4, s5), max(s6, s7))));
                    float erodedMask = min(centerMask, min(min(min(s0, s1), min(s2, s3)), min(min(s4, s5), min(s6, s7))));
                    float outsideEdge = saturate(dilatedMask - centerMask);
                    float maskInnerEdge = saturate(centerMask - erodedMask) * 0.35;

                    float d0 = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv - deltaX - deltaY).r;
                    float d1 = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv + deltaX + deltaY).r;
                    float d2 = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv + deltaX - deltaY).r;
                    float d3 = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv - deltaX + deltaY).r;
                    float depthEdge = abs(LinearEyeDepth(d0, _ZBufferParams) - LinearEyeDepth(d1, _ZBufferParams)) +
                        abs(LinearEyeDepth(d2, _ZBufferParams) - LinearEyeDepth(d3, _ZBufferParams));

                    float innerRegion = centerMask * erodedMask;
                    float screenInnerEdge = step(_OutlineDepthThreshold, depthEdge) * innerRegion;

                    float edge = saturate(outsideEdge + max(maskInnerEdge, screenInnerEdge)) * _OutlineIntensity;
                    float3 outlineColor = _OutlineColor.rgb;
                    
                    finalColor = lerp(finalColor, outlineColor, edge);
                #endif

                return float4(finalColor, sceneColor.a);
            }
            ENDHLSL
        }
    }
}
