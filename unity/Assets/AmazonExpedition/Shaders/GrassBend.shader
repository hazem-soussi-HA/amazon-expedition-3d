Shader "Amazon Expedition/Grass Bend"
{
    Properties
    {
        _Color ("Color", Color) = (0.18, 0.35, 0.13, 1)
        _GrassBendArray ("Bend Points", Vector) = (0, 0, 0, 0)
        _BendRadius ("Bend Radius", Float) = 4.5
        _BendStrength ("Bend Strength", Float) = 0.55
        _WindFrequency ("Wind Frequency", Float) = 1.4
        _WindAmplitude ("Wind Amplitude", Float) = 0.14
        [Toggle] _GRASS_BEND_ON ("Bend Active", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "ForwardBase" }
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_instancing
            #pragma shader_feature _GRASS_BEND_ON
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 diff : COLOR0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            fixed4 _Color;
            float4 _GrassBendArray;
            float _BendRadius;
            float _BendStrength;
            float _WindFrequency;
            float _WindAmplitude;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;

                #ifdef _GRASS_BEND_ON
                float3 delta = world - _GrassBendArray.xyz;
                float dist = length(delta);
                float influence = saturate(1.0 - dist / max(_BendRadius, 0.001));
                influence *= _GrassBendArray.w;
                world += normalize(delta + float3(0.0001, 0, 0.0001)) * influence * _BendStrength * v.vertex.y;
                #endif

                float wind = sin(_Time.y * _WindFrequency + world.x * 0.7 + world.z * 0.4)
                           * _WindAmplitude * (0.4 + 0.6 * saturate(v.vertex.y));
                world.x += wind;

                float3 viewNormal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                o.diff = max(0.0, dot(viewNormal, _WorldSpaceLightPos0.xyz)) * _Color;
                o.diff.a = 1.0;
                o.pos = UnityWorldToClipPos(float4(world, 1.0));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_TRANSFER_INSTANCE_ID(i, input);
                return i.diff;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
