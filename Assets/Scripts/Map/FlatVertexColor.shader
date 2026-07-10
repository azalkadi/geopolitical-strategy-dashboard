// Minimal unlit shader that draws mesh vertex colors flat — the map's country fills store
// their color per-vertex (see MapRenderer). Works in the Built-in Render Pipeline (the
// default for a fresh 2022.3 project). If you switch the project to URP, replace this with an
// equivalent URP unlit shader graph or the URP "Unlit" shader driven by vertex color.
Shader "Meridian/FlatVertexColor"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Cull Off
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
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
