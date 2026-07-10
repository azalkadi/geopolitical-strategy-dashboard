Shader "Meridian/ScreenDot"
{
    // City markers as constant-screen-size soft-edged dots, regardless of camera zoom.
    // Mesh stores the city's world position at all 4 quad corners (see MapRenderer.BuildCityMarkers)
    // plus a UV in [0,1]^2 identifying the corner; the vertex shader reconstructs the actual
    // corner offset from _PixelRadius and the orthographic camera's world-units-per-pixel, so the
    // rendered dot is always _PixelRadius screen pixels regardless of _OrthoSize.
    Properties
    {
        _PixelRadius ("Pixel Radius", Float) = 3
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

            float _PixelRadius;
            float _OrthoSize; // set globally each frame by MapLayers from Camera.main.orthographicSize

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float worldPerPixel = (2.0 * max(_OrthoSize, 0.01)) / max(_ScreenParams.y, 1.0);
                float2 corner = v.uv * 2.0 - 1.0; // [0,1] -> [-1,1]
                float3 worldPos = v.vertex.xyz + float3(corner * _PixelRadius * worldPerPixel, 0);
                o.pos = UnityObjectToClipPos(float4(worldPos, 1));
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 c = i.uv * 2.0 - 1.0;
                float d = length(c);
                float alpha = smoothstep(1.0, 0.75, d); // soft round edge, no hard square corners
                return fixed4(i.color.rgb, i.color.a * alpha);
            }
            ENDCG
        }
    }
}
