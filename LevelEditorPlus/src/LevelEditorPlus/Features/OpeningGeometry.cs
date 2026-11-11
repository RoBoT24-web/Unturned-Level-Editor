using System;
using System.Numerics;

namespace LevelEditorPlus.Features
{
    internal sealed class OpeningGeometry
    {
        public Vector3 Center { get; private set; }
        public Vector3 Right { get; private set; }
        public Vector3 Up { get; private set; }
        public Vector3 Normal { get; private set; }
        public float Width { get; private set; }
        public float Height { get; private set; }
        public bool IgnoredSurfaceDirection { get; private set; }

        public static OpeningGeometry Measure(Vector3 bottomLeft, Vector3 bottomRight,
            Vector3 topLeft, float inset, float depthOffset, Vector3? wallNormal = null)
        {
            if (!Finite(bottomLeft) || !Finite(bottomRight) || !Finite(topLeft) ||
                !Finite(inset) || !Finite(depthOffset) || inset < 0f)
            {
                throw new ArgumentException("Points and offsets must be finite; inset cannot be negative.");
            }

            Vector3 edge = bottomRight - bottomLeft;
            if (wallNormal.HasValue)
            {
                Vector3 n = wallNormal.Value;
                if (!Finite(n) || n.LengthSquared() < 0.000001f ||
                    Math.Abs(Vector3.Normalize(n).Y) > 0.25f)
                {
                    throw new ArgumentException("Pick the front face of a vertical wall first, or disable Upright / wall-aligned for unusual surfaces.");
                }
                n.Y = 0f;
                n = Vector3.Normalize(n);
                Vector3 horizontalEdge = new Vector3(edge.X, 0f, edge.Z);
                float horizontalWidth = horizontalEdge.Length();
                if (!Finite(horizontalWidth) || horizontalWidth < 0.02f)
                {
                    throw new ArgumentException("Bottom picks must span the opening horizontally by at least 2 cm.");
                }
                Vector3 pickedRight = horizontalEdge / horizontalWidth;
                Vector3 surfaceRight = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, n));
                if (Vector3.Dot(pickedRight, surfaceRight) < 0f)
                    surfaceRight = -surfaceRight;
                // A jamb's inner face is a valid vertical hit, but its normal
                // runs across the opening. Never let it turn the fit sideways.
                bool ignoreSurface = Vector3.Dot(pickedRight, surfaceRight) < 0.9659258f;
                Vector3 lockedRight = ignoreSurface ? pickedRight : surfaceRight;
                n = Vector3.Cross(lockedRight, Vector3.UnitY);
                float lockedWidth = Vector3.Dot(edge, lockedRight);
                float bottomY = (bottomLeft.Y + bottomRight.Y) * 0.5f;
                float lockedHeight = topLeft.Y - bottomY;
                if (!Finite(lockedWidth) || !Finite(lockedHeight) ||
                    lockedWidth - 2f * inset < 0.02f || lockedHeight - 2f * inset < 0.02f)
                {
                    throw new ArgumentException("Opening must be at least 2 cm wide/high after inset. Pick left, right, then top.");
                }
                return new OpeningGeometry
                {
                    Center = bottomLeft + lockedRight * (lockedWidth * 0.5f) +
                        Vector3.UnitY * ((bottomY + topLeft.Y) * 0.5f - bottomLeft.Y) + n * depthOffset,
                    Right = lockedRight,
                    Up = Vector3.UnitY,
                    Normal = n,
                    Width = lockedWidth - 2f * inset,
                    Height = lockedHeight - 2f * inset,
                    IgnoredSurfaceDirection = ignoreSurface,
                };
            }
            float width = edge.Length();
            if (!Finite(width) || width < 0.02f)
            {
                throw new ArgumentException("Bottom corners must be at least 2 cm apart.");
            }
            Vector3 right = edge / width;
            Vector3 rise = topLeft - bottomLeft;
            float sideways = Vector3.Dot(rise, right);
            Vector3 vertical = rise - right * sideways;
            float height = vertical.Length();
            if (!Finite(height) || height < 0.02f)
            {
                throw new ArgumentException("Top corner must be at least 2 cm above the bottom edge.");
            }
            if (Math.Abs(sideways) > Math.Max(0.05f, width * 0.05f))
            {
                throw new ArgumentException("Pick the top LEFT corner, directly above the first point.");
            }
            if (width - 2f * inset < 0.02f || height - 2f * inset < 0.02f)
            {
                throw new ArgumentException("Inset is too large for this opening.");
            }

            Vector3 up = vertical / height;
            Vector3 normal = Vector3.Normalize(Vector3.Cross(right, up));
            return new OpeningGeometry
            {
                Center = bottomLeft + edge * 0.5f + vertical * 0.5f + normal * depthOffset,
                Right = right,
                Up = up,
                Normal = normal,
                Width = width - 2f * inset,
                Height = height - 2f * inset,
            };
        }

        public (Vector3 Position, Quaternion Rotation, Vector3 Scale) Place(Vector3 localCenter,
            Vector3 nativeSize, int heightAxis, Vector3 currentScale, Quaternion currentRotation,
            bool resize)
        {
            if (!Finite(localCenter) || !Finite(nativeSize) ||
                nativeSize.X <= 0f || nativeSize.Y <= 0f || nativeSize.Z <= 0f ||
                !Finite(currentScale) || currentScale.X <= 0f ||
                currentScale.Y <= 0f || currentScale.Z <= 0f ||
                (heightAxis != 1 && heightAxis != 2))
            {
                throw new ArgumentException("Slot size and scale must be positive and finite.");
            }
            Vector3 scale = currentScale;
            Quaternion rotation = currentRotation;
            if (resize)
            {
                scale.X = Width / nativeSize.X;
                if (heightAxis == 2)
                    scale.Z = Height / nativeSize.Z;
                else
                    scale.Y = Height / nativeSize.Y;

                Vector3 sourceUp = heightAxis == 2 ? Vector3.UnitZ : Vector3.UnitY;
                Quaternion source = Basis(Vector3.UnitX, sourceUp, Vector3.Cross(Vector3.UnitX, sourceUp));
                rotation = Quaternion.Normalize(Basis(Right, Up, Normal) * Quaternion.Inverse(source));
            }
            // Prefab pivot and slot center differ; align the collider, not the pivot.
            Vector3 position = Center - Vector3.Transform(localCenter * scale, rotation);
            if (!Finite(position) || !Finite(scale))
            {
                throw new ArgumentException("Resulting slot transform is not finite.");
            }
            return (position, rotation, scale);
        }

        private static Quaternion Basis(Vector3 right, Vector3 up, Vector3 normal)
        {
            return Quaternion.CreateFromRotationMatrix(new Matrix4x4(
                right.X, right.Y, right.Z, 0,
                up.X, up.Y, up.Z, 0,
                normal.X, normal.Y, normal.Z, 0,
                0, 0, 0, 1));
        }

        private static bool Finite(Vector3 v) => Finite(v.X) && Finite(v.Y) && Finite(v.Z);
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
