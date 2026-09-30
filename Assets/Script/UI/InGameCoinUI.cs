using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quản lý giao diện hiển thị số Coin thu thập được trong trận đấu (In-game Run Coin Counter)
/// Hiển thị ở góc trên bên phải màn hình, nằm kế bên Minimap
/// </summary>
public class InGameCoinUI : MonoBehaviour
{
    public static InGameCoinUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private Image coinIconImage;
    [SerializeField] private TextMeshProUGUI coinText;
    [SerializeField] private RectTransform containerRect;

    private int lastCoinCount = 0;
    private Coroutine punchCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        ApplyTransparentStyle();
    }

    /// <summary>
    /// Làm trong suốt 100% nền của ô Coin HUD (chỉ hiển thị icon và số Coin)
    /// </summary>
    public void ApplyTransparentStyle()
    {
        Image bg = GetComponent<Image>();
        if (bg != null)
        {
            bg.color = Color.clear;
            bg.enabled = false;
        }

        Outline outline = GetComponent<Outline>();
        if (outline != null)
        {
            Destroy(outline);
        }
    }

    private void Start()
    {
        if (RunStatsTracker.Instance != null)
        {
            RunStatsTracker.Instance.OnCurrencyChanged += HandleCurrencyChanged;
            UpdateCoinDisplay(RunStatsTracker.Instance.CurrencyEarned, false);
        }
        else
        {
            UpdateCoinDisplay(0, false);
        }

        AlignWithMinimap();
    }

    private void OnDestroy()
    {
        if (RunStatsTracker.Instance != null)
        {
            RunStatsTracker.Instance.OnCurrencyChanged -= HandleCurrencyChanged;
        }
    }

    private void Update()
    {
        // Fallback kiểm tra đồng bộ liên tục để số Coin luôn chính xác tuyệt đối
        if (RunStatsTracker.Instance != null && RunStatsTracker.Instance.CurrencyEarned != lastCoinCount)
        {
            HandleCurrencyChanged(RunStatsTracker.Instance.CurrencyEarned);
        }
    }

    private void HandleCurrencyChanged(int newCoinCount)
    {
        bool hasIncreased = newCoinCount > lastCoinCount;
        UpdateCoinDisplay(newCoinCount, hasIncreased);
    }

    public void UpdateCoinDisplay(int amount, bool playPunchEffect = true)
    {
        lastCoinCount = amount;
        if (coinText != null)
        {
            coinText.text = amount.ToString();
        }

        if (playPunchEffect && gameObject.activeInHierarchy)
        {
            if (punchCoroutine != null) StopCoroutine(punchCoroutine);
            punchCoroutine = StartCoroutine(PunchEffectRoutine());
        }
    }

    public void SetVisible(bool visible)
    {
        if (gameObject != null)
        {
            gameObject.SetActive(visible);
        }
    }

    private IEnumerator PunchEffectRoutine()
    {
        if (containerRect == null) yield break;

        Vector3 originalScale = Vector3.one;
        Vector3 targetScale = new Vector3(1.15f, 1.15f, 1f);

        float duration = 0.16f;
        float elapsed = 0f;

        // Phóng to nhẹ tạo cảm giác nảy khi nhận tiền
        while (elapsed < duration * 0.4f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / (duration * 0.4f);
            containerRect.localScale = Vector3.Lerp(originalScale, targetScale, t);
            yield return null;
        }

        elapsed = 0f;
        // Thu về kích thước ban đầu
        while (elapsed < duration * 0.6f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / (duration * 0.6f);
            containerRect.localScale = Vector3.Lerp(targetScale, originalScale, t);
            yield return null;
        }

        containerRect.localScale = originalScale;
    }

    /// <summary>
    /// Căn chỉnh vị trí InGameCoinUI luôn bám sát cạnh trái của Minimap
    /// </summary>
    public void AlignWithMinimap()
    {
        GameObject minimapObj = GameObject.Find("MinimapWindow");
        RectTransform rect = GetComponent<RectTransform>();
        if (rect == null) return;

        rect.anchorMin = new Vector2(1, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 1);

        if (minimapObj != null)
        {
            RectTransform mapRect = minimapObj.GetComponent<RectTransform>();
            if (mapRect != null)
            {
                // Canh ngay cạnh trái MinimapWindow, cách 10px, đỉnh thẳng hàng với Minimap
                float leftOfMap = mapRect.anchoredPosition.x - mapRect.sizeDelta.x - 10f;
                rect.anchoredPosition = new Vector2(leftOfMap, mapRect.anchoredPosition.y);
                return;
            }
        }

        // Vị trí mặc định nếu chưa thấy MinimapWindow (-295f = -45f - 240f - 10f)
        rect.anchoredPosition = new Vector2(-295f, -55f);

        ApplyTransparentStyle();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadCallback()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (scene.name == "SampleScene")
            {
                EnsureCoinUIExists();
            }
        };
    }

    /// <summary>
    /// Đảm bảo InGameCoinUI luôn được tự động tạo và hiển thị trong màn chơi
    /// </summary>
    public static void EnsureCoinUIExists()
    {
        if (Instance != null && Instance.gameObject != null)
        {
            Instance.AlignWithMinimap();
            return;
        }

        GameObject existingObj = GameObject.Find("InGameCoinHUD");
        if (existingObj != null)
        {
            Instance = existingObj.GetComponent<InGameCoinUI>();
            if (Instance != null)
            {
                Instance.AlignWithMinimap();
                return;
            }
        }

        CreateAutoCoinUI();
    }

    private static void CreateAutoCoinUI()
    {
        Canvas canvas = FindUICanvas();
        if (canvas == null)
        {
            Debug.LogWarning("[InGameCoinUI] Không tìm thấy Canvas để tạo Coin UI.");
            return;
        }

        Transform parentTransform = canvas.transform;
        HUDManager hud = UnityEngine.Object.FindFirstObjectByType<HUDManager>();
        if (hud != null)
        {
            parentTransform = hud.transform;
        }

        // 1. Root GameObject
        GameObject coinObj = new GameObject("InGameCoinHUD", typeof(RectTransform));
        coinObj.transform.SetParent(parentTransform, false);

        RectTransform rootRect = coinObj.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(1, 1);
        rootRect.anchorMax = new Vector2(1, 1);
        rootRect.pivot = new Vector2(1, 1);
        rootRect.sizeDelta = new Vector2(85f, 32f);
        rootRect.anchoredPosition = new Vector2(-295f, -55f);

        // Nền hoàn toàn trong suốt theo yêu cầu
        Image bg = coinObj.AddComponent<Image>();
        bg.color = Color.clear;
        bg.enabled = false;
        bg.raycastTarget = false;

        // 2. Icon Coin
        GameObject iconObj = new GameObject("CoinIcon", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(coinObj.transform, false);

        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.sizeDelta = new Vector2(24f, 24f);
        iconRect.anchoredPosition = new Vector2(0f, 0f);

        Image iconImg = iconObj.GetComponent<Image>();
        iconImg.sprite = LoadCoinSpriteSafely();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        // 3. Text hiển thị số Coin
        GameObject textObj = new GameObject("CoinAmountText", typeof(RectTransform));
        textObj.transform.SetParent(coinObj.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = new Vector2(28f, 0f);
        textRect.offsetMax = new Vector2(0f, 0f);

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/Symtext SDF");
        if (font != null) tmp.font = font;

        tmp.text = "0";
        tmp.fontSize = 20f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.color = new Color(1f, 0.88f, 0.25f, 1f); // Màu vàng Gold sáng
        tmp.raycastTarget = false;

        // 4. Gắn Component InGameCoinUI
        InGameCoinUI ui = coinObj.AddComponent<InGameCoinUI>();
        ui.containerRect = rootRect;
        ui.coinIconImage = iconImg;
        ui.coinText = tmp;

        ui.AlignWithMinimap();

        if (RunStatsTracker.Instance != null)
        {
            ui.UpdateCoinDisplay(RunStatsTracker.Instance.CurrencyEarned, false);
        }

        Debug.Log("[InGameCoinUI] Đã tự động tạo In-Game Coin HUD kế bên Minimap.");
    }

    private static Sprite LoadCoinSpriteSafely()
    {
        Sprite s = Resources.Load<Sprite>("BuffIcons/coin_icon");
        if (s != null) return s;

        s = Resources.Load<Sprite>("BuffIcons/CoinMultiplier");
        if (s != null) return s;

#if UNITY_EDITOR
        s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Item/Coin/Coin.png");
        if (s != null) return s;
#endif
        return null;
    }

    private static Canvas FindUICanvas()
    {
        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas c in canvases)
        {
            if (c.renderMode != RenderMode.WorldSpace && c.gameObject.activeInHierarchy)
            {
                return c;
            }
        }
        return canvases.Length > 0 ? canvases[0] : null;
    }
}
