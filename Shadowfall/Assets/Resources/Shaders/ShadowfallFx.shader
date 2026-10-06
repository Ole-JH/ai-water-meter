// Soft glowing effects: particles (radial falloff from the quad centre) and effect meshes
// (rings, columns, arcs: falloff across the band, uv.y). Additive, unlit, no depth writes.
// _Mode: 0 = radial (particles), 1 = band (bright in the middle of uv.y), 2 = column (bright at uv.y = 0, fading up)
Shader "Shadowfall/Fx"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Mode ("Mode", Float) = 0
        _Intensity ("Intensity", Float) = 1.5
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Mode, _Intensity, _SrcBlend;

            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float a;
                if (_Mode < 0.5)
                {
                    float d = length(i.uv * 2.0 - 1.0);
                    a = saturate(1.0 - d);
                    a = a * a;
                }
                else if (_Mode < 1.5)
                {
                    float b = 1.0 - abs(i.uv.y * 2.0 - 1.0);
                    a = b * b;
                }
                else
                {
                    a = pow(saturate(1.0 - i.uv.y), 1.5) * saturate(i.uv.y * 8.0 + 0.2);
                }
                a *= i.color.a;
                fixed4 c;
                if (_SrcBlend > 4.5) // alpha blended (smoke): SrcAlpha
                {
                    c = fixed4(i.color.rgb, a);
                    UNITY_APPLY_FOG(i.fogCoord, c);
                }
                else // additive glow
                {
                    c = fixed4(i.color.rgb * a * _Intensity, 1);
                    UNITY_APPLY_FOG_COLOR(i.fogCoord, c, fixed4(0, 0, 0, 0));
                }
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
