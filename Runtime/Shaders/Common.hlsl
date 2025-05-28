#pragma once

float4 QuaternionMultiply(float4 q1, float4 q2)
{
    return float4(
        q2.xyz * q1.w + q1.xyz * q2.w + cross(q1.xyz, q2.xyz),
        q1.w * q2.w - dot(q1.xyz, q2.xyz)
    );
}

float3 GetPerpendicular(float3 vec)
{
    float3 up = float3(0, 1, 0);
    float3 right = float3(1, 0, 0);
    const float threshold = 0.999f;
    float dotProduct = abs(dot(normalize(vec), up));
    return cross(vec, dotProduct > threshold ? right : up);
}

// Vector rotation with a quaternion
// http://mathworld.wolfram.com/Quaternion.html
float3 RotateVector(float3 vec, float4 rotation)
{
    float4 reverseRotation = rotation * float4(-1, -1, -1, 1);
    return QuaternionMultiply(rotation, QuaternionMultiply(float4(vec, 0), reverseRotation)).xyz;
}