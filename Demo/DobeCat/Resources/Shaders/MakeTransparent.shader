// 桌宠透明窗口后处理 Shader：把与 _TransparentColorKey 接近的像素 alpha 改为 0。
// 配合 DwmExtendFrameIntoClientArea 让窗口真实透明（per-pixel alpha 由 DWM 合成）。
// 必须放在 Resources/ 下，否则 build 时不会被打包，运行时 Shader.Find 会失败。
Shader "Custom/MakeTransparent"
{
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _TransparentColorKey ("Transparent Color Key", Color) = (0, 1, 0, 1)
        _TransparencyMargin ("Transparency Margin", Float) = 0.01
    }
    SubShader
    {
        Pass
        {
            Tags { "RenderType"="Opaque" }
            LOD 200
            ZTest Always Cull Off ZWrite Off

            CGPROGRAM
            #pragma vertex VertexShaderFunction
            #pragma fragment PixelShaderFunction
            #include "UnityCG.cginc"

            struct VertexData
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct VertexToPixelData
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            VertexToPixelData VertexShaderFunction(VertexData input)
            {
                VertexToPixelData output;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = input.uv;
                return output;
            }

            sampler2D _MainTex;
            float3 _TransparentColorKey;
            float _TransparencyMargin;

            // Chroma-key spill suppression（绿屏算法）：
            //   假设 ColorKey 是单色，比如纯绿 (0,1,0)，sprite 的反走样边缘是 "(1-α)*green + α*sprite" 混色结果。
            //   bleed = key 主通道值 减去 其他通道均值 → 衡量"过量绿"。
            //   k = saturate(bleed * gain)：gain 由 _TransparencyMargin 调（margin 越大越激进）。
            //   削掉 key 主通道里 k 比例，alpha 同步降 1-k；纯 key 像素 → alpha=0；纯非 key → alpha=1；
            //   反走样边缘平滑过渡，不再留一圈绿边。
            float4 PixelShaderFunction(VertexToPixelData input) : SV_Target
            {
                float4 color = tex2D(_MainTex, input.uv);
                float3 key  = _TransparentColorKey.rgb;

                // 主通道值（key 上的投影）
                float keyAmount = dot(color.rgb, key);
                // 非主通道平均：用 (1-key) 作非主通道掩码
                float3 nonKeyMask = 1.0 - key;
                float nonKeySum = dot(color.rgb, nonKeyMask);
                float nonKeyCount = max(dot(nonKeyMask, float3(1, 1, 1)), 1.0); // 避免除零
                float nonKeyAvg = nonKeySum / nonKeyCount;

                // 过量主通道
                float bleed = saturate(keyAmount - nonKeyAvg);

                // gain 把 [0..1] 的 margin 映射到 [1..26]，给用户够大的调节空间
                float gain = 1.0 + _TransparencyMargin * 50.0;
                float k = saturate(bleed * gain);

                // 把 key 主通道往非主通道均值方向拉，去掉绿色溢色
                color.rgb = color.rgb - key * (keyAmount - nonKeyAvg) * k;
                color.a   = color.a * (1.0 - k);
                return color;
            }
            ENDCG
        }
    }
}
