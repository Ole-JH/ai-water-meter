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
        // weather globals (Weather.cs): snow flattens and whitens the blades, autumn browns them
        sampler2D _SfSnowMask;
        float4 _SfSnow, _SfWorld;
        float _SfLeaves;

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

            // Under snow the blades sink in (and what still shows is frosted); autumn turns them straw-coloured.
            float region = saturate((wp.z - _SfSnow.z) / max(_SfSnow.w, 0.01) + 0.5);
            float cover = lerp(_SfSnow.y, _SfSnow.x, region);
            float cleared = tex2Dlod(_SfSnowMask, float4(wp.xz / max(_SfWorld.xy, 1), 0, 0)).r;
            float depth = cover * (1.0 - cleared * 0.8);
            // (scaled by the vertex alpha: blade tips; critters and decorations drawn with this shader use alpha 0)
            wp.y = lerp(wp.y, -0.05, saturate(depth * 1.4) * v.color.a);
            v.color.rgb = lerp(v.color.rgb, float3(0.62, 0.52, 0.24), _SfLeaves * 0.55 * v.color.a);
            v.color.rgb = lerp(v.color.rgb, float3(0.85, 0.88, 0.93), saturate(cover * 1.5) * 0.7 * v.color.a);
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
