// 体素贴图着色器：_MainTex(atlas) × 顶点色 tint × 简单方向光
// vertex color 现在表示 per-block tint（草顶绿色，其它白色）；UV 为 atlas slot rect。
// 与 VoxelVertexColor 行为兼容（vertex color 通道仍参与），可直接替换。
Shader "Wula/VoxelTextured"
{
    Properties
    {
        _MainTex  ("Atlas",                    2D)        = "white" {}
        _LightDir ("Light Direction (World)",  Vector)    = (0.5, 1.0, 0.3, 0)
        _Ambient  ("Ambient",                  Range(0,1)) = 0.45
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Tags { "LightMode" = "Always" }

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos         : SV_POSITION;
                float4 color       : COLOR;
                float3 worldNormal : TEXCOORD0;
                float2 uv          : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4    _MainTex_ST;
            float4    _LightDir;
            float     _Ambient;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos         = UnityObjectToClipPos(v.vertex);
                o.color       = v.color;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.uv          = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 N    = normalize(i.worldNormal);
                float3 L    = normalize(_LightDir.xyz);
                float  ndl  = saturate(dot(N, L));
                float  light = _Ambient + (1.0 - _Ambient) * ndl;

                fixed4 tex   = tex2D(_MainTex, i.uv);
                fixed3 rgb   = tex.rgb * i.color.rgb * light;
                return fixed4(rgb, 1.0);
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
