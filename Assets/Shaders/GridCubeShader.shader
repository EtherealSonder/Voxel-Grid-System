Shader "Custom/GridCubeShader"
{
    Properties
    {
        [MainColor]_BaseColor("Base Color", Color) = (0.85, 0.85, 0.85, 1)
        _Steps("Toon Steps (2..8)", Range(2, 8)) = 4
        _Ambient("Ambient", Range(0, 1)) = 0.25

        [Header(Edge Lines)]
        _LineColor("Line Color", Color) = (0.05, 0.05, 0.05, 1)
        _LineThickness("Line Thickness (world units)", Range(0.005, 0.15)) = 0.04
        _GridScale("Grid Scale (cells per unit)", Range(0.1, 10)) = 1.0
        _GridOffset("Grid Offset (usually 0.5,0.5,0.5)", Vector) = (0.5, 0.5, 0.5, 0)
        [Toggle]_UseWorldSpace("Use World Space Grid", Float) = 1

        [Header(Hold Feedback Glow)]
        _GlowColor("Glow Color", Color) = (0, 0, 0, 0)
        _GlowStrength("Glow Strength", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half  _Steps;
                half  _Ambient;

                half4 _LineColor;
                half  _LineThickness;
                float _GridScale;
                float4 _GridOffset;
                float _UseWorldSpace;

                half4 _GlowColor;
                half  _GlowStrength;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(v.normalOS);

                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS   = nrm.normalWS;
                o.positionOS = v.positionOS.xyz;
                return o;
            }

            float GridEdgeMask(float3 posWS, float3 posOS, float3 normalWS)
{
    float3 p = (_UseWorldSpace > 0.5) ? posWS : posOS;

    p += _GridOffset.xyz;

    float scale = max(_GridScale, 1e-5);
    p *= scale;

    float3 f = frac(p);
    float3 d = min(f, 1.0 - f);

    float3 ap = abs(posOS);
    float distToEdge2D;

    if (ap.x >= ap.y && ap.x >= ap.z)
    {
        distToEdge2D = min(d.y, d.z);
    }
    else if (ap.y >= ap.x && ap.y >= ap.z)
    {
        distToEdge2D = min(d.x, d.z);
    }
    else
    {
        distToEdge2D = min(d.x, d.y);
    }

    float aa = fwidth(distToEdge2D) * 1.5;

    float thicknessCell = _LineThickness * scale;

    float m = 1.0 - smoothstep(thicknessCell, thicknessCell + aa, distToEdge2D);
    return saturate(m);
}


            half4 frag(Varyings i) : SV_Target
            {
                float3 N = normalize(i.normalWS);

                Light mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);

                float ndl = saturate(dot(N, L));

                float steps = max(_Steps, 2.0);
                float q = floor(ndl * steps) / (steps - 1.0);
                q = saturate(q);

                float lit = lerp(_Ambient, 1.0, q);
                half3 baseLit = _BaseColor.rgb * lit;

                float lineMask = GridEdgeMask(i.positionWS, i.positionOS, i.normalWS);
                half3 finalRgb = lerp(baseLit, _LineColor.rgb, lineMask);

                finalRgb += _GlowColor.rgb * saturate(_GlowStrength);

                return half4(finalRgb, _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
