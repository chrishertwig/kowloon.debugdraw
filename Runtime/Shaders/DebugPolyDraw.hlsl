#pragma once

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"
#include "Packages/kowloon.debugdraw/Runtime/Shaders/Common.hlsl"

struct PolyTriangleShape
{
    float3 vertex0;
    uint color;
    float3 vertex1;
    float depthBias;
    float3 vertex2;
    float padding2;
};

struct PolyLineShape
{
    float3 start;
    uint color;
    float3 end;
    float width;
    float3 normal;
    float depthBias;
};

struct NonUniformScaleShape
{
    float3 center;
    uint color;
    float3 size;
    float depthBias;
    float4 rotation;
};

struct Varyings
{
    float4 position : SV_POSITION;
    half4 color : TEXCOORD0;
};

half4 frag(Varyings input) : SV_Target
{
    return input.color;
}

#ifdef SHAPE_POLYGON_TRIANGLE
    #define INSTANCE_TYPE PolyTriangleShape
#elif SHAPE_POLYGON_LINE
    #define INSTANCE_TYPE PolyLineShape
#elif SHAPE_POLYGON_PLANE
    #define INSTANCE_TYPE NonUniformScaleShape
#elif SHAPE_NONUNIFORM_SCALE
    #define USE_VERTEX_INDEX_BUFFER
    #define INSTANCE_TYPE NonUniformScaleShape
#endif

#ifdef USE_VERTEX_INDEX_BUFFER
int vertexCount;
StructuredBuffer<float3> vertexBuffer;
StructuredBuffer<int> indexBuffer;
#endif

int indexCount;
StructuredBuffer<INSTANCE_TYPE> instanceBuffer;

#ifdef SHAPE_POLYGON_LINE
static const float quadRightSign[6] = { 1, 1, -1, -1, 1, -1 };
static const float quadForwardOffset[6] = { 0, 1, 0, 0, 1, 1 };
#elif SHAPE_POLYGON_PLANE
static const float3 quadVertices[4] = {
    float3(-0.5, 0.0, -0.5),
    float3(-0.5, 0.0, 0.5),
    float3(0.5, 0.0, -0.5),
    float3(0.5, 0.0, 0.5)
};
static const int quadIndices[6] = {
    0, 1, 2,
    2, 1, 3
};
#endif

Varyings vert(uint vertexId : SV_VertexID)
{
    INSTANCE_TYPE shapeData = instanceBuffer[vertexId / indexCount];
#ifdef SHAPE_POLYGON_TRIANGLE
    int shapeVertexIndex = vertexId % indexCount;
    float3 worldPos = shapeVertexIndex == 0 ? shapeData.vertex0 : shapeVertexIndex == 1 ? shapeData.vertex1 : shapeData.vertex2;
#elif SHAPE_POLYGON_LINE
    int shapeVertexIndex = vertexId % indexCount;

    float3 forward = normalize(shapeData.end - shapeData.start);
    float3 right = normalize(cross(forward, shapeData.normal));
    float halfWidth = shapeData.width * 0.5;

    float3 worldPos = lerp(shapeData.start, shapeData.end, quadForwardOffset[shapeVertexIndex]) + right * quadRightSign[shapeVertexIndex] * halfWidth;
#elif SHAPE_POLYGON_PLANE
    int shapeVertexIndex = vertexId % indexCount;

    float3 localPos = quadVertices[quadIndices[shapeVertexIndex]];
    float3 scaledPos = localPos * shapeData.size;
    float3 rotatedPos = RotateVector(scaledPos, shapeData.rotation);
    float3 worldPos = rotatedPos + shapeData.center;

#elif SHAPE_NONUNIFORM_SCALE
    int shapeVertexIndex = vertexId % indexCount;
    int vertexIndex = indexBuffer[shapeVertexIndex];

    float3 localPos = vertexBuffer[vertexIndex];
    float3 scaledPos = localPos * shapeData.size;
    float3 rotatedPos = RotateVector(scaledPos, shapeData.rotation);
    float3 worldPos = rotatedPos + shapeData.center;
#endif


    half4 color = UnpackFromR8G8B8A8(shapeData.color).abgr;
    float4 clipPos = TransformObjectToHClip(worldPos);
    clipPos.z += shapeData.depthBias;

    Varyings output;
    output.position = clipPos;
#ifdef KOWLOON_HIDDEN
    output.color.rgb = color.rgb;
    output.color.a = color.a * 0.05;
#else
    output.color = color;
#endif
    return output;
}
