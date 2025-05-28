#pragma once

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"
#include "Packages/kowloon.debugdraw/Runtime/Shaders/Common.hlsl"

struct LineShape
{
    float3 start;
    uint color;
    float3 end;
    float depthBias;
};

struct RayShape
{
    float3 origin;
    uint color;
    float3 direction;
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

struct ArrowShape
{
    float3 start;
    uint color;
    float4 rotation;
    float length;
    float width;
    float depthBias;
    float padding;
};

struct CapsuleShape
{
    float3 center;
    float radius;
    float4 rotation;
    uint color;
    float height;
    float depthBias;
    float padding;
};

struct ConeShape
{
    float3 center;
    float angle;
    float4 rotation;
    uint color;
    float height;
    float depthBias;
    float padding;
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

#ifdef SHAPE_LINE
    #define INSTANCE_TYPE LineShape
#elif SHAPE_RAY
    #define INSTANCE_TYPE RayShape
#elif SHAPE_NONUNIFORM_SCALE
    #define USE_VERTEX_INDEX_BUFFER
    #define INSTANCE_TYPE NonUniformScaleShape
#elif SHAPE_ARROW
    #define USE_VERTEX_INDEX_BUFFER
    #define INSTANCE_TYPE ArrowShape
#elif SHAPE_CAPSULE
    #define USE_VERTEX_INDEX_BUFFER
    #define INSTANCE_TYPE CapsuleShape
#elif SHAPE_CONE
    #define USE_VERTEX_INDEX_BUFFER
    #define INSTANCE_TYPE ConeShape
#endif

#ifdef USE_VERTEX_INDEX_BUFFER
int indexCount;
int vertexCount;
StructuredBuffer<float3> vertexBuffer;
StructuredBuffer<int> indexBuffer;
#endif

StructuredBuffer<INSTANCE_TYPE> instanceBuffer;

Varyings vert(uint vertexId : SV_VertexID)
{
#ifdef SHAPE_LINE
    uint shapeIndex = vertexId >> 1;
    LineShape shapeData = instanceBuffer[shapeIndex];
    float3 worldPos = vertexId & 1 ? shapeData.end : shapeData.start;
#elif SHAPE_RAY
    const float arrowWidthPercentage = 0.05;
    const float inverseArrowLengthPercentage = 1 - 0.2;

    RayShape shapeData = instanceBuffer[vertexId / 10];
    int shapeVertexIndex = vertexId % 10;

    float3 directionNormalized = normalize(shapeData.direction);
    float3 right = GetPerpendicular(directionNormalized);
    float3 up = cross(right, directionNormalized);

    float rayLength = length(shapeData.direction);
    right *= rayLength * arrowWidthPercentage;
    up *= rayLength * arrowWidthPercentage;

    float3 arrowBase = shapeData.direction * inverseArrowLengthPercentage;
    float isEndPoint = shapeVertexIndex == 1 || shapeVertexIndex == 3 || shapeVertexIndex == 5 || shapeVertexIndex == 7 || shapeVertexIndex == 9;

    float3 localPos = 0;
    localPos += shapeData.direction * isEndPoint;
    localPos += (arrowBase + right) * (shapeVertexIndex == 2);
    localPos += (arrowBase - right) * (shapeVertexIndex == 4);
    localPos += (arrowBase + up) * (shapeVertexIndex == 6);
    localPos += (arrowBase - up) * (shapeVertexIndex == 8);

    float3 worldPos = localPos + shapeData.origin;
#elif SHAPE_NONUNIFORM_SCALE
    NonUniformScaleShape shapeData = instanceBuffer[vertexId / indexCount];
    int shapeVertexIndex = indexBuffer[vertexId % indexCount];

    float3 localPos = vertexBuffer[shapeVertexIndex];
    float3 scaledPos = localPos * shapeData.size;
    float3 rotatedPos = RotateVector(scaledPos, shapeData.rotation);
    float3 worldPos = rotatedPos + shapeData.center;
#elif SHAPE_ARROW
    ArrowShape shapeData = instanceBuffer[vertexId / indexCount];
    int shapeVertexIndex = indexBuffer[vertexId % indexCount];

    float3 localPos = vertexBuffer[shapeVertexIndex];
    float3 scaledPos = localPos * shapeData.width;

    float offset = max(0, shapeData.length - shapeData.width);
    offset = shapeVertexIndex == 0 || shapeVertexIndex == 6 ? 0 : offset;
    scaledPos.z = min(shapeData.length, scaledPos.z + offset);

    float3 rotatedPos = RotateVector(scaledPos, shapeData.rotation);
    float3 worldPos = rotatedPos + shapeData.start;
#elif SHAPE_CAPSULE
    CapsuleShape shapeData = instanceBuffer[vertexId / indexCount];
    int shapeVertexIndex = indexBuffer[vertexId % indexCount];

    float3 localPos = vertexBuffer[shapeVertexIndex];
    float3 scaledPos = localPos * shapeData.radius;

    float adjustedHalfHeight = max(0, shapeData.height - shapeData.radius * 2) * 0.5f;
    scaledPos.y += shapeVertexIndex < vertexCount / 2 ? adjustedHalfHeight : -adjustedHalfHeight;

    float3 rotatedPos = RotateVector(scaledPos, shapeData.rotation);
    float3 worldPos = rotatedPos + shapeData.center;
#elif SHAPE_CONE
    ConeShape shapeData = instanceBuffer[vertexId / indexCount];
    int shapeVertexIndex = indexBuffer[vertexId % indexCount];

    float halfAngle = shapeData.angle * 0.5;
    float circleHeight = cos(halfAngle) * shapeData.height;
    float circleRadius = sin(halfAngle) * shapeData.height;

    float3 localPos = vertexBuffer[shapeVertexIndex];
    float3 scaledPos = localPos * circleRadius;

    const int offsetApexPoint = 19;
    scaledPos.z = shapeVertexIndex == vertexCount - offsetApexPoint ? scaledPos.y : circleHeight;

    if (shapeVertexIndex > vertexCount - offsetApexPoint)
    {
        scaledPos = normalize(scaledPos) * shapeData.height;
    }

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
