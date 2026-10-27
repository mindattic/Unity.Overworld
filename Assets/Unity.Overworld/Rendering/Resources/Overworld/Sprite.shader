// The one shader every world quad, the team button and the weather and cloud batches draw with (U.OVW-T45).
// It is what the original's fixed-function pass did for a colour-keyed texture: the texel, times the vertex colour,
// times the material colour (the clock's light, RECONSTRUCTION 6.2), blended over what is behind it with source alpha,
// unlit, no culling and no depth writes (layering is by sorting order, as the original's draw order was).
// It has no LightMode tag, so URP draws it in its SRPDefaultUnlit pass and the built-in pipeline as a plain pass:
// the same shader in either pipeline (SpriteMaterials is the only code that names it).
Shader "MindAttic/Overworld/Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Picture", 2D) = "white" {}
        _Color ("Light", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
