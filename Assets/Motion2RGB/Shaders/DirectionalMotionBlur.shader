Shader "Hidden/Motion2RGB/DirectionalMotionBlur"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _MainTex_TexelSize;
            float4x4 _CurrentInverseVP;
            float4x4 _PreviousVP;
            float _ExposureFraction, _TrailStrength, _MaxTrailPixels;
            float3 _CurrentCameraPosition;
            float _MotionDepthLimit;
            float2 _BlurDirection;
            float _ResolutionScale;
            float _NoiseAmount;
            float _ChromaticAmount;
            float _DepthSensitivity;
            float _NoiseSeed;
            int _Mode;
            fixed4 _Tint;

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 DepthUV(float2 uv)
            {
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0) uv.y = 1.0 - uv.y;
                #endif
                return uv;
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv0 = saturate(i.uv);
                fixed4 center = tex2D(_MainTex, uv0);
                if (_Mode == 1)
                {
                    float grain = (Hash21(uv0 * _ScreenParams.xy + _NoiseSeed) - 0.5) * _NoiseAmount;
                    return saturate(center + grain);
                }
                if (_Mode == 2)
                {
                    float2 blockUv = floor(uv0 * _ScreenParams.xy * _ResolutionScale) / max(1.0, _ScreenParams.xy * _ResolutionScale);
                    return tex2D(_MainTex, saturate(blockUv));
                }
                if (_Mode == 3)
                {
                    float2 chromaStep = _BlurDirection * _MainTex_TexelSize.xy * (1.0 + _ChromaticAmount * 16.0);
                    fixed4 shiftedR = tex2D(_MainTex, saturate(uv0 + chromaStep));
                    fixed4 shiftedB = tex2D(_MainTex, saturate(uv0 - chromaStep));
                    return fixed4(shiftedR.r, center.g, shiftedB.b, center.a);
                }

                // Reproject each visible surface through the actual previous camera pose.
                float2 depthUV = DepthUV(uv0);
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, depthUV);
                float clipDepth = rawDepth;
                #if !defined(UNITY_REVERSED_Z)
                clipDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif
                float4 world = mul(_CurrentInverseVP, float4(depthUV * 2.0 - 1.0, clipDepth, 1.0));
                if (abs(world.w) < 0.000001) return center;
                world /= world.w;
                float centerDepth = LinearEyeDepth(rawDepth);
                // Artistic distance cap applies only to motion estimation. Keep the
                // actual depth for edge filtering and leave rotation direction intact.
                float distanceScale = min(1.0, _MotionDepthLimit / max(centerDepth, 0.001));
                world.xyz = _CurrentCameraPosition + (world.xyz - _CurrentCameraPosition) * distanceScale;
                float4 previousClip = mul(_PreviousVP, world);
                if (previousClip.w <= 0.0001) return center;
                float2 previousUV = DepthUV(previousClip.xy / previousClip.w * 0.5 + 0.5);
                // Sample toward current image motion to place a trail toward its old position.
                float2 trail = (uv0 - previousUV) * _ExposureFraction;
                float pixelLength = length(trail * abs(_MainTex_TexelSize.zw));
                trail *= min(1.0, _MaxTrailPixels / max(0.0001, pixelLength));
                if (pixelLength < 0.1) return center;
                float3 sum = center.rgb;
                float totalWeight = 1.0;
                [unroll]
                for (int tap = 1; tap <= 20; tap++)
                {
                    float t = tap / 20.0;
                    float2 sampleUV = uv0 + trail * t;
                    if (any(sampleUV < 0.0) || any(sampleUV > 1.0)) continue;
                    float sampleDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, DepthUV(sampleUV)));
                    float relativeDepth = abs(sampleDepth - centerDepth) / max(centerDepth, 0.25);
                    float weight = (1.0 - t * 0.6) / (1.0 + relativeDepth * _DepthSensitivity);
                    sum += tex2D(_MainTex, sampleUV).rgb * weight;
                    totalWeight += weight;
                }
                // Preserve brightness when depth discontinuities or image borders reject taps.
                float3 blurred = sum / max(totalWeight, 0.0001);
                return fixed4(lerp(center.rgb, blurred * _Tint.rgb, _TrailStrength), center.a);
            }
            ENDCG
        }
    }
}
