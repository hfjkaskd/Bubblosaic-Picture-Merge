using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BubblePics.GameModes
{
    internal static class ModeTextureComposer
    {
        const int Size = 512;
        const int Cell = Size / 2;

        static readonly Color32[] CellColors =
        {
            new Color32(93, 193, 255, 255),
            new Color32(255, 151, 104, 255),
            new Color32(116, 214, 132, 255),
            new Color32(255, 211, 73, 255),
        };

        static readonly Dictionary<char, string[]> Glyphs = BuildGlyphs();

        public static Texture2D ComposeCategory(Texture2D[] icons)
        {
            // Category bubbles render their source icons directly. Keep this
            // round-completion fallback transparent so it can never introduce
            // the white cards used by the old quadrant-composite path.
            var texture = CreateBase(new Color32(0, 0, 0, 0));
            for (int slot = 0; slot < 4; slot++)
            {
                Texture2D icon = icons != null && slot < icons.Length
                    ? icons[slot]
                    : null;
                if (icon == null) continue;
                BlitIntoCell(texture, icon, slot, 28);
            }
            texture.Apply(false, false);
            texture.name = "CategoryMatchGroup";
            return texture;
        }

        public static Texture2D ComposeWords(string[] words)
        {
            // Word bubbles are label-driven just like the recovered Godot
            // implementation. The shared level loader still needs one carrier
            // texture per target group, so keep it fully transparent.
            var texture = CreateBase(new Color32(0, 0, 0, 0));
            texture.Apply(false, false);
            texture.name = "WordMatchGroup";
            return texture;
        }

        public static Texture2D ComposeTangramPlaceholder(
            TangramTargetData target)
        {
            var texture = CreateBase(new Color32(0, 0, 0, 0));
            TangramPieceData[] pieces = target?.Pieces ??
                Array.Empty<TangramPieceData>();
            Vector2[] all = pieces
                .Where(piece => piece?.Polygon != null)
                .SelectMany(piece => piece.Polygon)
                .ToArray();
            if (all.Length > 0)
            {
                float minX = all.Min(point => point.x);
                float maxX = all.Max(point => point.x);
                float minY = all.Min(point => point.y);
                float maxY = all.Max(point => point.y);
                float span = Mathf.Max(maxX - minX, maxY - minY, 0.001f);
                float scale = (Size - 64f) / span;
                Vector2 center = new Vector2(
                    (minX + maxX) * 0.5f,
                    (minY + maxY) * 0.5f);
                int rotation = Mathf.Abs(target.Code) % 4;
                foreach (TangramPieceData piece in pieces)
                {
                    if (piece?.Polygon == null || piece.Polygon.Length < 3)
                        continue;
                    var pixels = piece.Polygon.Select(point =>
                        new Vector2(
                            Size * 0.5f + (point.x - center.x) * scale,
                            Size * 0.5f - (point.y - center.y) * scale))
                        .ToArray();
                    RasterizePolygon(
                        texture,
                        pixels,
                        CellColors[(piece.PieceId + rotation) % 4]);
                }
            }
            texture.Apply(false, false);
            texture.name = "TangramTarget_" + (target?.Code ?? 0);
            return texture;
        }

        static void RasterizePolygon(
            Texture2D texture,
            IReadOnlyList<Vector2> polygon,
            Color32 color)
        {
            int minX = Mathf.Clamp(
                Mathf.FloorToInt(polygon.Min(point => point.x)), 0, Size - 1);
            int maxX = Mathf.Clamp(
                Mathf.CeilToInt(polygon.Max(point => point.x)), 0, Size - 1);
            int minY = Mathf.Clamp(
                Mathf.FloorToInt(polygon.Min(point => point.y)), 0, Size - 1);
            int maxY = Mathf.Clamp(
                Mathf.CeilToInt(polygon.Max(point => point.y)), 0, Size - 1);
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (PointInPolygon(
                            new Vector2(x + 0.5f, y + 0.5f), polygon))
                        texture.SetPixel(x, y, color);
                }
            }
        }

        static bool PointInPolygon(
            Vector2 point,
            IReadOnlyList<Vector2> polygon)
        {
            bool inside = false;
            for (int current = 0, previous = polygon.Count - 1;
                 current < polygon.Count;
                 previous = current++)
            {
                Vector2 a = polygon[current];
                Vector2 b = polygon[previous];
                bool crosses = (a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) /
                    Mathf.Max(Mathf.Abs(b.y - a.y), 0.000001f) *
                    Mathf.Sign(b.y - a.y) + a.x;
                if (crosses) inside = !inside;
            }
            return inside;
        }

        static Texture2D CreateBase(Color32 color)
        {
            var texture = new Texture2D(
                Size,
                Size,
                TextureFormat.RGBA32,
                false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[Size * Size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            texture.SetPixels32(pixels);
            return texture;
        }

        static void BlitIntoCell(
            Texture2D destination,
            Texture2D source,
            int slot,
            int padding)
        {
            CellRect(slot, out int x0, out int y0);
            int width = Cell - padding * 2;
            int height = Cell - padding * 2;
            float sourceAspect = source.width / (float)Mathf.Max(1, source.height);
            if (sourceAspect > 1f)
                height = Mathf.RoundToInt(height / sourceAspect);
            else
                width = Mathf.RoundToInt(width * sourceAspect);
            int startX = x0 + (Cell - width) / 2;
            int startY = y0 + (Cell - height) / 2;
            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height;
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    Color color;
                    try
                    {
                        color = source.GetPixelBilinear(u, v);
                    }
                    catch (UnityException)
                    {
                        // Downloaded Category textures are readable. If a
                        // platform decoder returns a non-readable texture,
                        // retain the cell background and fail visually safe.
                        return;
                    }
                    destination.SetPixel(startX + x, startY + y, color);
                }
            }
        }

        static void FillCell(Texture2D texture, int slot, Color32 color)
        {
            CellRect(slot, out int x0, out int y0);
            for (int y = 3; y < Cell - 3; y++)
                for (int x = 3; x < Cell - 3; x++)
                    texture.SetPixel(x0 + x, y0 + y, color);
        }

        static void DrawCenteredText(
            Texture2D texture,
            int slot,
            string raw)
        {
            string text = ToDisplayAscii(raw);
            if (text.Length == 0) text = "?";
            int scale = Mathf.Clamp(
                (Cell - 28) / Mathf.Max(1, text.Length * 6),
                2,
                7);
            int width = text.Length * 6 * scale - scale;
            int height = 7 * scale;
            CellRect(slot, out int x0, out int y0);
            int penX = x0 + (Cell - width) / 2;
            int penY = y0 + (Cell - height) / 2;
            for (int i = 0; i < text.Length; i++)
            {
                if (!Glyphs.TryGetValue(text[i], out string[] glyph))
                    glyph = Glyphs['?'];
                for (int row = 0; row < 7; row++)
                {
                    for (int column = 0; column < 5; column++)
                    {
                        if (glyph[row][column] != '1') continue;
                        for (int yy = 0; yy < scale; yy++)
                            for (int xx = 0; xx < scale; xx++)
                                texture.SetPixel(
                                    penX + column * scale + xx,
                                    penY + (6 - row) * scale + yy,
                                    Color.white);
                    }
                }
                penX += 6 * scale;
            }
        }

        static string ToDisplayAscii(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            string upper = raw.Trim().ToUpperInvariant();
            var chars = new List<char>(Mathf.Min(upper.Length, 18));
            for (int i = 0; i < upper.Length && chars.Count < 18; i++)
            {
                char c = upper[i];
                if (Glyphs.ContainsKey(c)) chars.Add(c);
                else if (char.IsWhiteSpace(c)) chars.Add(' ');
            }
            return new string(chars.ToArray()).Trim();
        }

        static void DrawSeams(Texture2D texture)
        {
            for (int offset = -2; offset <= 2; offset++)
            {
                for (int p = 0; p < Size; p++)
                {
                    texture.SetPixel(Cell + offset, p, Color.white);
                    texture.SetPixel(p, Cell + offset, Color.white);
                }
            }
        }

        static void CellRect(int slot, out int x, out int y)
        {
            x = (slot & 1) * Cell;
            // BubbleFragmentGeometry uses a top-left source convention; Unity
            // texture pixels are bottom-left, so source slots 1/2 live high.
            y = slot < 2 ? Cell : 0;
        }

        static Dictionary<char, string[]> BuildGlyphs()
        {
            var map = new Dictionary<char, string[]>();
            void Add(char c, params string[] rows) => map[c] = rows;
            Add(' ', "00000","00000","00000","00000","00000","00000","00000");
            Add('?', "01110","10001","00001","00010","00100","00000","00100");
            Add('-', "00000","00000","00000","11111","00000","00000","00000");
            Add('&', "01100","10010","10100","01000","10101","10010","01101");
            Add('A', "01110","10001","10001","11111","10001","10001","10001");
            Add('B', "11110","10001","10001","11110","10001","10001","11110");
            Add('C', "01111","10000","10000","10000","10000","10000","01111");
            Add('D', "11110","10001","10001","10001","10001","10001","11110");
            Add('E', "11111","10000","10000","11110","10000","10000","11111");
            Add('F', "11111","10000","10000","11110","10000","10000","10000");
            Add('G', "01111","10000","10000","10111","10001","10001","01111");
            Add('H', "10001","10001","10001","11111","10001","10001","10001");
            Add('I', "11111","00100","00100","00100","00100","00100","11111");
            Add('J', "00111","00010","00010","00010","10010","10010","01100");
            Add('K', "10001","10010","10100","11000","10100","10010","10001");
            Add('L', "10000","10000","10000","10000","10000","10000","11111");
            Add('M', "10001","11011","10101","10101","10001","10001","10001");
            Add('N', "10001","11001","10101","10011","10001","10001","10001");
            Add('O', "01110","10001","10001","10001","10001","10001","01110");
            Add('P', "11110","10001","10001","11110","10000","10000","10000");
            Add('Q', "01110","10001","10001","10001","10101","10010","01101");
            Add('R', "11110","10001","10001","11110","10100","10010","10001");
            Add('S', "01111","10000","10000","01110","00001","00001","11110");
            Add('T', "11111","00100","00100","00100","00100","00100","00100");
            Add('U', "10001","10001","10001","10001","10001","10001","01110");
            Add('V', "10001","10001","10001","10001","10001","01010","00100");
            Add('W', "10001","10001","10001","10101","10101","10101","01010");
            Add('X', "10001","10001","01010","00100","01010","10001","10001");
            Add('Y', "10001","10001","01010","00100","00100","00100","00100");
            Add('Z', "11111","00001","00010","00100","01000","10000","11111");
            for (char digit = '0'; digit <= '9'; digit++)
            {
                string[][] digits =
                {
                    new[]{"01110","10001","10011","10101","11001","10001","01110"},
                    new[]{"00100","01100","00100","00100","00100","00100","01110"},
                    new[]{"01110","10001","00001","00010","00100","01000","11111"},
                    new[]{"11110","00001","00001","01110","00001","00001","11110"},
                    new[]{"00010","00110","01010","10010","11111","00010","00010"},
                    new[]{"11111","10000","10000","11110","00001","00001","11110"},
                    new[]{"01110","10000","10000","11110","10001","10001","01110"},
                    new[]{"11111","00001","00010","00100","01000","01000","01000"},
                    new[]{"01110","10001","10001","01110","10001","10001","01110"},
                    new[]{"01110","10001","10001","01111","00001","00001","01110"},
                };
                map[digit] = digits[digit - '0'];
            }
            return map;
        }
    }
}
