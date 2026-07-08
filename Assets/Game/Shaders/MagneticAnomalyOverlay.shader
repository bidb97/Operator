Shader "Operator/MagneticAnomalyOverlay"
{
    Properties
    {
        _CoreColor ("Core Haze", Color) = (0.68, 0.44, 0.10, 0.2)
        _FlowColor ("Flow Highlight", Color) = (1.0, 0.74, 0.18, 0.5)
        _RimColor ("Edge Rim", Color) = (1.0, 0.62, 0.14, 1)
        _FlowSpeed ("Flow Speed", Float) = 0.85
        _StreakDensity ("Streak Density", Float) = 1.35
        _PulseSpeed ("Pulse Speed", Float) = 1.1
        _EdgeSoftness ("Edge Softness", Range(0.02, 0.25)) = 0.1
        _AnchorCell ("Anchor Cell", Vector) = (0, 0, 0, 0)
        _RadiusXY ("Radius XY", Vector) = (100, 150, 0, 0)
        _ShapeSeed ("Shape Seed", Float) = 0
        _FlowDir ("Flow Dir Cell", Vector) = (0, 1, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "MagneticAnomaly"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 worldPos : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _FlowColor;
                half4 _RimColor;
                half _FlowSpeed;
                half _StreakDensity;
                half _PulseSpeed;
                half _EdgeSoftness;
                float4 _AnchorCell;
                float4 _RadiusXY;
                float _ShapeSeed;
                float4 _FlowDir;
            CBUFFER_END

            float SmoothBlob(float2 worldPos)
            {
                float lx = worldPos.x - _AnchorCell.x;
                float ly = -worldPos.y - _AnchorCell.y;
                float nx = lx / max(_RadiusXY.x, 0.001);
                float ny = ly / max(_RadiusXY.y, 0.001);
                float dist = nx * nx + ny * ny;
                float seed = _ShapeSeed * 0.017;
                float wobble =
                    sin(nx * 4.0 + seed) * sin(ny * 4.0 - seed * 2.1) * 0.035
                    + sin((nx + ny) * 3.2 - seed * 1.3) * 0.02;
                float edge = 1.0 + _EdgeSoftness;
                return 1.0 - smoothstep(1.0 - _EdgeSoftness + wobble, edge + wobble, dist);
            }

            float SoftStreak(float x)
            {
                float s = sin(x * 3.14159265);
                return s * s * s * 0.5 + 0.5;
            }

            float FlowPattern(float2 cellPos, float2 flowDir, float2 perpDir)
            {
                float along = dot(cellPos, flowDir);
                float across = dot(cellPos, perpDir);
                float time = _Time.y * _FlowSpeed;

                float t1 = along * _StreakDensity - time;
                float t2 = along * (_StreakDensity * 0.55) - time * 0.7 + 1.7;
                float streak = max(SoftStreak(t1), SoftStreak(t2) * 0.65);

                float warp = sin(across * 0.65 + along * 0.25 + time * 1.3) * 0.18;
                float chevronT = frac((along + warp) * 0.45 - time * 0.55);
                float chevron = smoothstep(0.38, 0.48, chevronT) * (1.0 - smoothstep(0.48, 0.62, chevronT));
                float chevronShape = 1.0 - smoothstep(0.0, 0.42, abs(frac(across + warp * 2.0) - 0.5));
                chevron *= chevronShape;

                float shimmer = sin(along * 2.2 + across * 1.1 - time * 2.4) * 0.5 + 0.5;
                shimmer = shimmer * shimmer * 0.25;

                return saturate(streak * 0.75 + chevron * 0.55 + shimmer);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.worldPos = worldPos.xy;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float inside = SmoothBlob(input.worldPos);
                if (inside <= 0.001)
                {
                    discard;
                }

                float2 cellPos = float2(input.worldPos.x, -input.worldPos.y);
                float2 flowDir = normalize(_FlowDir.xy + 1e-5);
                float2 perpDir = float2(-flowDir.y, flowDir.x);

                float flow = FlowPattern(cellPos, flowDir, perpDir);
                float pulse = sin(_Time.y * _PulseSpeed + dot(cellPos, flowDir) * 0.15) * 0.5 + 0.5;
                float rim = smoothstep(0.55, 0.95, inside) * (1.0 - smoothstep(0.92, 1.0, inside));

                half3 rgb = _CoreColor.rgb * inside;
                rgb += _FlowColor.rgb * flow * _FlowColor.a;
                rgb += _RimColor.rgb * rim * _RimColor.a;
                rgb *= 0.85 + pulse * 0.15;

                half alpha = saturate(
                    _CoreColor.a * inside
                    + _FlowColor.a * flow * inside * 0.85
                    + _RimColor.a * rim);

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
