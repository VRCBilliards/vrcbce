// Custom shader made by Juice...
Shader "CF-Pool/Fur"
{
    Properties
    {
		[Header(Color)]
        _Emission ("Emission", Range(0.0, 1.0)) = .5
		_Hue ("Hue", Range(0.0, 360.0)) = 0.0
		_Sat ("Saturation", Range(0.0, 1.0)) = 1.0
        _Invert ("Invert", Range(0.0, 1.0)) = 0

		[Header(Environment)]
        _Specular ("Specular", Range(0, 1)) = .25
		_DL("Directional Light Influence", Range(0,1)) = 1
		_LP("Light Probe Influence", Range(0,1)) = 1
		_Gloss("Smoothness", Range(0,1)) = 0.5

		[HDR][Header(Texture (UV1))]
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Texture", 2D) = "white" { }

		[Header(Fur (UV2))]
        _FurMap ("Fur Map", 2D) = "white" { }
        _Density ("Fur Density", Range(0.0, 1.0)) = .5
        _Length ("Fur Length", Range(0.0, 1)) = 0.5
        _Shading ("Fur Shading", Range(0.0, 1)) = 0.25
    }


    Category
    {

        Tags {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "LightMode" = "ForwardBase"
            }
        Cull Back
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha
        
        SubShader
        {
            Pass {
                CGPROGRAM
                #pragma vertex vert_surface
                #pragma fragment frag_surface
                #define FURSTEP 0.00
                #include "CottonFoxFur.cginc"
                ENDCG
            }
            Pass {
                CGPROGRAM
                #pragma vertex vert_base
                #pragma fragment frag_base
                #define FURSTEP 0.05
                #include "CottonFoxFur.cginc"
                ENDCG
            }
            Pass {
                CGPROGRAM
                #pragma vertex vert_base
                #pragma fragment frag_base
                #define FURSTEP 0.10
                #include "CottonFoxFur.cginc"
                ENDCG
            }
            Pass {
                CGPROGRAM
                #pragma vertex vert_base
                #pragma fragment frag_base
                #define FURSTEP 0.15
                #include "CottonFoxFur.cginc"
                ENDCG
            }
            Pass {
                CGPROGRAM
                #pragma vertex vert_base
                #pragma fragment frag_base
                #define FURSTEP 0.20
                #include "CottonFoxFur.cginc"
                ENDCG
            }
            Pass {
                CGPROGRAM
                #pragma vertex vert_base
                #pragma fragment frag_base
                #define FURSTEP 0.30
                #include "CottonFoxFur.cginc"
                ENDCG
            }
            Pass {
                CGPROGRAM
                #pragma vertex vert_base
                #pragma fragment frag_base
                #define FURSTEP 0.40
                #include "CottonFoxFur.cginc"
                ENDCG
            }
            Pass {
                CGPROGRAM
                #pragma vertex vert_base
                #pragma fragment frag_base
                #define FURSTEP 0.50
                #include "CottonFoxFur.cginc"
                ENDCG
            }
            Pass {
                CGPROGRAM
                #pragma vertex vert_base
                #pragma fragment frag_base
                #define FURSTEP 0.60
                #include "CottonFoxFur.cginc"
                ENDCG
            }
            Pass {
                CGPROGRAM
                #pragma vertex vert_base
                #pragma fragment frag_base
                #define FURSTEP 0.70
                #include "CottonFoxFur.cginc"
                ENDCG
            }
            Pass {
                CGPROGRAM
                #pragma vertex vert_base
                #pragma fragment frag_base
                #define FURSTEP 0.80
                #include "CottonFoxFur.cginc"
                ENDCG
            }
            Pass {
                CGPROGRAM
                #pragma vertex vert_base
                #pragma fragment frag_base
                #define FURSTEP 0.90
                #include "CottonFoxFur.cginc"
                ENDCG
            }
            Pass {
                CGPROGRAM
                #pragma vertex vert_base
                #pragma fragment frag_base
                #define FURSTEP 1.00
                #include "CottonFoxFur.cginc"
                ENDCG
            }
        }
    }
}