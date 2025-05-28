#pragma once

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"
#include "Packages/kowloon.debugdraw/Runtime/Shaders/Common.hlsl"

struct Text128
{
    float3 position;
    uint color;
    float4 rotation;
    float size;
    float depthBias;
    uint fixedString128Bytes[32];
};

struct Varyings
{
    float4 position : SV_POSITION;
    half4 color : TEXCOORD0;
    float2 uv : TEXCOORD1;
};

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

float median(float a, float b, float c)
{
    return max(min(a, b), min(max(a, b), c));
}

half4 frag(Varyings input) : SV_Target
{
    float3 distanceField = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
    float signedDistance = median(distanceField.r, distanceField.g, distanceField.b) - 0.5;
    float alpha = clamp(signedDistance / fwidth(signedDistance) + 0.5, 0, 1);

    // clip(signedDistance - 0.5);

    return half4(input.color.rgb, alpha);
}

int indexCount;
StructuredBuffer<Text128> instanceBuffer;

static const float3 faceVertices[4] = {
    float3(0.0, 0.0, 0.0),
    float3(0.0, 1.0, 0.0),
    float3(1.0, 0.0, 0.0),
    float3(1.0, 1.0, 0.0)
};
static const int faceIndices[6] = {
    0, 1, 2,
    2, 1, 3
};

void setAtlasPosition(inout float2 uv, int code, int2 atlasDimensions)
{
    int row = code / atlasDimensions.x;
    int column = code % atlasDimensions.x;
    row = atlasDimensions.y - 1 - row;

    uv += float2(column, row);
    uv /= atlasDimensions;
}

int GetCharCode(Text128 instance, int byteIndex)
{
    int uintIndex = byteIndex / 4;
    int byteOffset = byteIndex % 4;

    uint word = instance.fixedString128Bytes[uintIndex];
    // Shift right by (byteOffset * 8) bits, then mask out the lower 8 bits
    int extractedByte = (int)((word >> (byteOffset * 8)) & 0xFF);
    return extractedByte;
}

Varyings vert(uint vertexId : SV_VertexID)
{
    uint instanceId = vertexId / indexCount;
    Text128 instance = instanceBuffer[instanceId];

    uint localVertexIndex = vertexId % indexCount;
    uint charIndex = localVertexIndex / 6;

    uint stringLength = instance.fixedString128Bytes[0] & 0xFFFF;
    float3 localPos = faceVertices[faceIndices[localVertexIndex % 6]];
    float2 uv = localPos.xy;
    localPos *= float3(0.5, 1, 1);
    localPos += float3(0.5, 0, 0) * charIndex;
    localPos *= instance.size;
    float3 worldPos = RotateVector(localPos, instance.rotation) + instance.position;

    const int bitOffset = 2; // First two bits are string length
    const int asciiStartCharIndex = 33; // Start at "!" char
    int charAsciiCode = GetCharCode(instance, charIndex + bitOffset) - asciiStartCharIndex;

    const int2 atlasDimensions = int2(8, 12);
    setAtlasPosition(uv, charAsciiCode, atlasDimensions);

    // Each tile in the atlas is 32px x 42px. We need to account for the empty pixels at the bottom of the atlas.
    const float yScale = 504.0 / 512.0;
    uv.y += 1 - yScale;
    uv.y *= yScale;


    // Move vertices off-screen if exceeding string length or if invalid ascii code (most cases it is "space")
    bool isValid = charIndex >= stringLength || charAsciiCode < 0;
    float4 clipPos = isValid ? float4(-1, -1, -1, -1) : TransformObjectToHClip(worldPos);
    clipPos.z += instance.depthBias;

    half4 color = UnpackFromR8G8B8A8(instance.color).abgr;

    Varyings output;
    output.position = clipPos;
#ifdef KOWLOON_HIDDEN
    output.color.rgb = color.rgb;
    output.color.a = color.a * 0.05;
#else
    output.color = color;
#endif
    output.uv = uv;
    return output;
}

uint4 UnpackChar(uint packed)
{
    uint4 result;
    result[0] = (packed >>  0) & 0xFF;
    result[1] = (packed >>  8) & 0xFF;
    result[2] = (packed >> 16) & 0xFF;
    result[3] = (packed >> 24) & 0xFF;
    return result;
}