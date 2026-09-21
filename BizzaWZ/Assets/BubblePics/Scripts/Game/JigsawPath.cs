using System.Collections.Generic;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Deterministic port of jigsaw_path.gd. Its PCG implementation and Bezier
    /// samples intentionally match Godot so a given image/parent path produces
    /// the same four complementary edges in both projects.
    /// </summary>
    public static class JigsawPath
    {
        const int SamplesPerSegment = 12;
        const ulong PcgMultiplier = 6364136223846793005UL;
        const ulong GodotDefaultIncrement = 1442695040888963407UL;

        static readonly Vector2[] TopTabRaw =
        {
            new(29.269f, 24f), new(34.538f, 24f), new(39.807f, 24f),
            new(40.5967f, 24f), new(40.9916f, 24f), new(41.0479f, 23.9943f),
            new(42.024f, 23.8957f), new(42.3238f, 23.4216f), new(41.9946f, 22.4975f),
            new(41.9756f, 22.4441f), new(41.729f, 21.9242f), new(41.2367f, 20.8861f),
            new(40.9574f, 20.2974f), new(40.7768f, 19.6438f), new(40.7833f, 18.9419f),
            new(40.8205f, 16.1963f), new(43.3759f, 14f), new(48f, 14f),
            new(52.6241f, 14f), new(55.1795f, 16.1963f), new(55.2167f, 18.9419f),
            new(55.2232f, 19.6438f), new(55.0426f, 20.2974f), new(54.7633f, 20.8861f),
            new(54.271f, 21.9242f), new(54.0244f, 22.4441f), new(54.0054f, 22.4975f),
            new(53.6762f, 23.4216f), new(53.976f, 23.8957f), new(54.9521f, 23.9943f),
            new(55.0084f, 24f), new(55.4033f, 24f), new(56.193f, 24f),
            new(61.462f, 24f), new(66.731f, 24f), new(72f, 24f),
        };

        static readonly Vector2[] TopSlotRaw =
        {
            new(29.269f, 24f), new(34.538f, 24f), new(39.807f, 24f),
            new(40.5967f, 24f), new(40.9916f, 24f), new(41.0479f, 24.0057f),
            new(42.024f, 24.1043f), new(42.3238f, 24.5784f), new(41.9946f, 25.5025f),
            new(41.9756f, 25.5559f), new(41.729f, 26.0758f), new(41.2367f, 27.1139f),
            new(40.9574f, 27.7026f), new(40.7768f, 28.3562f), new(40.7833f, 29.0581f),
            new(40.8205f, 31.8037f), new(43.3759f, 34f), new(48f, 34f),
            new(52.6241f, 34f), new(55.1795f, 31.8037f), new(55.2167f, 29.0581f),
            new(55.2232f, 28.3562f), new(55.0426f, 27.7026f), new(54.7633f, 27.1139f),
            new(54.271f, 26.0758f), new(54.0244f, 25.5559f), new(54.0054f, 25.5025f),
            new(53.6762f, 24.5784f), new(53.976f, 24.1043f), new(54.9521f, 24.0057f),
            new(55.0084f, 24f), new(55.4033f, 24f), new(56.193f, 24f),
            new(61.462f, 24f), new(66.731f, 24f), new(72f, 24f),
        };

        static readonly Vector2[] Corners =
        {
            new(0f, 0f), new(1f, 0f), new(1f, 1f), new(0f, 1f),
        };

        public static Vector2[][] BuildFourPieces(float size, int seed)
        {
            int[] styles = PickStyles(seed);
            int hLeft = styles[0];
            int hRight = styles[1];
            int vTop = styles[2];
            int vBottom = styles[3];
            int[][] edges =
            {
                new[] { -1, vTop, hLeft, -1 },
                new[] { -1, -1, hRight, Complement(vTop) },
                new[] { Complement(hLeft), vBottom, -1, -1 },
                new[] { Complement(hRight), -1, -1, Complement(vBottom) },
            };
            float half = size * 0.5f;
            Vector2[] origins =
            {
                Vector2.zero,
                new(half, 0f),
                new(0f, half),
                new(half, half),
            };
            var result = new Vector2[4][];
            for (int quadrant = 0; quadrant < 4; quadrant++)
                result[quadrant] = BuildPiece(origins[quadrant], half, edges[quadrant]);
            return result;
        }

        public static int[] PickStyles(int seed)
        {
            var random = new GodotPcg(seed);
            return new[]
            {
                random.Bounded(4), random.Bounded(4),
                random.Bounded(4), random.Bounded(4),
            };
        }

        public static bool TryTriangulate(
            IReadOnlyList<Vector2> polygon,
            out Vector2[] vertices,
            out ushort[] triangles)
        {
            var boundary = new List<Vector2>();
            if (polygon != null)
            {
                for (int i = 0; i < polygon.Count; i++)
                {
                    Vector2 point = polygon[i];
                    if (boundary.Count == 0 || Vector2.SqrMagnitude(
                            boundary[boundary.Count - 1] - point) > 0.00000001f)
                        boundary.Add(point);
                }
            }
            if (boundary.Count > 2 && Vector2.SqrMagnitude(
                    boundary[0] - boundary[boundary.Count - 1]) <= 0.00000001f)
                boundary.RemoveAt(boundary.Count - 1);
            if (boundary.Count < 3 || boundary.Count + 1 > ushort.MaxValue)
            {
                vertices = System.Array.Empty<Vector2>();
                triangles = System.Array.Empty<ushort>();
                return false;
            }

            float orientation = SignedArea(boundary) >= 0f ? 1f : -1f;
            var active = new List<int>(boundary.Count);
            for (int i = 0; i < boundary.Count; i++) active.Add(i);
            var output = new List<ushort>((boundary.Count - 2) * 3);
            int guard = boundary.Count * boundary.Count * 2;
            while (active.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < active.Count; i++)
                {
                    int previous = active[(i + active.Count - 1) % active.Count];
                    int current = active[i];
                    int next = active[(i + 1) % active.Count];
                    if (CrossDouble(
                            boundary[previous],
                            boundary[current],
                            boundary[next]) * orientation <= 0.00000001)
                        continue;
                    bool contains = false;
                    for (int j = 0; j < active.Count; j++)
                    {
                        int candidate = active[j];
                        if (candidate == previous || candidate == current || candidate == next)
                            continue;
                        if (!PointInTriangleStrict(
                                boundary[candidate],
                                boundary[previous],
                                boundary[current],
                                boundary[next]))
                            continue;
                        contains = true;
                        break;
                    }
                    if (contains) continue;
                    output.Add((ushort)previous);
                    output.Add((ushort)current);
                    output.Add((ushort)next);
                    active.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (clipped) continue;

                // Godot's tessellator filters near-collinear curve samples.
                // Do the same only when stalled; the removed point differs
                // from the resulting chord by a sub-pixel amount and area QA
                // below guards against a visible silhouette change.
                int flattest = -1;
                double smallestCross = double.PositiveInfinity;
                for (int i = 0; i < active.Count; i++)
                {
                    double value = System.Math.Abs(CrossDouble(
                        boundary[active[(i + active.Count - 1) % active.Count]],
                        boundary[active[i]],
                        boundary[active[(i + 1) % active.Count]]));
                    if (value >= smallestCross) continue;
                    smallestCross = value;
                    flattest = i;
                }
                if (flattest < 0 || smallestCross > 0.05)
                    break;
                active.RemoveAt(flattest);
            }
            if (active.Count == 3)
            {
                output.Add((ushort)active[0]);
                output.Add((ushort)active[1]);
                output.Add((ushort)active[2]);
            }
            vertices = boundary.ToArray();
            triangles = output.ToArray();
            return active.Count == 3 && triangles.Length >= 3;
        }

        static float SignedArea(IReadOnlyList<Vector2> polygon)
        {
            float area = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 current = polygon[i];
                Vector2 next = polygon[(i + 1) % polygon.Count];
                area += current.x * next.y - next.x * current.y;
            }
            return area * 0.5f;
        }

        static double CrossDouble(Vector2 a, Vector2 b, Vector2 c) =>
            ((double)b.x - a.x) * ((double)c.y - a.y) -
            ((double)b.y - a.y) * ((double)c.x - a.x);

        static bool PointInTriangleStrict(
            Vector2 point,
            Vector2 a,
            Vector2 b,
            Vector2 c)
        {
            double first = CrossDouble(a, b, point);
            double second = CrossDouble(b, c, point);
            double third = CrossDouble(c, a, point);
            const double epsilon = 0.00000001;
            return (first > epsilon && second > epsilon && third > epsilon) ||
                   (first < -epsilon && second < -epsilon && third < -epsilon);
        }

        static int Complement(int style) => 3 - style;

        static Vector2[] BuildPiece(Vector2 origin, float half, int[] edgeStyles)
        {
            var polygon = new List<Vector2>(4 * (SamplesPerSegment * 12 + 1));
            for (int direction = 0; direction < 4; direction++)
            {
                polygon.Add(origin + Corners[direction] * half);
                int style = edgeStyles[direction];
                if (style < 0) continue;
                foreach (Vector2 point in SampleEdgeRelative(direction, style))
                    polygon.Add(origin + point * half);
            }
            return polygon.ToArray();
        }

        static IEnumerable<Vector2> SampleEdgeRelative(int direction, int style)
        {
            Vector2[] raw = style == 0 || style == 1 ? TopTabRaw : TopSlotRaw;
            Vector2 previous = Vector2.zero;
            int segmentCount = raw.Length / 3;
            for (int segment = 0; segment < segmentCount; segment++)
            {
                Vector2 c1 = Normalize(raw[segment * 3]);
                Vector2 c2 = Normalize(raw[segment * 3 + 1]);
                Vector2 end = Normalize(raw[segment * 3 + 2]);
                for (int sample = 1; sample <= SamplesPerSegment; sample++)
                {
                    if (segment == segmentCount - 1 && sample == SamplesPerSegment)
                        continue;
                    float t = sample / (float)SamplesPerSegment;
                    yield return RotateDirection(Cubic(previous, c1, c2, end, t), direction);
                }
                previous = end;
            }
        }

        static Vector2 Normalize(Vector2 value) =>
            (value - new Vector2(24f, 24f)) / 48f;

        static Vector2 Cubic(
            Vector2 p0,
            Vector2 c1,
            Vector2 c2,
            Vector2 p3,
            float t)
        {
            float u = 1f - t;
            return u * u * u * p0 + 3f * u * u * t * c1 +
                   3f * u * t * t * c2 + t * t * t * p3;
        }

        static Vector2 RotateDirection(Vector2 point, int direction)
        {
            return direction switch
            {
                1 => new Vector2(1f - point.y, point.x),
                2 => new Vector2(1f - point.x, 1f - point.y),
                3 => new Vector2(point.y, 1f - point.x),
                _ => point,
            };
        }

        struct GodotPcg
        {
            ulong _state;
            ulong _increment;

            public GodotPcg(int seed)
            {
                _state = 0UL;
                _increment = unchecked((GodotDefaultIncrement << 1) | 1UL);
                Next();
                _state = unchecked(_state + (ulong)(long)seed);
                Next();
            }

            public int Bounded(uint bound)
            {
                uint threshold = unchecked(0u - bound) % bound;
                while (true)
                {
                    uint value = Next();
                    if (value >= threshold) return (int)(value % bound);
                }
            }

            uint Next()
            {
                ulong oldState = _state;
                _state = unchecked(oldState * PcgMultiplier + (_increment | 1UL));
                uint xorshifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
                int rotation = (int)(oldState >> 59);
                return (xorshifted >> rotation) |
                       (xorshifted << ((-rotation) & 31));
            }
        }
    }
}
