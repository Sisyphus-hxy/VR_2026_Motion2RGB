Shader "Motion2RGB/PolyHaven PBR"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _RoughMap ("Roughness", 2D) = "white" {}
        _MetalMap ("Metalness", 2D) = "black" {}
        _AOMap ("Ambient occlusion", 2D) = "white" {}
        _AlphaMap ("Opacity", 2D) = "white" {}
        _EmissionMap ("Emission", 2D) = "black" {}
        [HDR] _EmissionColor ("Emission tint", Color) = (1,1,1,1)
        _UseAlpha ("Use cutout", Float) = 0
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300
        Cull [_Cull]
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        #pragma multi_compile_instancing
        sampler2D _MainTex, _BumpMap, _RoughMap, _MetalMap, _AOMap, _AlphaMap, _EmissionMap;
        fixed4 _Color;
        half4 _EmissionColor;
        half _UseAlpha, _Cutoff;
        struct Input { float2 uv_MainTex; };
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.uv_MainTex;
            if (_UseAlpha > 0.5) clip(tex2D(_AlphaMap, uv).r - _Cutoff);
            o.Albedo = tex2D(_MainTex, uv).rgb * _Color.rgb;
            o.Normal = UnpackNormal(tex2D(_BumpMap, uv));
            o.Metallic = tex2D(_MetalMap, uv).r;
            o.Smoothness = 1.0 - tex2D(_RoughMap, uv).r;
            o.Occlusion = tex2D(_AOMap, uv).r;
            o.Emission = tex2D(_EmissionMap, uv).rgb * _EmissionColor.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
