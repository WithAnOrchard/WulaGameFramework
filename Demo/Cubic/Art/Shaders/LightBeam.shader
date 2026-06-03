// =====================================================================
// LightBeam.shader  ——  built-in pipeline
// URP 体积光束 Shader（Unlit + Additive）—— 用于模拟 Spot Light 锥形光束。
// 配合一个沿 -Z 拉长的 Cylinder Mesh 使用，依靠菲涅尔（视角与表面法线的点积）
// 制造"侧视最亮、头视最暗"的体积感；并用 UV.y 做长度方向衰减、噪声扰动模拟尘埃。
//
// 适用：built-in pipeline（Unity 2022+ / Unity 6）。
// 渲染：Unlit → Additive → 不写深度。
// 注意事项：
//   - Mesh 应为朝向 -Z 拉长的 Cylinder/Frustum（见 CubicZAxisLightVFX.BuildBeamMesh）。
//   - 配合 Bloom 后处理可形成"光晕扩散"。
// =====================================================================
Shader "Cubic/LightBeam"
{
    Properties
    {
        _MainColor       ("Tint Color (HDR)", Color)      = (1, 0.85, 0.5, 1)
        _Intensity       ("Intensity",       Range(0, 8))  = 1.5
        _FresnelPower    ("Fresnel Power",   Range(0.5, 8)) = 2.0
        _LengthFade      ("Length Fade",     Range(0.1, 1))  = 0.85
        _NoiseStrength   ("Noise Strength",  Range(0, 1))    = 0.25
        _NoiseScale      ("Noise Scale",     Range(0.1, 20)) = 4
        _ScrollSpeed     ("Scroll Speed",    Range(-5, 5))   = 1.2
        _Alpha           ("Master Alpha",    Range(0, 1))    = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent+10"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "LightBeamAdditive"
            Tags { "LightMode" = "UniversalForward" }

            Blend  One One           // Additive
            ZWrite Off
            ZTest  LEqual
            Cull   Off               // 双面：摄像机可从任意角度看

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            float4 _MainColor;
            float  _Intensity;
            float  _FresnelPower;
            float  _LengthFade;
            float  _NoiseStrength;
            float  _NoiseScale;
            float  _ScrollSpeed;
            float  _Alpha;

            // 简易 2D 哈希噪声
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = v.uv;
                o.normalWS = UnityObjectToWorldNormal(v.normal);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDirWS = normalize(_WorldSpaceCameraPos - worldPos);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // Fresnel：相机视线与法线夹角 → 侧视最亮、头视最暗
                float3 N = normalize(i.normalWS);
                float3 V = normalize(i.viewDirWS);
                float  fres = pow(1.0 - saturate(abs(dot(N, V))), _FresnelPower);

                // 长度方向衰减 + 噪声
                float lenFade = lerp(1.0, _LengthFade, i.uv.y);
                float n = ValueNoise(i.uv * _NoiseScale + float2(0, _Time.y * _ScrollSpeed));
                float dust = 1.0 - _NoiseStrength + _NoiseStrength * n;

                half3 col = _MainColor.rgb * _Intensity * fres * lenFade * dust;
                half  a   = fres * lenFade * _Alpha;

                return half4(col * a, a);  // premultiplied for Blend One One
            }
            ENDCG
        }
    }

    Fallback Off
}
