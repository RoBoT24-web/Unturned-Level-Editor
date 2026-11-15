using System;
using System.Numerics;

namespace LevelEditorPlus.Features
{
    internal static class OpeningDetector
    {
        private const float Step = 0.02f;
        private const float Reach = 6f;
        private const float EdgeTolerance = 0.003f;
        private const float ValidationSpacing = 0.1f;

        public static OpeningGeometry Detect(Vector3 seed, Vector3 right, Vector3 up,
            Func<Vector3, bool> isSolid, float inset, float depth)
        {
            if (isSolid == null)
                throw new ArgumentNullException(nameof(isSolid));
            if (!Finite(seed) || !Finite(right) || !Finite(up) ||
                right.LengthSquared() < 0.000001f || up.LengthSquared() < 0.000001f)
                throw new ArgumentException("Opening probe needs finite points and non-zero axes.");
            right = Vector3.Normalize(right);
            up = Vector3.Normalize(up);
            if (Math.Abs(Vector3.Dot(right, up)) > 0.001f)
                throw new ArgumentException("Opening probe axes must be perpendicular.");
            if (isSolid(seed))
                throw new ArgumentException("Aim inside the empty opening, not at the frame or a closed door.");

            float left = FindEdge(seed, -right, isSolid);
            float rightEdge = FindEdge(seed, right, isSolid);
            float bottom = FindEdge(seed, -up, isSolid);
            float top = FindEdge(seed, up, isSolid);
            float width = left + rightEdge;
            float height = bottom + top;
            if (width < 0.1f || height < 0.1f)
                throw new ArgumentException("Detected opening is too small. Use manual Fit + Center.");

            Vector3 bottomLeft = seed - right * left - up * bottom;
            ValidateEdge(bottomLeft, right, up, width, isSolid);
            ValidateEdge(bottomLeft + up * height, right, -up, width, isSolid);
            ValidateEdge(bottomLeft, up, right, height, isSolid);
            ValidateEdge(bottomLeft + right * width, up, -right, height, isSolid);

            int columns = Math.Max(2, (int)Math.Ceiling(width / ValidationSpacing));
            int rows = Math.Max(2, (int)Math.Ceiling(height / ValidationSpacing));
            for (int x = 1; x < columns; x++)
            {
                for (int y = 1; y < rows; y++)
                {
                    if (isSolid(bottomLeft + right * (width * x / columns) +
                        up * (height * y / rows)))
                        throw new ArgumentException("Opening is obstructed or not rectangular. Use manual Fit + Center.");
                }
            }
            return OpeningGeometry.Measure(bottomLeft, bottomLeft + right * width,
                bottomLeft + up * height, inset, depth);
        }

        private static float FindEdge(Vector3 seed, Vector3 direction, Func<Vector3, bool> isSolid)
        {
            for (int i = 1; i <= (int)(Reach / Step); i++)
            {
                float distance = i * Step;
                if (!isSolid(seed + direction * distance))
                    continue;
                float empty = distance - Step;
                float solid = distance;
                while (solid - empty > 0.001f)
                {
                    float middle = (empty + solid) * 0.5f;
                    if (isSolid(seed + direction * middle))
                        solid = middle;
                    else
                        empty = middle;
                }
                return (empty + solid) * 0.5f;
            }
            throw new ArgumentException("No enclosing frame within 6 m in every direction. Use manual Fit + Center.");
        }

        private static void ValidateEdge(Vector3 start, Vector3 along, Vector3 inward,
            float length, Func<Vector3, bool> isSolid)
        {
            int samples = Math.Max(2, (int)Math.Ceiling(length / ValidationSpacing));
            for (int i = 1; i < samples; i++)
            {
                Vector3 point = start + along * (length * i / samples);
                if (isSolid(point + inward * EdgeTolerance) ||
                    !isSolid(point - inward * EdgeTolerance))
                    throw new ArgumentException("Frame edges are irregular or incomplete. Use manual Fit + Center.");
            }
        }

        private static bool Finite(Vector3 v) => Finite(v.X) && Finite(v.Y) && Finite(v.Z);
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
