Shader "Meridian/ScreenLine"
{
    // Roads/railways/water-crossings as constant-SCREEN-width lines, regardless of camera zoom.
    // The old approach baked a fixed WORLD-space half-width straight into vertex positions —
    // fine at the zoom level it was tuned for, but since 1 Mercator-degree-unit is ~111km at the
    // equator, that width scaled up to multi-kilometer-wide blobs the moment you zoomed in past
    // that point (constant world size = growing screen size as you zoom in). Same fix as
    // ScreenDot.shader for point markers: the mesh stores the RAW line point at every vertex
    // (see MapRenderer.AppendQuad/AppendJoint) plus a per-vertex offset direction in UV0; this
    // shader reconstructs the actual corner offset from _PixelHalfWidth and the orthographic
    // camera's world-units-per-pixel each frame, so the line is always _PixelHalfWidth*2 screen
    // pixels wide no matter how far in you zoom.
    Properties
    {
        _PixelHalfWidth ("Pixel Half Width", Float) = 1.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _PixelHalfWidth;
            float _OrthoSize; // set globally each frame by MapLayers from Camera.main.orthographicSize

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0; // offset direction (NOT normalized to unit length for joints — see AppendJoint)
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float worldPerPixel = (2.0 * max(_OrthoSize, 0.01)) / max(_ScreenParams.y, 1.0);
                float3 worldPos = v.vertex.xyz + float3(v.uv * _PixelHalfWidth * worldPerPixel, 0);
                o.pos = UnityObjectToClipPos(float4(worldPos, 1));
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
}
