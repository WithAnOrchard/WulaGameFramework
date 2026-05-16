// 体素纯色着色器：顶点色 + 简单方向光打光（不依赖 URP / 不依赖光照引擎，最小可用）。
// 目的：让 VoxelChunkMesher 写入的 vertex color 直接显示出来，并按法线给方向光照，
// 避免所有面同色看不出立体感。后续接贴图时把这个 shader 换成 atlas 版即可。
Shader "Wula/VoxelVertexColor"
{
    Properties
    {
        _LightDir ("Light Direction (World)", Vector) = (0.5, 1.0, 0.3, 0)
        _Ambient  ("Ambient", Range(0, 1))            = 0.45
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
            };

            struct v2f
            {
                float4 pos         : SV_POSITION;
                float4 color       : COLOR;
                float3 worldNormal : TEXCOORD0;
            };

            float4 _LightDir;
            float  _Ambient;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 N = normalize(i.worldNormal);
                float3 L = normalize(_LightDir.xyz);
                float  ndl = saturate(dot(N, L));
                float  light = _Ambient + (1.0 - _Ambient) * ndl;
                return float4(i.color.rgb * light, 1.0);
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
