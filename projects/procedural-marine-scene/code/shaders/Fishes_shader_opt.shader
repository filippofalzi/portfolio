Shader "Custom/Fishes_compute_shader"
{
    Properties
    {
        // Fish texture
        _MainTex ("Fish Texture", 2D) = "white" {}

        // Number of fish
        _FishCount ("Number Fish", Int) = 64

        // Fish body default dimensions
        // Body
        [HideInInspector] _BodyWidth  ("Body Width", Float) = 0.9
        [HideInInspector] _BodyHeight ("Body Height", Float) = 0.75

        // Tail
        [HideInInspector] _TailLength         ("Tail Length", Float) = 0.35
        [HideInInspector] _TailRootHalfWidth  ("Tail Root Half Width", Float) = 0.08
        [HideInInspector] _TailTipHalfWidth   ("Tail Tip Half Width", Float) = 0.18

        // Tail movement
        _TailAmplitude ("Tail Amplitude", Float) = 0.18
        _TailSpeed     ("Tail Speed", Float) = 3.5
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma target 4.6
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            int _FishCount;

            float _BodyWidth;
            float _BodyHeight;

            float _TailLength;
            float _TailRootHalfWidth;
            float _TailTipHalfWidth;

            float _TailAmplitude;
            float _TailSpeed;

            struct FishData
            {
                float4 position_phase;   // xyz = position, w = tail phase
                float4 velocity_orbit;   // xyz = velocity, w = orbit offset
                float4 extra;            // x = height offset, yzw unused
            };

            StructuredBuffer<FishData> _FishBuffer;

            // Tail segments number --> joints for tail movement
            static const int TAIL_SEGMENTS = 4;

            // INPUTs  VERTEX SHADER
            struct v2g
            {
                uint id : TEXCOORD0;
            };

            // OUTPUT GEOMETRY -> FRAGMENT
            struct g2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
            };

            // Vertex shader: passes data to GEOMETRY
            v2g vert(uint vid : SV_VertexID)
            {
                v2g o;
                o.id = vid;
                return o;
            }

            // Rotation around Z axis
            float3x3 RotateZ(float angle)
            {
                float s = sin(angle);
                float c = cos(angle);

                return float3x3(
                    c, -s, 0,
                    s,  c, 0,
                    0,  0, 1
                );
            }

            // Rotation around X axis
            float3x3 RotateX(float angle)
            {
                float s = sin(angle);
                float c = cos(angle);

                return float3x3(
                    1, 0, 0,
                    0, c, -s,
                    0, s,  c
                );
            }

            // Conversion object space -> clip space
            g2f VertexOutput(float3 pos, float2 uv)
            {
                g2f o;
                o.vertex = UnityObjectToClipPos(float4(pos, 1.0));
                o.uv = uv;
                return o;
            }

            // Mapping UV of whole body
            float2 FishUV(float x, float y, float totalLength, float totalHalfHeight, float bodyHalfWidth)
            {
                return float2(
                    // Maps form local coordinates to [0,1] uv coordinates
                    (x + bodyHalfWidth) / totalLength,
                    (y + totalHalfHeight) / (2.0 * totalHalfHeight)
                );
            }

            // Vertex of central body
            g2f GenerateBodyVertex(
                float3 basePos,
                float2 localXY,
                float3x3 transformMatrix,
                float totalLength,
                float totalHalfHeight,
                float bodyHalfWidth
            )
            {
                float3 localPoint = float3(localXY.x, localXY.y, 0.0);

                // Use previous function to create uv
                float2 uv = FishUV(localXY.x, localXY.y, totalLength, totalHalfHeight, bodyHalfWidth);

                float3 finalPos = basePos + mul(transformMatrix, localPoint);
                return VertexOutput(finalPos, uv); // Drawing on uv coordinates
            }


            // GEOMETRY SHADER
            [maxvertexcount(32)]
            void geom(point v2g IN[1], inout TriangleStream<g2f> stream)
            {
            uint id = IN[0].id;
            if (id >= _FishCount) return;

            FishData fish = _FishBuffer[id];

            // Final fish position
            float3 fishPos = fish.position_phase.xyz;
            float phase    = fish.position_phase.w;
            float3 velocity = fish.velocity_orbit.xyz;

            // Vertical offset
            float3 objectUp = float3(0.0, 1.0, 0.0);

            // Fish plane normal --> points toward orbit center
            float3 flockCenter = float3(0.0, fish.extra.x, 0.0);
            float3 inwardDir = normalize(flockCenter - fishPos);

            // Fish side direction --> tangent to orbit
            float3 swimDir = normalize(velocity);
            swimDir.y = 0.0;

            if (length(swimDir) < 0.001)
                swimDir = float3(1.0, 0.0, 0.0);

            swimDir = normalize(swimDir);

            // Transformation matrix
            // X locale -> -swimDir   -> head forward, tail backward
            // Y locale -> objectUp   -> fish body develops vertically
            // Z locale -> inwardDir  -> plane normal points toward orbit center
            float3x3 transformMatrix = float3x3(
                -swimDir.x,  objectUp.x,  inwardDir.x,
                -swimDir.y,  objectUp.y,  inwardDir.y,
                -swimDir.z,  objectUp.z,  inwardDir.z
            );

            // TAIL rotation
            // Tail phase is advanced in the compute shader.
            float tailSwing = sin(phase) * _TailAmplitude;

            // Body measures
            float bodyHalfWidth  = _BodyWidth * 0.5;
            float bodyHalfHeight = _BodyHeight * 0.5;

            float totalLength     = _BodyWidth + _TailLength;
            float totalHalfHeight = max(bodyHalfHeight, _TailTipHalfWidth);

            // Modelling parts of one whole Fish
            // 1) CENTRAL BODY
            {
                float3 p00 = float3(-bodyHalfWidth, -bodyHalfHeight, 0.0);
                float3 p10 = float3( bodyHalfWidth, -bodyHalfHeight, 0.0);
                float3 p01 = float3(-bodyHalfWidth,  bodyHalfHeight, 0.0);
                float3 p11 = float3( bodyHalfWidth,  bodyHalfHeight, 0.0);

                float2 uv00 = float2(
                    (-bodyHalfWidth + bodyHalfWidth) / totalLength,
                    (-bodyHalfHeight + totalHalfHeight) / (2.0 * totalHalfHeight)
                );
                float2 uv10 = float2(
                    ( bodyHalfWidth + bodyHalfWidth) / totalLength,
                    (-bodyHalfHeight + totalHalfHeight) / (2.0 * totalHalfHeight)
                );
                float2 uv01 = float2(
                    (-bodyHalfWidth + bodyHalfWidth) / totalLength,
                    ( bodyHalfHeight + totalHalfHeight) / (2.0 * totalHalfHeight)
                );
                float2 uv11 = float2(
                    ( bodyHalfWidth + bodyHalfWidth) / totalLength,
                    ( bodyHalfHeight + totalHalfHeight) / (2.0 * totalHalfHeight)
                );

                stream.Append(VertexOutput(fishPos + mul(transformMatrix, p00), uv00));
                stream.Append(VertexOutput(fishPos + mul(transformMatrix, p01), uv01));
                stream.Append(VertexOutput(fishPos + mul(transformMatrix, p10), uv10));
                stream.Append(VertexOutput(fishPos + mul(transformMatrix, p11), uv11));
                stream.RestartStrip();
            }

            // 2) TAIL: strip made with t [0,1]
            for (int i = 0; i < TAIL_SEGMENTS; i++)
            {
                float t = i / (float)TAIL_SEGMENTS;
                float span = _TailLength * t;
                float halfChord = lerp(_TailRootHalfWidth, _TailTipHalfWidth, t);

                float xRest = bodyHalfWidth + span;

                // Bending function --> f(t)
                float bend = tailSwing * (0.20 * t + 0.80 * t * t);

                float yCenter = bend;
                float yBottom = yCenter - halfChord;
                float yTop    = yCenter + halfChord;

                float3 pBottom = float3(xRest, yBottom, 0.0);
                float3 pTop    = float3(xRest, yTop,    0.0);

                float2 uvBottom = float2(
                    (xRest + bodyHalfWidth) / totalLength,
                    (yBottom + totalHalfHeight) / (2.0 * totalHalfHeight)
                );
                float2 uvTop = float2(
                    (xRest + bodyHalfWidth) / totalLength,
                    (yTop + totalHalfHeight) / (2.0 * totalHalfHeight)
                );

                stream.Append(VertexOutput(fishPos + mul(transformMatrix, pBottom), uvBottom));
                stream.Append(VertexOutput(fishPos + mul(transformMatrix, pTop),    uvTop));
            }

            // TAIL TIP
            {
                float xRestTip = bodyHalfWidth + _TailLength;
                float yRestTip = tailSwing;

                float3 pTip = float3(xRestTip, yRestTip, 0.0);

                float2 uvTip = float2(
                    (xRestTip + bodyHalfWidth) / totalLength,
                    (yRestTip + totalHalfHeight) / (2.0 * totalHalfHeight)
                );

                stream.Append(VertexOutput(fishPos + mul(transformMatrix, pTip), uvTip));
                stream.RestartStrip();
            }
            }

            // FRAGMENT SHADER
            fixed4 frag(g2f i) : SV_Target
            {
                float2 uv = TRANSFORM_TEX(i.uv, _MainTex);
                fixed4 col = tex2D(_MainTex, uv);

                clip(col.a - 0.1);

                return col;
            }

            ENDCG
        }
    }
}
