using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BubblePics.GameModes
{
    /// <summary>
    /// Round-local category icon mapping and the recovered Godot formation
    /// rules. Category icons remain independent transparent textures instead
    /// of being baked into a white four-quadrant photo.
    /// </summary>
    public static class CategoryMatchContent
    {
        static readonly float[] IconDisplayRatios = { 1f, 0.86f, 0.82f, 0.78f };

        public static bool IsCategoryImage(int imageId)
        {
            return ModeSession.ActiveKind == GameplayKind.CategoryMatch &&
                   TryGetGroup(imageId, out _);
        }

        public static Texture2D IconFor(int imageId, int slot)
        {
            if (!TryGetGroup(imageId, out CompiledModeGroup group) ||
                !group.CodesBySlot.TryGetValue(slot, out string code) ||
                ModeSession.ActiveCategoryIcons == null ||
                !ModeSession.ActiveCategoryIcons.TryGetValue(
                    code,
                    out Texture2D texture))
                return null;
            return texture;
        }

        public static string LabelFor(int imageId)
        {
            return TryGetGroup(imageId, out CompiledModeGroup group)
                ? group.GroupLabel ?? string.Empty
                : string.Empty;
        }

        public static float FormationCell(
            IReadOnlyCollection<int> quadrants,
            float radius,
            int count = -1)
        {
            return NumberMatchContent.FormationCell(quadrants, radius, count);
        }

        public static Vector2 FormationOffset(
            int slot,
            IReadOnlyCollection<int> quadrants,
            float cell)
        {
            var sorted = quadrants == null
                ? new List<int> { slot }
                : quadrants.OrderBy(value => value).ToList();
            int rank = sorted.IndexOf(slot);
            if (rank < 0) rank = 0;
            switch (Mathf.Clamp(sorted.Count, 1, 4))
            {
                case 2:
                    return (rank == 0
                        ? new Vector2(0f, -0.95f)
                        : new Vector2(0f, 0.95f)) * (cell * 0.5f);
                case 3:
                    return new[]
                    {
                        new Vector2(0f, -1.22f),
                        new Vector2(1.05f, 0.61f),
                        new Vector2(-1.05f, 0.61f),
                    }[Mathf.Clamp(rank, 0, 2)] * (cell * 0.5f);
                default:
                    return NumberMatchContent.FormationOffset(
                        slot,
                        sorted,
                        cell);
            }
        }

        public static float IconDisplaySize(float cell, int count = 1)
        {
            int safeCount = Mathf.Clamp(count, 1, 4);
            return cell * IconDisplayRatios[safeCount - 1];
        }

        static bool TryGetGroup(
            int imageId,
            out CompiledModeGroup group)
        {
            group = null;
            CompiledModeLevel compiled = ModeSession.ActiveCompiledLevel;
            if (compiled == null || compiled.Kind != GameplayKind.CategoryMatch)
                return false;
            group = compiled.Groups.FirstOrDefault(
                value => value != null && value.ImageId == imageId);
            return group != null;
        }
    }
}
