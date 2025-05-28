using Unity.Entities;
using UnityEngine;

namespace Kowloon.DebugDraw
{
    public struct DebugDrawSettings : IComponentData
    {
        public UnityObjectRef<Texture2D> FontTexture;
    }
}