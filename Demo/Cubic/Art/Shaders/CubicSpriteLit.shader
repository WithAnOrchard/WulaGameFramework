// =====================================================================
// CubicSpriteLit.shader  ——  built-in pipeline
// Cubic 专用 Sprite 光照 + 边缘 Halo（不依赖 URP / Light2D）
// ---------------------------------------------------------------------
// 效果：
//   1) 基色 _BaseColor
//   2) Z 轴光：上亮下暗（uv.y 主导）
//   3) 边缘 halo：UV 距中心越远越亮，做出"软遮罩/朦胧"
// 写法：CGPROGRAM + UnityCG.cginc（标准 built-in 2D sprite 套路）。
// =====================================================================
Shader "Cubic/SpriteLit"
{
    Properties
    {
        _MainTex              ("Sprite Texture", 2D)              = "white" {}
        _BaseColor            ("Base Color",      Color)          = (1, 1, 1, 1)
        _RimColor             ("Rim / Halo Color", Color)         = (1.0, 0.85, 0.55, 1)
        _RimPower             ("Rim Power",       Range(0.5, 8))  = 3.0
        _RimStrength          ("Rim Strength",    Range(0, 2))    = 0.4
        _ZTopBrightness       ("Top Brightness",  Range(0, 2))    = 1.15
        _ZBottomBrightness    ("Bottom Brightness", Range(0, 1))  = 0.9
        _ZLightIntensity      ("Z Light Intensity", Range(0, 1))  = 0.3
        _Alpha                ("Master Alpha",    Range(0, 1))    = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType"     = "Plane"
            "RenderPipeline"  = "UniversalPipeline"
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            Blend  SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest  LEqual
            Cull   Off

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _BaseColor;
            float4 _RimColor;
            float  _RimPower;
            float  _RimStrength;
            float  _ZTopBrightness;
            float  _ZBottomBrightness;
            float  _ZLightIntensity;
            float  _Alpha;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = TRANSFORM_TEX(v.uv, _MainTex);
                o.color  = v.color;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // 1) 主纹理
                half4 tex = tex2D(_MainTex, i.uv);

                // 2) 综合 sprite 颜色：贴图 × 材质 _BaseColor × SpriteRenderer.color
                //    —— 必须乘 i.color.rgb，否则 SpriteRenderer.color 完全失效，
                //       所有 sprite 看起来都是 _BaseColor（材质默认白）的样子
                half3 spriteRGB = tex.rgb * _BaseColor.rgb * i.color.rgb;
                half  spriteA   = tex.a   * _BaseColor.a   * i.color.a;

                // 3) Z 轴渐变：uv.y → 轻微提亮 sprite 自身（不引入新颜色）
                float zGrad = lerp(_ZBottomBrightness, _ZTopBrightness, i.uv.y);
                // (zGrad - 1.0) 让中心点为 0、顶部 +、底部 −，纯亮度调整、不染颜色
                half3 zLit = spriteRGB * (zGrad - 1.0) * _ZLightIntensity;

                // 4) UV-距中心 rim：边缘提亮（multiplicative，不引入新颜色）
                float2 uvOff = abs(i.uv - 0.5) * 2.0;            // 0=中心, 1=边缘
                float  rim   = pow(max(uvOff.x, uvOff.y), _RimPower);
                half3  rimLit = spriteRGB * rim * _RimStrength;

                // 5) 合成：基础色 + Z轴提亮/压暗 + 边缘提亮
                half3 col = spriteRGB + zLit + rimLit;
                half  a   = spriteA * _Alpha;

                return half4(col, a);
            }
            ENDCG
        }
    }

    Fallback Off
}
