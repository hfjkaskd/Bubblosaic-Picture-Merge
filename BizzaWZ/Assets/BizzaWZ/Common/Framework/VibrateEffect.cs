using UnityEngine;

// Keep the component type for existing prefab references. Ambient effects no
// longer vibrate; only a rejected gameplay merge produces haptic feedback.
public class VibrateEffect : MonoBehaviour
{
    public E_VibrateType vibrateType = E_VibrateType.Light;
}
