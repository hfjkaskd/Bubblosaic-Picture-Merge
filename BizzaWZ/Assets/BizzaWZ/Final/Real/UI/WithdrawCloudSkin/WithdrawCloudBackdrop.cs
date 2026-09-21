using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Loads the withdrawal-only sky when its configured page becomes visible.</summary>
[DisallowMultipleComponent]
public sealed class WithdrawCloudBackdrop : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private string resourcePath = "WithdrawCloud/CloudBackdrop";
    [SerializeField] private Color loadingColor = new Color(0.58f, 0.65f, 1f, 1f);
    [SerializeField] private Color loadedTint = Color.white;
    private Sprite loadedSprite;
    private Coroutine loading;

    private void OnEnable()
    {
        if (targetImage == null) return;
        if (loadedSprite != null)
        {
            targetImage.sprite = loadedSprite;
            targetImage.color = loadedTint;
            return;
        }
        targetImage.color = loadingColor;
        loading = StartCoroutine(Load());
    }

    private IEnumerator Load()
    {
        ResourceRequest request = Resources.LoadAsync<Sprite>(resourcePath);
        yield return request;
        loading = null;
        loadedSprite = request.asset as Sprite;
        if (loadedSprite == null)
        {
            Debug.LogError("Missing withdrawal backdrop: " + resourcePath, this);
            yield break;
        }
        targetImage.sprite = loadedSprite;
        targetImage.color = loadedTint;
    }

    private void OnDisable()
    {
        if (loading != null)
        {
            StopCoroutine(loading);
            loading = null;
        }
    }
}
