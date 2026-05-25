// Sprites/Flash — 基于 Sprites-Default 的受伤闪烁 shader。
// _FlashAmount = 0 时表现与默认 sprite 完全一致；
// _FlashAmount = 1 时所有不透明像素变为 _FlashColor（保留 alpha → 保留形状轮廓）。
Shader "Sprites/Flash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
        _FlashAmount ("Flash Amount", Range(0,1)) = 0
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment FlashFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            float  _FlashAmount;
            fixed4 _FlashColor;

            fixed4 FlashFrag(v2f IN) : SV_Target
            {
                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;
                // Flash: 将 rgb 向 _FlashColor 插值，alpha 不变 → 保留形状轮廓
                c.rgb = lerp(c.rgb, _FlashColor.rgb, _FlashAmount);
                // 预乘 alpha（与 Sprites/Default 一致）
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
