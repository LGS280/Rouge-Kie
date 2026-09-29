using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Gắn vào HUD_Canvas. Script tự tạo ô cooldown ở góc dưới bên phải màn hình.
public class DashCooldownUI : MonoBehaviour
{
    [Header("Nguồn dữ liệu")]
    [Tooltip("Để trống: tự tìm skill của người chơi trong scene.")]
    [SerializeField] private DashSlashSkill skill;
    [Tooltip("Canvas để đặt UI vào. Để trống: dùng Canvas chứa object này.")]
    [SerializeField] private Canvas targetCanvas;

    [Header("Vị trí & kích thước (neo góc dưới-phải)")]
    [Tooltip("X âm = dịch sang trái, Y dương = dịch lên trên.")]
    [SerializeField] private Vector2 anchoredPosition = new Vector2(-50f, 50f);
    [SerializeField] private float size = 90f;

    [Header("Giao diện")]
    [Tooltip("Icon skill (không bắt buộc). Để trống sẽ hiện chữ phím bấm.")]
    [SerializeField] private Sprite icon;
    // Hiển thị nhãn phím F tương ứng với phím kỹ năng của Zero
    [SerializeField] private string keyLabel = "F";
    [SerializeField] private Color readyColor = new Color(0.3f, 0.8f, 1f, 1f);
    [SerializeField] private Color cooldownColor = new Color(0.25f, 0.25f, 0.3f, 1f);
    [SerializeField] private Color backgroundColor = new Color(0.08f, 0.1f, 0.14f, 0.9f);
    [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.65f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private float ringThickness = 5f;

    [Header("Hiệu ứng khi hồi xong")]
    [SerializeField] private float popDuration = 0.25f;
    [SerializeField] private float popScale = 0.25f;

    private RectTransform root;
    private Image ring;
    private Image iconImage;
    private Image overlay;
    private Text label;

    private bool wasCooling;
    private float popTimer;
    private float findTimer;

    private void Start()
    {
        if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>();
        if (targetCanvas == null) targetCanvas = FindFirstObjectByType<Canvas>();
        if (targetCanvas == null)
        {
            Debug.LogWarning("[DashCooldownUI] Không tìm thấy Canvas nào.");
            enabled = false;
            return;
        }

        BuildUI();
    }

    private void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
    }

    private void Update()
    {
        if (root == null) return;

        if (skill == null)
        {
            root.gameObject.SetActive(false);
            findTimer -= Time.unscaledDeltaTime;
            if (findTimer <= 0f)
            {
                findTimer = 0.5f;
                FindSkill();
            }
            if (skill == null) return;
        }

        root.gameObject.SetActive(true);

        float remaining = skill.CooldownRemaining;
        bool cooling = remaining > 0f;

        // Vòng quét đen: đầy lúc vừa dùng, vơi dần về 0
        overlay.fillAmount = cooling ? remaining / Mathf.Max(0.01f, skill.CooldownDuration) : 0f;
        ring.color = cooling ? cooldownColor : readyColor;

        if (cooling)
        {
            label.text = remaining >= 1f
                ? Mathf.CeilToInt(remaining).ToString()
                : remaining.ToString("0.0");
        }
        else
        {
            label.text = iconImage.enabled ? "" : keyLabel;
        }

        // Phồng nhẹ một nhịp khi vừa hồi xong
        if (wasCooling && !cooling) popTimer = popDuration;
        wasCooling = cooling;

        if (popTimer > 0f)
        {
            popTimer -= Time.unscaledDeltaTime;
            float t = 1f - Mathf.Clamp01(popTimer / popDuration);
            root.localScale = Vector3.one * (1f + popScale * Mathf.Sin(t * Mathf.PI));
        }
        else
        {
            root.localScale = Vector3.one;
        }
    }

    // Ưu tiên nhân vật đang có PlayerInput bật (người chơi local)
    private void FindSkill()
    {
        DashSlashSkill[] all = FindObjectsByType<DashSlashSkill>(FindObjectsSortMode.None);
        DashSlashSkill fallback = null;

        foreach (DashSlashSkill s in all)
        {
            if (fallback == null) fallback = s;

            PlayerInput pi = s.GetComponent<PlayerInput>();
            if (pi != null && pi.enabled)
            {
                skill = s;
                return;
            }
        }

        skill = fallback;
    }

    private void BuildUI()
    {
        Sprite circle = CreateCircleSprite();

        GameObject rootGo = new GameObject("DashCooldownUI", typeof(RectTransform));
        root = rootGo.GetComponent<RectTransform>();
        root.SetParent(targetCanvas.transform, false);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(1f, 0f);
        root.sizeDelta = new Vector2(size, size);
        root.anchoredPosition = anchoredPosition;

        // Vòng viền (đổi màu: xanh = sẵn sàng, xám = đang hồi)
        ring = CreateImage("Ring", root, circle, readyColor, 0f);
        // Nền tối bên trong
        CreateImage("Background", root, circle, backgroundColor, ringThickness);
        // Icon skill (nếu có)
        iconImage = CreateImage("Icon", root, icon, Color.white, ringThickness + 6f);
        iconImage.enabled = icon != null;
        iconImage.preserveAspect = true;
        // Lớp quét đen theo thời gian hồi
        overlay = CreateImage("CooldownOverlay", root, circle, overlayColor, ringThickness);
        overlay.type = Image.Type.Filled;
        overlay.fillMethod = Image.FillMethod.Radial360;
        overlay.fillOrigin = (int)Image.Origin360.Top;
        overlay.fillClockwise = false;
        overlay.fillAmount = 0f;

        // Chữ ở giữa
        GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.SetParent(root, false);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = textRt.offsetMax = Vector2.zero;

        label = textGo.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = Mathf.RoundToInt(size * 0.38f);
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.color = textColor;
        label.raycastTarget = false;
        label.text = keyLabel;

        Outline outline = textGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        root.SetAsLastSibling();
        root.gameObject.SetActive(false); // hiện khi tìm thấy skill
    }

    private Image CreateImage(string objName, Transform parent, Sprite sprite, Color color, float inset)
    {
        GameObject go = new GameObject(objName, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);

        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // Tạo sprite hình tròn bằng code, không cần asset
    private static Sprite CreateCircleSprite(int res = 128)
    {
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        float radius = res / 2f;
        Vector2 center = new Vector2((res - 1) / 2f, (res - 1) / 2f);

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(radius - dist);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), 100f);
    }
}