Shader "CAVE/SpaceCubemapInverted"
{
    Properties
    {
        _Cube ("Space Cubemap", Cube) = "" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _Exposure ("Exposure (Helligkeit)", Range(0,4)) = 1
    }
    SubShader
    {
        // Wird als Hintergrund gezeichnet, vor aller anderen Geometrie
        Tags { "RenderType"="Background" "Queue"="Background" }

        Cull Front      // Das macht den Cube invertiert
        ZWrite Off      // verhält sich wie eine echte Skybox
        Fog { Mode Off }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            samplerCUBE _Cube;
            half4 _Tint;
            half _Exposure;

            struct appdata { float4 vertex : POSITION; };
            struct v2f
            {
                float4 pos     : SV_POSITION;
                float3 viewDir : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // Blickrichtung von der Kamera zur Cube-Oberfläche
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = worldPos - _WorldSpaceCameraPos;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = texCUBE(_Cube, i.viewDir);
                c.rgb *= _Tint.rgb * _Exposure;
                return c;
            }
            ENDCG
        }
    }
}