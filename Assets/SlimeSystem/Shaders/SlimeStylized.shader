Shader "Stylized/SlimeParticle"
{
    Properties
    {
        _MainColor ("Base Color", Color) = (0.2, 0.8, 0.4, 0.8)
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 3.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Particle
            {
                float3 position;
                float3 velocity;
                float3 predicted;
                float density;
                float lambda;
            };

            StructuredBuffer<Particle> _Particles;
            float _Size;
            float4 _MainColor;
            float4 _RimColor;
            float _RimPower;

            struct v2g {
                float3 pos : TEXCOORD0;
                float density : TEXCOORD1;
            };

            struct g2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float density : TEXCOORD1;
            };

            v2g vert(uint id : SV_VertexID)
            {
                v2g o;
                o.pos = _Particles[id].position;
                o.density = _Particles[id].density;
                return o;
            }

            [maxvertexcount(4)]
            void geom(point v2g IN[1], inout TriangleStream<g2f> triStream)
            {
                float3 worldPos = IN[0].pos;
                float3 viewUp = UNITY_MATRIX_V[1].xyz;
                float3 viewRight = UNITY_MATRIX_V[0].xyz;
                float halfSize = _Size * 0.5;

                float4 v[4];
                v[0] = float4(worldPos + viewRight * -halfSize + viewUp * -halfSize, 1.0);
                v[1] = float4(worldPos + viewRight * -halfSize + viewUp * halfSize, 1.0);
                v[2] = float4(worldPos + viewRight * halfSize + viewUp * -halfSize, 1.0);
                v[3] = float4(worldPos + viewRight * halfSize + viewUp * halfSize, 1.0);

                g2f o;
                o.density = IN[0].density;
                
                o.pos = TransformWorldToHClip(v[0].xyz); o.uv = float2(0, 0); triStream.Append(o);
                o.pos = TransformWorldToHClip(v[1].xyz); o.uv = float2(0, 1); triStream.Append(o);
                o.pos = TransformWorldToHClip(v[2].xyz); o.uv = float2(1, 0); triStream.Append(o);
                o.pos = TransformWorldToHClip(v[3].xyz); o.uv = float2(1, 1); triStream.Append(o);
            }

            float4 frag(g2f i) : SV_Target
            {
                float2 uv = i.uv * 2.0 - 1.0;
                float dist = length(uv);
                if (dist > 1.0) discard; 

                float fresnel = pow(dist, _RimPower);
                float4 col = lerp(_MainColor, _RimColor, fresnel);
                col.a *= (1.0 - dist) * _MainColor.a;
                
                return col;
            }
            ENDHLSL
        }
    }
}
