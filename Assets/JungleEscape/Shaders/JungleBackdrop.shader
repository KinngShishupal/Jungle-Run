// Distant mountain ranges: unlit, ignores scene fog, colour from vertex colour.
// Vertex alpha = how much of the ridge colour shows (0 = pure haze at the base).
Shader "JungleEscape/Backdrop"
{
    Properties
    {
        _HazeColor ("Haze", Color) = (0.74, 0.78, 0.64, 1)
        _Haze ("Extra Haze", Range(0, 1)) = 0.15
        _Tint ("Ridge Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry-50" }
        Cull Off
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half4 _HazeColor, _Tint;
            half _Haze;

            struct appdata
            {
                float4 vertex : POSITION;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half amount = saturate(i.color.a * (1 - _Haze));
                return half4(lerp(_HazeColor.rgb, i.color.rgb * _Tint.rgb, amount), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
