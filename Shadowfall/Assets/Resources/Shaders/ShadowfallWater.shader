// Lake surface: translucent, two scrolling ripple normal maps, fresnel-tinted edges and a sun glint.
Shader "Shadowfall/Water"
{
    Properties
    {
        _Normal ("Ripple normal map (linear)", 2D) = "bump" {}
        _Deep ("Deep color", Color) = (0.06, 0.16, 0.24, 0.78)
        _Shallow ("Shallow / sky reflection", Color) = (0.32, 0.46, 0.55, 1)
        _Scale ("World units per ripple repeat", Float) = 6
        _Speed ("Ripple speed", Float) = 0.04
        _SpecColor ("Glint color", Color) = (1, 0.95, 0.85, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 200
        ZWrite Off

        CGPROGRAM
        #pragma surface surf BlinnPhong alpha:fade
        #pragma target 3.0

        sampler2D _Normal;
        fixed4 _Deep, _Shallow;
        float _Scale, _Speed;

        struct Input
        {
            float3 worldPos;
            float3 viewDir;
        };

        float3 Ripple(float2 uv)
        {
            return tex2D(_Normal, uv).xyz * 2.0 - 1.0;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            float2 uv = IN.worldPos.xz / _Scale;
            float t = _Time.y * _Speed;
            float3 n1 = Ripple(uv + float2(t, t * 0.6));
            float3 n2 = Ripple(uv * 1.7 + float2(-t * 0.8, t * 1.1));
            float3 n = normalize(float3(n1.xy + n2.xy, n1.z * n2.z));
            o.Normal = normalize(float3(n.x * 0.6, n.y * 0.6, n.z));

            float fres = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
            fres = fres * fres;
            o.Albedo = lerp(_Deep.rgb, _Shallow.rgb, fres * 0.8);
            o.Alpha = lerp(_Deep.a, 0.95, fres);
            o.Specular = 0.9;
            o.Gloss = 1.0;
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
