// Vertex-colored grass blades (no textures). Vertex alpha = 0 at the root, 1 at the tip and drives
// the wind sway and the push-away from the hero. Normals point up so blades light like the ground.
Shader "Shadowfall/Grass"
{
    Properties
    {
        _Wind ("Wind strength", Float) = 0.12
        _WindSpeed ("Wind speed", Float) = 1.6
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200
        Cull Off

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert
        #pragma target 3.0

        float _Wind, _WindSpeed;
        float4 _SfPlayerPos; // xyz = hero position, set by GrassField

        struct Input
        {
            float4 color : COLOR;
        };

        void vert (inout appdata_full v)
        {
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float k = v.color.a * v.color.a;
            float t = _Time.y * _WindSpeed;
            float gust = sin(t * 0.37 + wp.x * 0.05) * 0.5 + 0.5;
            float sway = sin(t + wp.x * 0.35 + wp.z * 0.2) * 0.6 + sin(t * 1.9 + wp.x * 1.3 - wp.z * 0.9) * 0.4;
            float3 offs = float3(sway, 0, sway * 0.6) * _Wind * (0.5 + gust) * k;

            // Bend away from the hero.
            float2 away = wp.xz - _SfPlayerPos.xz;
            float d = length(away);
            float push = saturate(1.0 - d / 1.1) * 0.35 * k;
            offs.xz += (away / max(d, 0.001)) * push;
            offs.y -= push * 0.5;

            wp += offs;
            v.vertex.xyz = mul(unity_WorldToObject, float4(wp, 1)).xyz;
            v.normal = mul((float3x3)unity_WorldToObject, float3(0, 1, 0));
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            o.Albedo = IN.color.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
