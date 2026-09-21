using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BubblePics.GameModes
{
    /// <summary>
    /// Round-local word mapping and the recovered compact formation rules.
    /// Words are rendered as labels inside transparent bubble carriers; they
    /// are not baked into square textures.
    /// </summary>
    public static class WordMatchContent
    {
        public static bool IsWordImage(int imageId)
        {
            return ModeSession.ActiveKind == GameplayKind.WordMatch &&
                   TryGetGroup(imageId, out _);
        }

        public static string WordFor(int imageId, int slot)
        {
            if (!TryGetGroup(imageId, out CompiledModeGroup group) ||
                !group.CodesBySlot.TryGetValue(slot, out string code) ||
                !ModeCatalogRepository.WordCatalog.Words.TryGetValue(
                    code,
                    out WordEntry entry))
                return string.Empty;
            return entry.Label.Resolve();
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
            return NumberMatchContent.FormationOffset(slot, quadrants, cell);
        }

        static bool TryGetGroup(int imageId, out CompiledModeGroup group)
        {
            group = null;
            CompiledModeLevel compiled = ModeSession.ActiveCompiledLevel;
            if (compiled == null || compiled.Kind != GameplayKind.WordMatch)
                return false;
            group = compiled.Groups.FirstOrDefault(
                value => value != null && value.ImageId == imageId);
            return group != null;
        }
    }
}
