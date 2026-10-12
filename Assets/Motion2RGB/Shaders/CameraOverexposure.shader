Shader "Hidden/Motion2RGB/CameraOverexposure"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _TexelSize;
            float _Exposure, _BloomIntensity, _Threshold;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = saturate(i.uv);
                float3 center = tex2D(_MainTex, uv).rgb * _Exposure;
                float luma = dot(center, float3(0.2126, 0.7152, 0.0722));
                float3 bloom = 0;
                float total = 0;
                const float2 taps[8] = {
                    float2(-1, -1), float2(0, -1), float2(1, -1), float2(-1, 0),
                    float2(1, 0), float2(-1, 1), float2(0, 1), float2(1, 1)
                };
                for (int n = 0; n < 8; n++)
                {
                    float3 sampleColor = tex2D(_MainTex, uv + taps[n] * _TexelSize.xy * 3.0).rgb * _Exposure;
                    float sampleLuma = dot(sampleColor, float3(0.2126, 0.7152, 0.0722));
                    float weight = saturate((sampleLuma - _Threshold) * 1.7);
                    bloom += sampleColor * weight;
                    total += weight;
                }
                bloom /= max(total, 1.0);
                float highlight = saturate((luma - _Threshold) * 1.35);
                float3 result = center + bloom * (_BloomIntensity * (0.35 + highlight));
                result = 1.0 - exp(-result);
                return fixed4(saturate(result), 1);
            }
            ENDCG
        }
    }
}
