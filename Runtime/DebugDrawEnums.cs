using System;

namespace Kowloon.DebugDraw
{
    // Update enum count below when adding new values!
    internal enum DrawType : byte
    {
        Line,
        Ray,
        Square,
        Circle,
        Arrow,
        Cube,
        Sphere,
        Capsule,
        Cone,
        PolyTriangle,
        PolyLine,
        PolyDisc,
        PolyCube,
        PolyPlane,
        Text128
    }

    // Update enum count below when adding new values!
    public enum RenderMode : byte
    {
        DepthTest,
        Always,
        SeeThrough,
        Transparent
    }

    [Flags]
    public enum Mask : uint
    {
        None = 0,
        KeepOneFrame = 1 << 0,
        BuildingGeneration = 1u << 1,
        Projectiles = 1u << 2,
        Unused3 = 1u << 3,
        Unused4 = 1u << 4,
        Unused5 = 1u << 5,
        Unused6 = 1u << 6,
        Unused7 = 1u << 7,
        Unused8 = 1u << 8,
        Unused9 = 1u << 9,
        Unused10 = 1u << 10,
        Unused11 = 1u << 11,
        Unused12 = 1u << 12,
        Unused13 = 1u << 13,
        Unused14 = 1u << 14,
        Unused15 = 1u << 15,
        Unused16 = 1u << 16,
        Unused17 = 1u << 17,
        Unused18 = 1u << 18,
        Unused19 = 1u << 19,
        Unused20 = 1u << 20,
        Unused21 = 1u << 21,
        Unused22 = 1u << 22,
        Unused23 = 1u << 23,
        Unused24 = 1u << 24,
        Unused25 = 1u << 25,
        Unused26 = 1u << 26,
        Unused27 = 1u << 27,
        Unused28 = 1u << 28,
        Unused29 = 1u << 29,
        Unused30 = 1u << 30,
        Unused31 = 1u << 31
    }

    internal static class EnumUtility
    {
        public const int DrawTypeCount = 15;
        public const int RenderModeCount = 4;
    }
}