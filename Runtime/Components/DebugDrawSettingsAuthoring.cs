using Unity.Entities;
using UnityEngine;

namespace Kowloon.DebugDraw
{
    public class DebugDrawSettingsAuthoring : MonoBehaviour
    {
        [SerializeField]
        private Texture2D _FontTexture;

        private class DebugDrawSettingsBaker : Baker<DebugDrawSettingsAuthoring>
        {
            public override void Bake(DebugDrawSettingsAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new DebugDrawSettings
                {
                    FontTexture = authoring._FontTexture
                });
            }
        }
    }
}