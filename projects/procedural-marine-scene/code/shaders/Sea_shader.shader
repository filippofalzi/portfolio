Shader "Custom/Sea_shader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Sea Color", Color) = (0, 0.5, 0.8, 0.6)

        _Tess ("Tessellation (Max)", Range(1, 32)) = 8
        _AdaptiveTess ("Adaptive Tessellation", Range(0, 1)) = 1
        _TessMin ("Tessellation (Min)", Range(1, 32)) = 2
        _TessNear ("Tess Near", Float) = 10
        _TessFar ("Tess Far", Float) = 80

        _WaveHeight ("Wave Height", Float) = 0.25
        _WaveSpeed ("Wave Speed", Float) = 1.0

        _DispTex ("Displacement Map", 2D) = "gray" {}
        _DispScale ("Displacement Scale", Float) = 0.35
        _DispTiling ("Displacement Tiling", Float) = 0.25
        _DispSpeed ("Displacement Speed (UV)", Vector) = (0.05, 0.03, 0, 0)

        _DepthFade ("Depth Fade", Float) = 2.0

        _Wireframe ("Wireframe", Range(0, 1)) = 0
        _WireColor ("Wire Color", Color) = (1, 1, 1, 1)
        _WireThickness ("Wire Thickness", Range(0.25, 3.0)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }

        LOD 300
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma target 4.6
            #pragma vertex vert
            #pragma hull hull
            #pragma domain domain
            #pragma geometry geom
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;
            sampler2D _DispTex;

            float4 _Color;
            float _Tess;
            float _AdaptiveTess;
            float _TessMin;
            float _TessNear;
            float _TessFar;
            float _WaveHeight;
            float _WaveSpeed;
            float _DispScale;
            float _DispTiling;
            float4 _DispSpeed;
            float _DepthFade;

            float _Wireframe;
            float4 _WireColor;
            float _WireThickness;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2h
            {
                float4 pos : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct h2d
            {
                float edge[3] : SV_TessFactor;
                float inside : SV_InsideTessFactor;
            };

            struct d2g
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
            };

            // OUTPUT GEOMETRY -> FRAGMENT
            struct g2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float3 bary : TEXCOORD3;
            };

            float SampleDisplacement(float2 uv)
            {
                // Domain shader
                float2 duv = uv * _DispTiling + _Time.y * _DispSpeed.xy;
                float h = tex2Dlod(_DispTex, float4(duv, 0, 0)).r;
                return (h * 2.0 - 1.0) * _DispScale;
            }

            // Vertex
            v2h vert (appdata v)
            {
                v2h o;
                o.pos = v.vertex;
                o.uv = v.uv;
                return o;
            }

            // auxiliar function to calculate tessellation factor based on distance to camera (to avoid cracks)
            float CalcEdgeTess(float3 a, float3 b)
            {
                float tess = _Tess;
                if (_AdaptiveTess > 0.5)
                {
                    float3 mid = (a + b) * 0.5;
                    float dist = distance(_WorldSpaceCameraPos, mid);
                    float denom = max(0.0001, (_TessFar - _TessNear));
                    float t = saturate((dist - _TessNear) / denom);
                    float tMin = clamp(_TessMin, 1.0, 32.0);
                    tess = lerp(_Tess, tMin, t);
                    tess = clamp(tess, 1.0, 32.0);
                    tess = floor(tess + 0.5);
                }
                return tess;
            }
            // Tessellation amount
            h2d hsconst (InputPatch<v2h,3> patch)
            {
                h2d o;

                float3 w0 = mul(unity_ObjectToWorld, float4(patch[0].pos.xyz, 1)).xyz;
                float3 w1 = mul(unity_ObjectToWorld, float4(patch[1].pos.xyz, 1)).xyz;
                float3 w2 = mul(unity_ObjectToWorld, float4(patch[2].pos.xyz, 1)).xyz;

                o.edge[0] = CalcEdgeTess(w1, w2);
                o.edge[1] = CalcEdgeTess(w2, w0);
                o.edge[2] = CalcEdgeTess(w0, w1);
                o.inside = max(o.edge[0], max(o.edge[1], o.edge[2]));
                return o;
            }

            // Hull
            [domain("tri")]
            [partitioning("integer")]
            [outputtopology("triangle_cw")]
            [patchconstantfunc("hsconst")]
            [outputcontrolpoints(3)]
            v2h hull (InputPatch<v2h,3> patch, uint id : SV_OutputControlPointID)
            {
                return patch[id];
            }

            // Domain - Wave movement
            [domain("tri")]
            d2g domain (h2d tess, float3 bary : SV_DomainLocation, const OutputPatch<v2h,3> patch)
            {
                d2g o;

                float3 pos =
                    patch[0].pos.xyz * bary.x +
                    patch[1].pos.xyz * bary.y +
                    patch[2].pos.xyz * bary.z;

                float2 uv =
                    patch[0].uv * bary.x +
                    patch[1].uv * bary.y +
                    patch[2].uv * bary.z;


                float wave =
                    sin(pos.x * 2 + _Time.y * _WaveSpeed) *
                    cos(pos.z * 2 + _Time.y * _WaveSpeed);

                pos.y += wave * _WaveHeight;
                pos.y += SampleDisplacement(uv);

                float4 worldPos = mul(unity_ObjectToWorld, float4(pos,1));

                o.vertex = UnityWorldToClipPos(worldPos);
                o.worldPos = worldPos.xyz;
                o.screenPos = ComputeScreenPos(o.vertex);
                o.uv = uv;

                return o;
            }

            [maxvertexcount(3)]
            void geom(triangle d2g IN[3], inout TriangleStream<g2f> stream)
            {
                g2f o;

                o = (g2f)0;
                o.vertex = IN[0].vertex;
                o.uv = IN[0].uv;
                o.worldPos = IN[0].worldPos;
                o.screenPos = IN[0].screenPos;
                o.bary = float3(1, 0, 0);
                stream.Append(o);

                o = (g2f)0;
                o.vertex = IN[1].vertex;
                o.uv = IN[1].uv;
                o.worldPos = IN[1].worldPos;
                o.screenPos = IN[1].screenPos;
                o.bary = float3(0, 1, 0);
                stream.Append(o);

                o = (g2f)0;
                o.vertex = IN[2].vertex;
                o.uv = IN[2].uv;
                o.worldPos = IN[2].worldPos;
                o.screenPos = IN[2].screenPos;
                o.bary = float3(0, 0, 1);
                stream.Append(o);
            }

            // Fragment
            fixed4 frag (g2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;

                // Fresnel
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 normal = float3(0,1,0);
                float fresnel = pow(1 - saturate(dot(viewDir, normal)), 3);

                // Depth fade
                float sceneDepth = LinearEyeDepth(
                    SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos))
                );

                float waterDepth = LinearEyeDepth(i.screenPos.z / i.screenPos.w);

                float fade = saturate((sceneDepth - waterDepth) / _DepthFade);

                // Final alpha so that the sea is transparent
                float fresnelAlpha = lerp(0.15, 1.0, fresnel);
                col.a = _Color.a * fresnelAlpha * fade;

                // Wireframe
                if (_Wireframe > 0.5)
                {
                    float3 d = fwidth(i.bary);
                    float3 a3 = smoothstep(0.0, d * _WireThickness, i.bary);
                    float edge = 1.0 - min(min(a3.x, a3.y), a3.z);
                    col.rgb = lerp(col.rgb, _WireColor.rgb, edge);
                    col.a = max(col.a, edge * _WireColor.a);
                }

                return col;
            }

            ENDCG
        }
    }
}
