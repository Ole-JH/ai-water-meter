// Full-screen color grade for a darker, grittier look: desaturation, contrast, split toning
// (cool shadows, warm highlights) and darkened edges. Applied by ColorGrade.cs after the scene renders.
Shader "Hidden/Shadowfall/Grade"
{
    Properties
    {
        _MainTex ("Screen", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Saturation, _Contrast, _Exposure, _Vignette;
            float4 _ShadowTint, _HighlightTint;

            fixed4 frag (v2f_img i) : SV_Target
            {
                fixed4 src = tex2D(_MainTex, i.uv);
                float3 c = src.rgb * _Exposure;
                float luma = dot(c, float3(0.299, 0.587, 0.114));
                c = lerp(luma.xxx, c, _Saturation);
                c = (c - 0.5) * _Contrast + 0.5;
                float3 tone = lerp(_ShadowTint.rgb, _HighlightTint.rgb, smoothstep(0.1, 0.75, luma));
                c *= tone;
                float2 d = (i.uv - 0.5) * float2(1.15, 1.0);
                c *= 1.0 - _Vignette * smoothstep(0.25, 0.85, dot(d, d) * 2.2);
                return fixed4(saturate(c), src.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
