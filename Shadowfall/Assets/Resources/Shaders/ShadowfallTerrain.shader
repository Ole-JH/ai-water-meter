// Ground splat shader: eight tiling layers blended by two control maps, using each layer's
// height (alpha) so grass pokes through dirt and stones sit on top of the mortar, instead of a smeary crossfade.
Shader "Shadowfall/Terrain"
{
    Properties
    {
        _Control0 ("Control 0 (grass, forest, dry, dead)", 2D) = "red" {}
        _Control1 ("Control 1 (dirt, cobble, gravel, sand)", 2D) = "black" {}
        _Tint ("Large-scale tint", 2D) = "white" {}
        _L0 ("Grass", 2D) = "gray" {}
        _L1 ("Forest floor", 2D) = "gray" {}
        _L2 ("Dry grass", 2D) = "gray" {}
        _L3 ("Dead grass", 2D) = "gray" {}
        _L4 ("Dirt", 2D) = "gray" {}
        _L5 ("Cobblestone", 2D) = "gray" {}
        _L6 ("Gravel", 2D) = "gray" {}
        _L7 ("Sand", 2D) = "gray" {}
        _Tiling ("World units per texture repeat", Float) = 5
        _BlendDepth ("Height blend softness", Float) = 0.18
        _WorldSize ("World size (xy)", Vector) = (160, 160, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry-100" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert fullforwardshadows
        #pragma target 3.5

        sampler2D _Control0, _Control1, _Tint;
        sampler2D _L0, _L1, _L2, _L3, _L4, _L5, _L6, _L7;
        float _Tiling, _BlendDepth;
        float4 _WorldSize;

        struct Input
        {
            float3 worldPos;
        };

        // Rotate the UVs per layer so the repeats of different layers don't line up.
        float2 Rot(float2 uv, float s, float c) { return float2(uv.x * c - uv.y * s, uv.x * s + uv.y * c); }

        void surf (Input IN, inout SurfaceOutput o)
        {
            float2 wp = IN.worldPos.xz;
            float2 cuv = wp / _WorldSize.xy;
            float4 w0 = tex2D(_Control0, cuv);
            float4 w1 = tex2D(_Control1, cuv);

            float2 uv = wp / _Tiling;
            fixed4 t0 = tex2D(_L0, uv);
            fixed4 t1 = tex2D(_L1, Rot(uv, 0.5, 0.866) + 0.31);
            fixed4 t2 = tex2D(_L2, Rot(uv, -0.707, 0.707) + 0.57);
            fixed4 t3 = tex2D(_L3, Rot(uv, 0.866, -0.5) + 0.13);
            fixed4 t4 = tex2D(_L4, Rot(uv * 0.8, 0.259, 0.966) + 0.71);
            fixed4 t5 = tex2D(_L5, uv * 1.15);
            fixed4 t6 = tex2D(_L6, Rot(uv, -0.5, 0.866) + 0.43);
            fixed4 t7 = tex2D(_L7, Rot(uv * 0.7, 0.966, 0.259) + 0.19);

            // Height blend: weight + texture height, keep only what's near the top.
            float4 h0 = w0 + float4(t0.a, t1.a, t2.a, t3.a) * 0.5;
            float4 h1 = w1 + float4(t4.a, t5.a, t6.a, t7.a) * 0.5;
            h0 *= step(0.001, w0);
            h1 *= step(0.001, w1);
            float top = max(max(max(h0.x, h0.y), max(h0.z, h0.w)), max(max(h1.x, h1.y), max(h1.z, h1.w)));
            float4 b0 = max(h0 - (top - _BlendDepth), 0);
            float4 b1 = max(h1 - (top - _BlendDepth), 0);
            float sum = dot(b0, 1) + dot(b1, 1) + 1e-5;

            fixed3 col = (t0.rgb * b0.x + t1.rgb * b0.y + t2.rgb * b0.z + t3.rgb * b0.w +
                          t4.rgb * b1.x + t5.rgb * b1.y + t6.rgb * b1.z + t7.rgb * b1.w) / sum;

            fixed3 tint = tex2D(_Tint, cuv).rgb * 2.0; // 0.5 = neutral
            o.Albedo = col * tint;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
