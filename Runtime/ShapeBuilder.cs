using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Kowloon.DebugDraw
{
    [BurstCompile]
    public static class ShapeBuilder
    {
        private const int CIRCLE_SECTION_COUNT = 24;

        [BurstCompile]
        public static void GenerateSquare(out NativeArray<float3> vertices, out NativeArray<int> indices, Allocator allocator)
        {
            vertices = new NativeArray<float3>(4, allocator);
            indices = new NativeArray<int>(8, allocator);

            vertices[0] = new float3(-0.5f, -0.5f, 0);
            vertices[1] = new float3(0.5f, -0.5f, 0);
            vertices[2] = new float3(0.5f, 0.5f, 0);
            vertices[3] = new float3(-0.5f, 0.5f, 0);

            indices[0] = 0; indices[1] = 1;
            indices[2] = 1; indices[3] = 2;
            indices[4] = 2; indices[5] = 3;
            indices[6] = 3; indices[7] = 0;
        }

        [BurstCompile]
        public static void GenerateCube(out NativeArray<float3> vertices, out NativeArray<int> indices, Allocator allocator)
        {
            vertices = new NativeArray<float3>(8, allocator);
            indices = new NativeArray<int>(24, allocator);

            vertices[0] = new float3(-0.5f, -0.5f, -0.5f);
            vertices[1] = new float3(0.5f, -0.5f, -0.5f);
            vertices[2] = new float3(0.5f, 0.5f, -0.5f);
            vertices[3] = new float3(-0.5f, 0.5f, -0.5f);
            vertices[4] = new float3(-0.5f, -0.5f, 0.5f);
            vertices[5] = new float3(0.5f, -0.5f, 0.5f);
            vertices[6] = new float3(0.5f, 0.5f, 0.5f);
            vertices[7] = new float3(-0.5f, 0.5f, 0.5f);

            indices[0] = 0; indices[1] = 1;
            indices[2] = 1; indices[3] = 2;
            indices[4] = 2; indices[5] = 3;
            indices[6] = 3; indices[7] = 0;
            indices[8] = 4; indices[9] = 5;
            indices[10] = 5; indices[11] = 6;
            indices[12] = 6; indices[13] = 7;
            indices[14] = 7; indices[15] = 4;
            indices[16] = 0; indices[17] = 4;
            indices[18] = 1; indices[19] = 5;
            indices[20] = 2; indices[21] = 6;
            indices[22] = 3; indices[23] = 7;
        }

        [BurstCompile]
        public static void GenerateCircle(out NativeArray<float3> vertices, out NativeArray<int> indices, Allocator allocator)
        {
            vertices = new NativeArray<float3>(CIRCLE_SECTION_COUNT, allocator);
            indices = new NativeArray<int>(CIRCLE_SECTION_COUNT * 2, allocator);
            for (int i = 0; i < CIRCLE_SECTION_COUNT; i++)
            {
                float angle = math.PI2 * i / CIRCLE_SECTION_COUNT;
                vertices[i] = new float3(math.cos(angle), math.sin(angle), 0);
                indices[i * 2] = i;
                indices[i * 2 + 1] = (i + 1) % CIRCLE_SECTION_COUNT;
            }
        }

        [BurstCompile]
        public static void GenerateSphere(out NativeArray<float3> vertices, out NativeArray<int> indices, Allocator allocator)
        {
            vertices = new NativeArray<float3>(CIRCLE_SECTION_COUNT * 3, allocator);
            indices = new NativeArray<int>(CIRCLE_SECTION_COUNT * 2 * 3, allocator);

            for (int i = 0; i < CIRCLE_SECTION_COUNT; i++)
            {
                float angle = math.PI2 * i / CIRCLE_SECTION_COUNT;

                int vertexIndexXY = i;
                int vertexIndexXZ = i + CIRCLE_SECTION_COUNT;
                int vertexIndexYZ = i + CIRCLE_SECTION_COUNT * 2;

                vertices[vertexIndexXY] = new float3(math.cos(angle), math.sin(angle), 0);
                vertices[vertexIndexXZ] = new float3(math.cos(angle), 0, math.sin(angle));
                vertices[vertexIndexYZ] = new float3(0, math.cos(angle), math.sin(angle));

                int indexIndexXY = i * 2;
                int indexIndexXZ = i * 2 + CIRCLE_SECTION_COUNT * 2;
                int indexIndexYZ = i * 2 + CIRCLE_SECTION_COUNT * 4;

                int next = (i + 1) % CIRCLE_SECTION_COUNT;

                indices[indexIndexXY] = i;
                indices[indexIndexXY + 1] = next;
                indices[indexIndexXZ] = i + CIRCLE_SECTION_COUNT;
                indices[indexIndexXZ + 1] = next + CIRCLE_SECTION_COUNT;
                indices[indexIndexYZ] = i + CIRCLE_SECTION_COUNT * 2;
                indices[indexIndexYZ + 1] = next + CIRCLE_SECTION_COUNT * 2;
            }
        }

        [BurstCompile]
        public static void GenerateCapsule(out NativeArray<float3> vertices, out NativeArray<int> indices, Allocator allocator)
        {
            vertices = new NativeArray<float3>(CIRCLE_SECTION_COUNT * 4 + 4 + 8, allocator);
            indices = new NativeArray<int>((CIRCLE_SECTION_COUNT * 4 + 4) * 2, allocator);

            int currentVertexOffset = 0;
            int currentIndexOffset = 0;

            // Top full circle
            int loopStartIndex = currentIndexOffset;
            int length = CIRCLE_SECTION_COUNT;
            for (int i = 0; i < length; i++)
            {
                indices[currentIndexOffset++] = currentVertexOffset;
                indices[currentIndexOffset++] = currentVertexOffset + 1;

                float angle = math.PI2 * i / CIRCLE_SECTION_COUNT;
                vertices[currentVertexOffset++] = new float3(math.cos(angle), 0f, math.sin(angle));
            }

            // Connect last and first vertex
            indices[currentIndexOffset - 1] = loopStartIndex;

            // Top hemisphere in XY plane
            length = length / 2 + 1;
            for (int i = 0; i < length; i++)
            {
                if (i < length - 1)
                {
                    indices[currentIndexOffset++] = currentVertexOffset;
                    indices[currentIndexOffset++] = currentVertexOffset + 1;
                }

                float angle = math.PI2 * i / CIRCLE_SECTION_COUNT;
                vertices[currentVertexOffset++] = new float3(math.cos(angle), math.sin(angle), 0f);
            }

            // Top hemisphere in YZ plane
            length = CIRCLE_SECTION_COUNT / 2 + 1;
            for (int i = 0; i < length; i++)
            {
                if (i < length - 1)
                {
                    indices[currentIndexOffset++] = currentVertexOffset;
                    indices[currentIndexOffset++] = currentVertexOffset + 1;
                }

                float angle = math.PI2 * i / CIRCLE_SECTION_COUNT;
                vertices[currentVertexOffset++] = new float3(0f, math.sin(angle), math.cos(angle));
            }

            // Connecting vertical lines
            int lineIndexRightTop = currentVertexOffset;
            vertices[currentVertexOffset++] = new float3(1f, 0f, 0f);
            int lineIndexLeftTop = currentVertexOffset;
            vertices[currentVertexOffset++] = new float3(-1f, 0f, 0f);
            int lineIndexForwardTop = currentVertexOffset;
            vertices[currentVertexOffset++] = new float3(0f, 0f, 1f);
            int lineIndexBackTop = currentVertexOffset;
            vertices[currentVertexOffset++] = new float3(0f, 0f, -1f);

            // Bottom full circle
            loopStartIndex = currentIndexOffset;
            length = CIRCLE_SECTION_COUNT;
            for (int i = 0; i < length; i++)
            {
                indices[currentIndexOffset++] = currentVertexOffset;
                indices[currentIndexOffset++] = currentVertexOffset + 1;

                float angle = math.PI2 * i / CIRCLE_SECTION_COUNT;
                vertices[currentVertexOffset++] = new float3(math.cos(angle), 0f, math.sin(angle));
            }

            // Connect last and first vertex
            indices[currentIndexOffset - 1] = loopStartIndex;

            // Bottom hemisphere in XY plane
            length = CIRCLE_SECTION_COUNT / 2 + 1;
            for (int i = 0; i < length; i++)
            {
                if (i < length - 1)
                {
                    indices[currentIndexOffset++] = currentVertexOffset;
                    indices[currentIndexOffset++] = currentVertexOffset + 1;
                }

                float angle = math.PI + math.PI2 * i / CIRCLE_SECTION_COUNT;
                vertices[currentVertexOffset++] = new float3(math.cos(angle), math.sin(angle), 0f);
            }

            // Bottom hemisphere in YZ plane
            length = CIRCLE_SECTION_COUNT / 2 + 1;
            for (int i = 0; i < length; i++)
            {
                if (i < length - 1)
                {
                    indices[currentIndexOffset++] = currentVertexOffset;
                    indices[currentIndexOffset++] = currentVertexOffset + 1;
                }

                float angle = math.PI + math.PI2 * i / CIRCLE_SECTION_COUNT;
                vertices[currentVertexOffset++] = new float3(0f, math.sin(angle), math.cos(angle));
            }

            // Connecting vertical lines
            int lineIndexRightBottom = currentVertexOffset;
            vertices[currentVertexOffset++] = new float3(1f, 0f, 0f);
            int lineIndexLeftBottom = currentVertexOffset;
            vertices[currentVertexOffset++] = new float3(-1f, 0f, 0f);
            int lineIndexForwardBottom = currentVertexOffset;
            vertices[currentVertexOffset++] = new float3(0f, 0f, 1f);
            int lineIndexBackBottom = currentVertexOffset;
            vertices[currentVertexOffset] = new float3(0f, 0f, -1f);

            // Connecting lines
            indices[currentIndexOffset++] = lineIndexRightTop;
            indices[currentIndexOffset++] = lineIndexRightBottom;
            indices[currentIndexOffset++] = lineIndexLeftTop;
            indices[currentIndexOffset++] = lineIndexLeftBottom;
            indices[currentIndexOffset++] = lineIndexForwardTop;
            indices[currentIndexOffset++] = lineIndexForwardBottom;
            indices[currentIndexOffset++] = lineIndexBackTop;
            indices[currentIndexOffset] = lineIndexBackBottom;
        }

        [BurstCompile]
        public static void GenerateCone(out NativeArray<float3> vertices, out NativeArray<int> indices, Allocator allocator)
        {
            vertices = new NativeArray<float3>(CIRCLE_SECTION_COUNT + 1 + 9 * 2, allocator);
            indices = new NativeArray<int>((CIRCLE_SECTION_COUNT + 4 + 8 * 2) * 2, allocator);

            int currentVertexOffset = 0;
            int currentIndexOffset = 0;

            // Full circle
            for (int i = 0; i < CIRCLE_SECTION_COUNT; i++)
            {
                indices[currentIndexOffset++] = i;
                indices[currentIndexOffset++] = (i + 1) % CIRCLE_SECTION_COUNT;

                float angle = math.PI2 * i / CIRCLE_SECTION_COUNT;
                vertices[currentVertexOffset++] = new float3(math.cos(angle), math.sin(angle), 0f);
            }

            // Connecting side lines
            indices[currentIndexOffset++] = 0;
            indices[currentIndexOffset++] = currentVertexOffset;
            indices[currentIndexOffset++] = CIRCLE_SECTION_COUNT / 4;
            indices[currentIndexOffset++] = currentVertexOffset;
            indices[currentIndexOffset++] = CIRCLE_SECTION_COUNT / 2;
            indices[currentIndexOffset++] = currentVertexOffset;
            indices[currentIndexOffset++] = CIRCLE_SECTION_COUNT / 4 * 3;
            indices[currentIndexOffset++] = currentVertexOffset;
            vertices[currentVertexOffset++] = float3.zero;

            // Lines across the base.
            for (int i = 0; i < 9; i++)
            {
                float x = (i / 8f - 0.5f) * 2f;
                if (i != 8)
                {
                    indices[currentIndexOffset++] = currentVertexOffset;
                    indices[currentIndexOffset++] = currentVertexOffset + 1;
                }
                vertices[currentVertexOffset++] = new float3(x, 0f, 0.1f);
            }

            for (int i = 0; i < 9; i++)
            {
                float y = (i / 8f - 0.5f) * 2f;
                if (i != 8)
                {
                    indices[currentIndexOffset++] = currentVertexOffset;
                    indices[currentIndexOffset++] = currentVertexOffset + 1;
                }
                vertices[currentVertexOffset++] = new float3(0f, y, 0.1f);
            }
        }

        [BurstCompile]
        public static void GenerateArrow(out NativeArray<float3> vertices, out NativeArray<int> indices, Allocator allocator)
        {
            vertices = new NativeArray<float3>(7, allocator);
            indices = new NativeArray<int>(12, allocator);

            vertices[0] = new float3(-0.25f, 0f, 0f);
            vertices[1] = new float3(-0.25f, 0f, 0f);
            vertices[2] = new float3(-0.5f, 0f, 0f);
            vertices[3] = new float3(0f, 0f, 1f);
            vertices[4] = new float3(0.5f, 0f, 0f);
            vertices[5] = new float3(0.25f, 0f, 0f);
            vertices[6] = new float3(0.25f, 0f, 0f);

            indices[0] = 0; indices[1] = 1;
            indices[2] = 1; indices[3] = 2;
            indices[4] = 2; indices[5] = 3;
            indices[6] = 3; indices[7] = 4;
            indices[8] = 4; indices[9] = 5;
            indices[10] = 5; indices[11] = 6;
        }

        [BurstCompile]
        public static void GeneratePolyCube(out NativeArray<float3> vertices, out NativeArray<int> indices, Allocator allocator)
        {
            vertices = new NativeArray<float3>(8, allocator);
            indices = new NativeArray<int>(36, allocator);

            vertices[0] = new float3(-0.5f, -0.5f, 0.5f);
            vertices[1] = new float3(0.5f, -0.5f, 0.5f);
            vertices[2] = new float3(-0.5f, 0.5f, 0.5f);
            vertices[3] = new float3(0.5f, 0.5f, 0.5f);
            vertices[4] = new float3(-0.5f, 0.5f, -0.5f);
            vertices[5] = new float3(0.5f, 0.5f, -0.5f);
            vertices[6] = new float3(-0.5f, -0.5f, -0.5f);
            vertices[7] = new float3(0.5f, -0.5f, -0.5f);

            indices[0] = 0; indices[1] = 1; indices[2] = 2;
            indices[3] = 2; indices[4] = 1; indices[5] = 3;
            indices[6] = 2; indices[7] = 3; indices[8] = 4;
            indices[9] = 4; indices[10] = 3; indices[11] = 5;
            indices[12] = 4; indices[13] = 5; indices[14] = 6;
            indices[15] = 6; indices[16] = 5; indices[17] = 7;
            indices[18] = 6; indices[19] = 7; indices[20] = 0;
            indices[21] = 0; indices[22] = 7; indices[23] = 1;
            indices[24] = 1; indices[25] = 7; indices[26] = 3;
            indices[27] = 3; indices[28] = 7; indices[29] = 5;
            indices[30] = 6; indices[31] = 0; indices[32] = 4;
            indices[33] = 4; indices[34] = 0; indices[35] = 2;
        }

        [BurstCompile]
        public static void GeneratePolyDisc(out NativeArray<float3> vertices, out NativeArray<int> indices, Allocator allocator)
        {
            vertices = new NativeArray<float3>(CIRCLE_SECTION_COUNT + 1, allocator);
            indices = new NativeArray<int>(CIRCLE_SECTION_COUNT * 3, allocator);

            for (int i = 0; i < CIRCLE_SECTION_COUNT; i++)
            {
                float angle = math.PI2 * i / CIRCLE_SECTION_COUNT;
                vertices[i] = new float3(math.cos(angle), math.sin(angle), 0);
                indices[i * 3] = i;
                indices[i * 3 + 1] = (i + 1) % CIRCLE_SECTION_COUNT;
                indices[i * 3 + 2] = CIRCLE_SECTION_COUNT;
            }
            vertices[CIRCLE_SECTION_COUNT] = new float3(0, 0, 0);
        }
    }
}