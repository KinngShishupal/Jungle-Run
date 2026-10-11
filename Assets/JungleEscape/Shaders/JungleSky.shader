// Soft gradient sky with a warm sun glow. Used as the skybox; fog colour is
// matched to _HorizonColor so distant scenery melts into the haze.
Shader "JungleEscape/Sky"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.30, 0.50, 0.66, 1)
        _HorizonColor ("Horizon", Color) = (0.86, 0.84, 0.68, 1)
        _GroundColor ("Ground", Color) = (0.25, 0.30, 0.20, 1)
        _SunColor ("Sun", Color) = (1, 0.85, 0.6, 1)
        _SunDirection ("Direction To Sun", Vector) = (0, 0.5, 1, 0)
        _SunSize ("Sun Size", Range(0.0001, 0.05)) = 0.003
        _SunGlow ("Sun Glow", Float) = 0.8
        _HorizonFalloff ("Horizon Falloff", Float) = 0.45
        _Exposure ("Exposure", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half4 _ZenithColor, _HorizonColor, _GroundColor, _SunColor;
            float4 _SunDirection;
            float _SunSize, _SunGlow, _HorizonFalloff, _Exposure;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;

                // Sky above the horizon, darker canopy-tinted ground below it.
                float up = pow(saturate(y), _HorizonFalloff);
                half3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, up);
                col = lerp(col, _GroundColor.rgb, saturate(-y * 3.0));

                // Sun disc + wide warm glow (the glow also tints the horizon haze).
                float s = saturate(dot(d, normalize(_SunDirection.xyz)));
                float glow = pow(s, 6.0) * 0.35 + pow(s, 48.0) * 0.8;
                float disc = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.5, s);
                col += _SunColor.rgb * (glow * _SunGlow + disc * 12.0);

                return half4(col * _Exposure, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
