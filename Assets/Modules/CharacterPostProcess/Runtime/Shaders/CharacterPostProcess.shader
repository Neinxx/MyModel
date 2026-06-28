Shader "Hidden/CharacterPostProcess/Composite"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        HLSLINCLUDE
        #pragma target 3.0
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        TEXTURE2D_X(_CharacterPostProcess_BloomTex);
        TEXTURE2D_X(_CharacterPostProcess_MaskTex);
        float4 _CharacterBlurTexelSize;
        float4 _CharacterBloomTint;
        float _CharacterBloomThreshold;
        float _CharacterBlurRadius;
        float _CharacterBloomIntensity;
        float _CharacterColorBoost;
        float _CharacterEdgeGlowIntensity;

        #pragma multi_compile_local _ _CHARACTER_OUTLINE_ON

        #if defined(_CHARACTER_OUTLINE_ON)
            TEXTURE2D_X_FLOAT(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);
            TEXTURE2D_X_FLOAT(_CameraNormalsTexture);
            SAMPLER(sampler_CameraNormalsTexture);
        #endif

        float _CharacterOutlineIntensity;
        float4 _CharacterOutlineColor;
        float _CharacterOutlineThickness;
        float _CharacterOutlineDepthThreshold;
        float _CharacterOutlineNormalThreshold;

        float Luma(float3 c)
        {
            return dot(c, float3(0.2126, 0.7152, 0.0722));
        }

        float4 SampleBlit(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
        }

        float SampleCharacterSilhouette(float2 uv)
        {
            float4 mask = SAMPLE_TEXTURE2D_X(_CharacterPostProcess_MaskTex, sampler_LinearClamp, uv);
            // Character Mask Pass outputs RGB=1 for the silhouette
            return saturate(max(mask.r, max(mask.g, mask.b)));
        }

        float SampleCharacterBloomMask(float2 uv)
        {
            float4 mask = SAMPLE_TEXTURE2D_X(_CharacterPostProcess_MaskTex, sampler_LinearClamp, uv);
            // Character Mask Pass outputs mask1.a in the Alpha channel for bloom
            return saturate(mask.a);
        }

        ENDHLSL

        Pass
        {
            Name "Extract"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                float4 c = SampleBlit(input.texcoord);
                float silhouette = SampleCharacterSilhouette(input.texcoord);
                float bloomMask = SampleCharacterBloomMask(input.texcoord);
                
                // Return color multiplied by bloom mask for the bloom extraction pass
                // Return silhouette in the alpha channel for the composite pass to use!
                return float4(c.rgb * bloomMask, silhouette);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ExtractCharacterBloom"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                float4 c = SampleBlit(input.texcoord);
                float alphaMask = saturate(c.a);
                float brightness = max(max(c.r, c.g), c.b);
                float threshold = saturate((brightness - _CharacterBloomThreshold) / max(0.0001, 1.0 - _CharacterBloomThreshold));
                float brightnessMask = saturate(max(threshold, alphaMask * 0.08));
                float3 colorBloom = c.rgb * brightnessMask * _CharacterBloomTint.rgb;
                float3 silhouetteBloom = _CharacterBloomTint.rgb * alphaMask * 0.08;
                return float4(max(colorBloom, silhouetteBloom), alphaMask);
            }
            ENDHLSL
        }

        Pass
        {
            Name "BlurHorizontal"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 stepUV = float2(_CharacterBlurTexelSize.x * _CharacterBlurRadius, 0);

                float4 c = SampleBlit(uv) * 0.4026;
                c += SampleBlit(uv + stepUV * 1.3846) * 0.2442;
                c += SampleBlit(uv - stepUV * 1.3846) * 0.2442;
                c += SampleBlit(uv + stepUV * 3.2308) * 0.0545;
                c += SampleBlit(uv - stepUV * 3.2308) * 0.0545;
                return c;
            }
            ENDHLSL
        }

        Pass
        {
            Name "BlurVertical"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 stepUV = float2(0, _CharacterBlurTexelSize.y * _CharacterBlurRadius);

                float4 c = SampleBlit(uv) * 0.4026;
                c += SampleBlit(uv + stepUV * 1.3846) * 0.2442;
                c += SampleBlit(uv - stepUV * 1.3846) * 0.2442;
                c += SampleBlit(uv + stepUV * 3.2308) * 0.0545;
                c += SampleBlit(uv - stepUV * 3.2308) * 0.0545;
                return c;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Composite"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float4 sceneColor = SampleBlit(uv);
                float4 bloom = SAMPLE_TEXTURE2D_X(_CharacterPostProcess_BloomTex, sampler_LinearClamp, uv);

                float characterMask = saturate(bloom.a);
                float edgeMask = saturate(Luma(bloom.rgb) * _CharacterEdgeGlowIntensity);
                float3 boostedCharacter = sceneColor.rgb * (1.0 + _CharacterColorBoost);
                float3 glow = bloom.rgb * _CharacterBloomIntensity + bloom.rgb * edgeMask;

                float3 finalColor = lerp(sceneColor.rgb, boostedCharacter, characterMask) + glow;

                #if defined(_CHARACTER_OUTLINE_ON)
                    float2 deltaX = float2(_ScreenParams.z - 1.0, 0.0) * _CharacterOutlineThickness;
                    float2 deltaY = float2(0.0, _ScreenParams.w - 1.0) * _CharacterOutlineThickness;

                    float centerMask = SampleCharacterSilhouette(uv);
                    float m0 = SampleCharacterSilhouette(uv - deltaX - deltaY);
                    float m1 = SampleCharacterSilhouette(uv + deltaX + deltaY);
                    float m2 = SampleCharacterSilhouette(uv + deltaX - deltaY);
                    float m3 = SampleCharacterSilhouette(uv - deltaX + deltaY);
                    float m4 = SampleCharacterSilhouette(uv - deltaX);
                    float m5 = SampleCharacterSilhouette(uv + deltaX);
                    float m6 = SampleCharacterSilhouette(uv - deltaY);
                    float m7 = SampleCharacterSilhouette(uv + deltaY);

                    float dilatedMask = max(centerMask, max(max(max(m0, m1), max(m2, m3)), max(max(m4, m5), max(m6, m7))));
                    float erodedMask = min(centerMask, min(min(min(m0, m1), min(m2, m3)), min(min(m4, m5), min(m6, m7))));
                    float outsideEdge = saturate(dilatedMask - centerMask);
                    float maskInnerEdge = saturate(centerMask - erodedMask) * 0.35;

                    float d0 = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv - deltaX - deltaY).r;
                    float d1 = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv + deltaX + deltaY).r;
                    float d2 = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv + deltaX - deltaY).r;
                    float d3 = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv - deltaX + deltaY).r;
                    float depthEdge = abs(LinearEyeDepth(d0, _ZBufferParams) - LinearEyeDepth(d1, _ZBufferParams)) +
                        abs(LinearEyeDepth(d2, _ZBufferParams) - LinearEyeDepth(d3, _ZBufferParams));

                    float3 n0 = SAMPLE_TEXTURE2D_X(_CameraNormalsTexture, sampler_CameraNormalsTexture, uv - deltaX - deltaY).rgb;
                    float3 n1 = SAMPLE_TEXTURE2D_X(_CameraNormalsTexture, sampler_CameraNormalsTexture, uv + deltaX + deltaY).rgb;
                    float3 n2 = SAMPLE_TEXTURE2D_X(_CameraNormalsTexture, sampler_CameraNormalsTexture, uv + deltaX - deltaY).rgb;
                    float3 n3 = SAMPLE_TEXTURE2D_X(_CameraNormalsTexture, sampler_CameraNormalsTexture, uv - deltaX + deltaY).rgb;
                    float normalEdge = step(_CharacterOutlineNormalThreshold, length(n0 - n1) + length(n2 - n3));

                    float innerRegion = centerMask * erodedMask;
                    float screenInnerEdge = max(step(_CharacterOutlineDepthThreshold, depthEdge), normalEdge) * innerRegion;

                    float edge = saturate(outsideEdge + max(maskInnerEdge, screenInnerEdge)) * _CharacterOutlineIntensity;
                    
                    finalColor = lerp(finalColor, _CharacterOutlineColor.rgb, edge);
                #endif

                return float4(finalColor, sceneColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DebugMaskBackground"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                float4 sceneColor = SampleBlit(input.texcoord);
                float luma = Luma(sceneColor.rgb);
                // Beautiful dark-themed dimmed grayscale background
                float3 grayscale = float3(luma, luma, luma) * 0.25;
                return float4(grayscale, sceneColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DebugMaskForeground"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                float4 sceneColor = SampleBlit(input.texcoord);
                float mask = SampleCharacterSilhouette(input.texcoord);
                // Vibrant glowing neon cyan to highlight stencil 2 areas
                float3 highlight = float3(0.0, 0.95, 0.85);
                float3 finalColor = lerp(sceneColor.rgb, highlight, mask * 0.35) + highlight * (mask * 0.2);
                return float4(finalColor, sceneColor.a);
            }
            ENDHLSL
        }
    }
}
