using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BubblePics.GameModes
{
    /// <summary>
    /// Runtime view of the recovered Tangram target catalog. The source
    /// polygons stay data-driven; this class only performs the same centering,
    /// fitting and deterministic color-order work as TangramContent.gd.
    /// </summary>
    public static class TangramMatchContent
    {
        public const float MoldRadiusScale = 1.5f;
        public const float FillRatio = 0.82f;

        static readonly Color[] Palette =
        {
            new Color(0.611765f, 0.152941f, 0.690196f, 1f),
            new Color(0.909804f, 0.454902f, 0.231373f, 1f),
            new Color(0.360784f, 0.721569f, 0.360784f, 1f),
            new Color(0.941176f, 0.768627f, 0.098039f, 1f),
        };

        static Texture2D _whiteTexture;

        public static bool IsTangramImage(int imageId)
        {
            return ModeSession.ActiveKind == GameplayKind.Tangram &&
                   TryGetTarget(imageId, out _);
        }

        public static bool TryGetTarget(
            int imageId,
            out TangramTargetData target)
        {
            target = null;
            CompiledModeLevel compiled = ModeSession.ActiveCompiledLevel;
            if (compiled == null || compiled.Kind != GameplayKind.Tangram)
                return false;
            CompiledModeGroup group = compiled.Groups.FirstOrDefault(
                value => value != null && value.ImageId == imageId);
            target = group?.TangramTarget;
            return target != null;
        }

        public static Texture2D WhiteTexture
        {
            get
            {
                if (_whiteTexture != null) return _whiteTexture;
                _whiteTexture = new Texture2D(
                    16, 16, TextureFormat.RGBA32, false)
                {
                    name = "TangramWhite",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                var pixels = new Color32[16 * 16];
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32(255, 255, 255, 255);
                _whiteTexture.SetPixels32(pixels);
                _whiteTexture.Apply(false, true);
                return _whiteTexture;
            }
        }

        public static Color PieceColor(int imageId, int slot)
        {
            int[] order = ColorOrder(imageId);
            int index = slot >= 0 && slot < order.Length
                ? order[slot]
                : Mathf.Clamp(slot, 0, Palette.Length - 1);
            return Palette[Mathf.Clamp(index, 0, Palette.Length - 1)];
        }

        public static Vector2[] PieceCenteredPolygon(int imageId, int slot)
        {
            Vector2[] world = PieceWorldPolygon(imageId, slot);
            Vector2 center = PolygonCenter(world);
            return world.Select(point => point - center).ToArray();
        }

        public static Vector2[] PieceWorldPolygon(int imageId, int slot)
        {
            if (!TryGetPiece(imageId, slot, out TangramPieceData piece))
                return Array.Empty<Vector2>();
            Bounds2 bounds = SilhouetteBounds(imageId);
            return piece.Polygon.Select(point => point - bounds.Center).ToArray();
        }

        public static float FormationScale(
            int imageId,
            IReadOnlyList<int> slots,
            float radius,
            bool forceFull = false)
        {
            int[] fitted = forceFull
                ? new[] { 0, 1, 2, 3 }
                : DistinctSlots(slots);
            if (fitted.Length == 0) fitted = new[] { 0 };
            Bounds2 bounds = BoundsFor(imageId, fitted);
            float maxDistance = 0.0001f;
            foreach (int slot in fitted)
            {
                foreach (Vector2 point in PieceWorldPolygon(imageId, slot))
                    maxDistance = Mathf.Max(
                        maxDistance,
                        Vector2.Distance(point, bounds.Center));
            }
            return Mathf.Max(0.0001f, radius * FillRatio / maxDistance);
        }

        public static Vector2 FormationOffset(
            int imageId,
            int slot,
            IReadOnlyList<int> slots,
            float scale,
            bool forceFull = false)
        {
            int[] fitted = forceFull
                ? new[] { 0, 1, 2, 3 }
                : DistinctSlots(slots);
            if (fitted.Length == 0) fitted = new[] { slot };
            Bounds2 bounds = BoundsFor(imageId, fitted);
            return (PolygonCenter(PieceWorldPolygon(imageId, slot)) -
                    bounds.Center) * scale;
        }

        public static bool TryTriangulate(
            IReadOnlyList<Vector2> input,
            out Vector2[] vertices,
            out ushort[] triangles)
        {
            var clean = new List<Vector2>();
            if (input != null)
            {
                for (int i = 0; i < input.Count; i++)
                {
                    Vector2 point = input[i];
                    if (clean.Count == 0 ||
                        Vector2.SqrMagnitude(clean[clean.Count - 1] - point) >
                        0.00000001f)
                        clean.Add(point);
                }
            }
            if (clean.Count > 2 &&
                Vector2.SqrMagnitude(clean[0] - clean[clean.Count - 1]) <
                0.00000001f)
                clean.RemoveAt(clean.Count - 1);
            RemoveCollinear(clean);
            vertices = clean.ToArray();
            if (vertices.Length < 3 || vertices.Length > ushort.MaxValue)
            {
                triangles = Array.Empty<ushort>();
                return false;
            }

            float orientation = SignedArea(vertices) >= 0f ? 1f : -1f;
            var indices = Enumerable.Range(0, vertices.Length).ToList();
            var output = new List<ushort>((vertices.Length - 2) * 3);
            int guard = vertices.Length * vertices.Length;
            while (indices.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < indices.Count; i++)
                {
                    int previous = indices[(i + indices.Count - 1) % indices.Count];
                    int current = indices[i];
                    int next = indices[(i + 1) % indices.Count];
                    if (Cross(
                            vertices[previous],
                            vertices[current],
                            vertices[next]) * orientation <= 0.0000001f)
                        continue;
                    bool contains = false;
                    for (int j = 0; j < indices.Count; j++)
                    {
                        int candidate = indices[j];
                        if (candidate == previous || candidate == current ||
                            candidate == next)
                            continue;
                        if (!PointInTriangle(
                                vertices[candidate],
                                vertices[previous],
                                vertices[current],
                                vertices[next]))
                            continue;
                        contains = true;
                        break;
                    }
                    if (contains) continue;
                    output.Add((ushort)previous);
                    output.Add((ushort)current);
                    output.Add((ushort)next);
                    indices.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            if (indices.Count == 3)
            {
                output.Add((ushort)indices[0]);
                output.Add((ushort)indices[1]);
                output.Add((ushort)indices[2]);
            }
            triangles = output.ToArray();
            return triangles.Length == (vertices.Length - 2) * 3;
        }

        static int[] ColorOrder(int imageId)
        {
            int[] previous = null;
            int[] result = { 0, 1, 2, 3 };
            for (int current = 0; current <= imageId; current++)
            {
                if (!TryGetTarget(current, out TangramTargetData target))
                    continue;
                int seed = unchecked(target.Code * 73856093 + 19349663);
                foreach (TangramPieceData piece in target.Pieces ??
                         Array.Empty<TangramPieceData>())
                {
                    seed = unchecked(seed * 31 + piece.PieceId);
                    seed = unchecked(seed * 31 + piece.Polygon.Length);
                }
                var random = new System.Random(seed & int.MaxValue);
                result = new[] { 0, 1, 2, 3 };
                for (int index = result.Length - 1; index > 0; index--)
                {
                    int other = random.Next(index + 1);
                    (result[index], result[other]) =
                        (result[other], result[index]);
                }
                if (result.SequenceEqual(new[] { 0, 1, 2, 3 }))
                    (result[0], result[1]) = (result[1], result[0]);
                if (previous != null && result.SequenceEqual(previous))
                {
                    (result[2], result[3]) = (result[3], result[2]);
                    if (result.SequenceEqual(new[] { 0, 1, 2, 3 }))
                        (result[0], result[1]) = (result[1], result[0]);
                }
                previous = (int[])result.Clone();
            }
            return result;
        }

        static bool TryGetPiece(
            int imageId,
            int slot,
            out TangramPieceData piece)
        {
            piece = null;
            if (!TryGetTarget(imageId, out TangramTargetData target))
                return false;
            piece = (target.Pieces ?? Array.Empty<TangramPieceData>())
                .FirstOrDefault(value => value != null && value.PieceId == slot);
            return piece?.Polygon != null && piece.Polygon.Length >= 3;
        }

        static int[] DistinctSlots(IReadOnlyList<int> slots)
        {
            return slots == null
                ? Array.Empty<int>()
                : slots.Where(slot => slot >= 0 && slot < 4)
                    .Distinct()
                    .ToArray();
        }

        static Bounds2 SilhouetteBounds(int imageId)
        {
            return BoundsFor(imageId, new[] { 0, 1, 2, 3 }, false);
        }

        static Bounds2 BoundsFor(
            int imageId,
            IReadOnlyList<int> slots,
            bool world = true)
        {
            bool initialized = false;
            Vector2 min = Vector2.zero;
            Vector2 max = Vector2.zero;
            foreach (int slot in slots)
            {
                Vector2[] polygon = world
                    ? PieceWorldPolygon(imageId, slot)
                    : TryGetPiece(imageId, slot, out TangramPieceData piece)
                        ? piece.Polygon
                        : Array.Empty<Vector2>();
                foreach (Vector2 point in polygon)
                {
                    if (!initialized)
                    {
                        min = max = point;
                        initialized = true;
                    }
                    else
                    {
                        min = Vector2.Min(min, point);
                        max = Vector2.Max(max, point);
                    }
                }
            }
            return initialized
                ? new Bounds2(min, max)
                : new Bounds2(Vector2.zero, Vector2.one);
        }

        static Vector2 PolygonCenter(IReadOnlyList<Vector2> polygon)
        {
            if (polygon == null || polygon.Count == 0) return Vector2.zero;
            Vector2 total = Vector2.zero;
            for (int i = 0; i < polygon.Count; i++) total += polygon[i];
            return total / polygon.Count;
        }

        static void RemoveCollinear(List<Vector2> vertices)
        {
            bool changed = true;
            int guard = vertices.Count * 2;
            while (changed && vertices.Count > 3 && guard-- > 0)
            {
                changed = false;
                for (int i = 0; i < vertices.Count; i++)
                {
                    Vector2 previous =
                        vertices[(i + vertices.Count - 1) % vertices.Count];
                    Vector2 current = vertices[i];
                    Vector2 next = vertices[(i + 1) % vertices.Count];
                    if (Mathf.Abs(Cross(previous, current, next)) > 0.000001f)
                        continue;
                    vertices.RemoveAt(i);
                    changed = true;
                    break;
                }
            }
        }

        static float SignedArea(IReadOnlyList<Vector2> vertices)
        {
            float area = 0f;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector2 current = vertices[i];
                Vector2 next = vertices[(i + 1) % vertices.Count];
                area += current.x * next.y - next.x * current.y;
            }
            return area * 0.5f;
        }

        static float Cross(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.x - a.x) * (c.y - a.y) -
                   (b.y - a.y) * (c.x - a.x);
        }

        static bool PointInTriangle(
            Vector2 point,
            Vector2 a,
            Vector2 b,
            Vector2 c)
        {
            float first = Cross(a, b, point);
            float second = Cross(b, c, point);
            float third = Cross(c, a, point);
            const float epsilon = 0.0000001f;
            return (first > epsilon && second > epsilon && third > epsilon) ||
                   (first < -epsilon && second < -epsilon && third < -epsilon);
        }

        readonly struct Bounds2
        {
            public readonly Vector2 Min;
            public readonly Vector2 Max;
            public Vector2 Center => (Min + Max) * 0.5f;

            public Bounds2(Vector2 min, Vector2 max)
            {
                Min = min;
                Max = max;
            }
        }
    }
}
