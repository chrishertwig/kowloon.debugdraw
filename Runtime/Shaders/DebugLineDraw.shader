Shader "Kowloon/DebugLineDraw"
{
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

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile SHAPE_LINE SHAPE_RAY SHAPE_NONUNIFORM_SCALE SHAPE_ARROW SHAPE_CAPSULE SHAPE_CONE
            #include "Packages/kowloon.debugdraw/Runtime/Shaders/DebugLineDraw.hlsl"
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

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile SHAPE_LINE SHAPE_RAY SHAPE_NONUNIFORM_SCALE SHAPE_ARROW SHAPE_CAPSULE SHAPE_CONE
            #include "Packages/kowloon.debugdraw/Runtime/Shaders/DebugLineDraw.hlsl"
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
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #define KOWLOON_HIDDEN
            #pragma multi_compile SHAPE_LINE SHAPE_RAY SHAPE_NONUNIFORM_SCALE SHAPE_ARROW SHAPE_CAPSULE SHAPE_CONE
            #include "Packages/kowloon.debugdraw/Runtime/Shaders/DebugLineDraw.hlsl"
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
            #pragma multi_compile SHAPE_LINE SHAPE_RAY SHAPE_NONUNIFORM_SCALE SHAPE_ARROW SHAPE_CAPSULE SHAPE_CONE
            #include "Packages/kowloon.debugdraw/Runtime/Shaders/DebugLineDraw.hlsl"
            ENDHLSL
        }
    }
}
