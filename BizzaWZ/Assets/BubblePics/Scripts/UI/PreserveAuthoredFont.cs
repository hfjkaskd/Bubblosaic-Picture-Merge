using TMPro;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// The prefab explicitly owns this label's font and fallback chain.
    /// Shared locale refreshes must retain those serialized choices.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class PreserveAuthoredFont : MonoBehaviour
    {
    }
}
