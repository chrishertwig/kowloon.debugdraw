Shader "Kowloon/DebugTextDraw"
{
    Properties
    {
        _MainTex ("MainTex", 2D) = "white" {}
    }
    SubShader
    {
        Pass
        {
            Name "DepthTest"
            Tags
            {
                "RenderType" = "Opaque" "RenderPipeline" = "UniversalRenderPipeline"
            }
            ZWrite On
            ZTest LEqual
            AlphaToMask On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile SHAPE_TEXT_128
            #include "Packages/kowloon.debugdraw/Runtime/Shaders/DebugTextDraw.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "Always"
            Tags
            {
                "RenderType" = "Opaque" "RenderPipeline" = "UniversalRenderPipeline"
            }
            ZWrite On
            ZTest Always
            AlphaToMask On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile SHAPE_TEXT_128
            #include "Packages/kowloon.debugdraw/Runtime/Shaders/DebugTextDraw.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "SeeThrough"
            Tags
            {
                "RenderType" = "Transparent" "RenderPipeline" = "UniversalRenderPipeline"
            }
            ZWrite Off
            ZTest Greater
            AlphaToMask On
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #define KOWLOON_HIDDEN
            #pragma multi_compile SHAPE_TEXT_128
            #include "Packages/kowloon.debugdraw/Runtime/Shaders/DebugTextDraw.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "TransparentPass"
            Tags
            {
                "RenderType" = "Transparent" "RenderPipeline" = "UniversalRenderPipeline"
            }
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile SHAPE_TEXT_128
            #include "Packages/kowloon.debugdraw/Runtime/Shaders/DebugTextDraw.hlsl"
            ENDHLSL
        }
    }
}
