Shader "Custom/Seagulls_Opt_Shader"
{
    Properties
    {
        // Seagull texture
        _MainTex ("Seagull Texture", 2D) = "white" {}

        // Number of seagulls
        _BirdCount ("Number Seagulls", Int) = 64

        // Seagulls body default dimensions
        // Body
        [HideInInspector] _BodyWidth  ("Body Width", Float) = 0.22
        [HideInInspector] _BodyHeight ("Body Height", Float) = 1.10

        // Wings
        [HideInInspector] _WingLength         ("Wing Length", Float) = 0.95
        [HideInInspector] _WingRootHalfWidth  ("Wing Root Half Width", Float) = 0.20
        [HideInInspector] _WingTipHalfWidth   ("Wing Tip Half Width", Float) = 0.03
        [HideInInspector] _WingSweep          ("Wing Sweep", Float) = 0.08

        // Wings movement
        _FlapAmplitude ("Flap Amplitude (deg)", Range(0, 45)) = 18
        _FlapSpeed     ("Flap Speed", Float) = 3.5
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

            int _BirdCount;

            float _BodyWidth;
            float _BodyHeight;

            float _WingLength;
            float _WingRootHalfWidth;
            float _WingTipHalfWidth;
            float _WingSweep;

            float _FlapAmplitude;
            float _FlapSpeed;

            struct BirdData
            {
                float4 position_phase;   // xyz = position, w = flap phase
                float4 velocity_orbit;   // xyz = velocity, w = orbit offset
                float4 extra;            // x = height offset, yzw unused
            };

            StructuredBuffer<BirdData> _BirdBuffer;

            // Wings segments number --> joints for wings movement
            static const int WING_SEGMENTS = 4;

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

            // Rotacio around Z axis
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

            // Conversion object space -> clip space
            g2f VertexOutput(float3 pos, float2 uv)
            {
                g2f o;
                o.vertex = UnityObjectToClipPos(float4(pos, 1.0));
                o.uv = uv;
                return o;
            }

            // GEOMETRY SHADER
            [maxvertexcount(32)]
            void geom(point v2g IN[1], inout TriangleStream<g2f> stream)
            {
                uint id = IN[0].id;
                if (id >= _BirdCount) return;

                BirdData bird = _BirdBuffer[id];

                float3 birdPos = bird.position_phase.xyz;
                float  phase   = bird.position_phase.w;
                float3 velocity = bird.velocity_orbit.xyz;

                float3 objectUp = float3(0.0, 1.0, 0.0);

                // Forward direction
                float3 forwardDir = normalize(float3(velocity.x, 0.0, velocity.z));
                if (length(forwardDir) < 0.001)
                    forwardDir = float3(0.0, 0.0, 1.0);

                // Right direction
                float3 rightDir = normalize(cross(objectUp, forwardDir));

                // Transformation matrix
                // X = right direction
                // Y = forward direction
                // Z = vertical normal
                float3x3 transformMatrix = float3x3(
                    rightDir.x,  forwardDir.x,  -objectUp.x,
                    rightDir.y,  forwardDir.y,  -objectUp.y,
                    rightDir.z,  forwardDir.z,  -objectUp.z
                );

                // WINGS rotation
                float flapAngle = sin(_Time.y * _FlapSpeed + phase) * radians(_FlapAmplitude);

                // Body measures
                float bodyHalfWidth  = _BodyWidth * 0.5;
                float bodyHalfHeight = _BodyHeight * 0.5;
                float totalHalfWidth = bodyHalfWidth + _WingLength;

                // Modelling parts of one whole Seagull
                // 1) CENTRAL BODY
                {
                    float3 p00 = float3(-bodyHalfWidth, -bodyHalfHeight, 0.0);
                    float3 p10 = float3( bodyHalfWidth, -bodyHalfHeight, 0.0);
                    float3 p01 = float3(-bodyHalfWidth,  bodyHalfHeight, 0.0);
                    float3 p11 = float3( bodyHalfWidth,  bodyHalfHeight, 0.0);

                    float2 uv00 = float2(
                        (-bodyHalfWidth + totalHalfWidth) / (2.0 * totalHalfWidth),
                        (-bodyHalfHeight + bodyHalfHeight) / (2.0 * bodyHalfHeight)
                    );
                    float2 uv10 = float2(
                        ( bodyHalfWidth + totalHalfWidth) / (2.0 * totalHalfWidth),
                        (-bodyHalfHeight + bodyHalfHeight) / (2.0 * bodyHalfHeight)
                    );
                    float2 uv01 = float2(
                        (-bodyHalfWidth + totalHalfWidth) / (2.0 * totalHalfWidth),
                        ( bodyHalfHeight + bodyHalfHeight) / (2.0 * bodyHalfHeight)
                    );
                    float2 uv11 = float2(
                        ( bodyHalfWidth + totalHalfWidth) / (2.0 * totalHalfWidth),
                        ( bodyHalfHeight + bodyHalfHeight) / (2.0 * bodyHalfHeight)
                    );

                    stream.Append(VertexOutput(birdPos + mul(transformMatrix, p00), uv00));
                    stream.Append(VertexOutput(birdPos + mul(transformMatrix, p01), uv01));
                    stream.Append(VertexOutput(birdPos + mul(transformMatrix, p10), uv10));
                    stream.Append(VertexOutput(birdPos + mul(transformMatrix, p11), uv11));
                    stream.RestartStrip();
                }

                // 2) LEFT WING: strip made with t [0,1]
                for (int i = 0; i < WING_SEGMENTS; i++)
                {
                    float t = i / (float)WING_SEGMENTS;
                    float span = _WingLength * t;
                    float halfChord = lerp(_WingRootHalfWidth, _WingTipHalfWidth, t);

                    float yCenter = _WingSweep * t;
                    float yBottom = yCenter - halfChord;
                    float yTop    = yCenter + halfChord;

                    float xRest = -(bodyHalfWidth + span);

                    // Bending function --> f(t)
                    float bend = flapAngle * (0.20 * t + 0.80 * t * t);

                    float xDeformed = -(bodyHalfWidth + span * cos(bend));
                    float zDeformed = span * sin(bend);

                    float3 pBottom = float3(xDeformed, yBottom, zDeformed);
                    float3 pTop    = float3(xDeformed, yTop,    zDeformed);

                    float2 uvBottom = float2(
                        (xRest + totalHalfWidth) / (2.0 * totalHalfWidth),
                        (yBottom + bodyHalfHeight) / (2.0 * bodyHalfHeight)
                    );
                    float2 uvTop = float2(
                        (xRest + totalHalfWidth) / (2.0 * totalHalfWidth),
                        (yTop + bodyHalfHeight) / (2.0 * bodyHalfHeight)
                    );

                    stream.Append(VertexOutput(birdPos + mul(transformMatrix, pBottom), uvBottom));
                    stream.Append(VertexOutput(birdPos + mul(transformMatrix, pTop),    uvTop));
                }

                // LEFT WING TIP
                {
                    float xRestTip = -(bodyHalfWidth + _WingLength);
                    float yRestTip = _WingSweep;

                    float bendTip = flapAngle;
                    float xDefTip = -(bodyHalfWidth + _WingLength * cos(bendTip));
                    float zDefTip = _WingLength * sin(bendTip);

                    float3 pTip = float3(xDefTip, yRestTip, zDefTip);

                    float2 uvTip = float2(
                        (xRestTip + totalHalfWidth) / (2.0 * totalHalfWidth),
                        (yRestTip + bodyHalfHeight) / (2.0 * bodyHalfHeight)
                    );

                    stream.Append(VertexOutput(birdPos + mul(transformMatrix, pTip), uvTip));
                    stream.RestartStrip();
                }

                // 3) RIGHT WING --> opposite sign
                for (int j = 0; j < WING_SEGMENTS; j++)
                {
                    float t = j / (float)WING_SEGMENTS;
                    float span = _WingLength * t;
                    float halfChord = lerp(_WingRootHalfWidth, _WingTipHalfWidth, t);

                    float yCenter = _WingSweep * t;
                    float yBottom = yCenter - halfChord;
                    float yTop    = yCenter + halfChord;

                    float xRest = (bodyHalfWidth + span);

                    float bend = flapAngle * (0.20 * t + 0.80 * t * t);

                    float xDeformed = (bodyHalfWidth + span * cos(bend));
                    float zDeformed = span * sin(bend);

                    float3 pBottom = float3(xDeformed, yBottom, zDeformed);
                    float3 pTop    = float3(xDeformed, yTop,    zDeformed);

                    float2 uvBottom = float2(
                        (xRest + totalHalfWidth) / (2.0 * totalHalfWidth),
                        (yBottom + bodyHalfHeight) / (2.0 * bodyHalfHeight)
                    );
                    float2 uvTop = float2(
                        (xRest + totalHalfWidth) / (2.0 * totalHalfWidth),
                        (yTop + bodyHalfHeight) / (2.0 * bodyHalfHeight)
                    );

                    stream.Append(VertexOutput(birdPos + mul(transformMatrix, pBottom), uvBottom));
                    stream.Append(VertexOutput(birdPos + mul(transformMatrix, pTop),    uvTop));
                }

                // RIGHT TIP
                {
                    float xRestTip = (bodyHalfWidth + _WingLength);
                    float yRestTip = _WingSweep;

                    float bendTip = flapAngle;
                    float xDefTip = (bodyHalfWidth + _WingLength * cos(bendTip));
                    float zDefTip = _WingLength * sin(bendTip);

                    float3 pTip = float3(xDefTip, yRestTip, zDefTip);

                    float2 uvTip = float2(
                        (xRestTip + totalHalfWidth) / (2.0 * totalHalfWidth),
                        (yRestTip + bodyHalfHeight) / (2.0 * bodyHalfHeight)
                    );

                    stream.Append(VertexOutput(birdPos + mul(transformMatrix, pTip), uvTip));
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