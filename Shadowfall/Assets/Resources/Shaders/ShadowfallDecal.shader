// Ground decals (blood splatter, pools, scorch marks): a textured, lit, alpha-blended layer drawn on top of
// the ground. Vertex color tints it and its alpha fades it; wet decals get a little specular shine.
Shader "Shadowfall/Decal"
{
    Properties
    {
        _MainTex ("Splats (atlas)", 2D) = "white" {}
        _SpecColor ("Specular", Color) = (0.55, 0.5, 0.5, 1)
        _Wet ("Wetness", Range(0, 1)) = 0.7
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+2" "RenderType" = "Transparent" "IgnoreProjector" = "True" "ForceNoShadowCasting" = "True" }
        Offset -1, -1
        ZWrite Off

        CGPROGRAM
        #pragma surface surf BlinnPhong decal:blend noforwardadd
        #pragma target 3.0

        sampler2D _MainTex;
        half _Wet;

        struct Input { float2 uv_MainTex; float4 color : COLOR; };

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 t = tex2D(_MainTex, IN.uv_MainTex);
            o.Albedo = IN.color.rgb * t.rgb;
            o.Alpha = t.a * IN.color.a;
            // uv2-free trick: fresh blood keeps full vertex alpha and stays shiny; dried blood is drawn darker and dull
            o.Specular = 0.25;
            o.Gloss = _Wet * t.a * saturate(IN.color.r * 2.0);
        }
        ENDCG
    }
    Fallback Off
}
