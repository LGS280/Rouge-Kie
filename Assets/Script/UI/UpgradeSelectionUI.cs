using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UpgradeSelectionUI : MonoBehaviour
{
    private static UpgradeSelectionUI _instance;
    public static UpgradeSelectionUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<UpgradeSelectionUI>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject("UpgradeSelectionUI");
                    _instance = obj.AddComponent<UpgradeSelectionUI>();
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    public void OpenUpgradeMenu(Action onComplete)
    {
        if (GameConfigManager.Instance == null || GameConfigManager.Instance.BuffDb == null || GameConfigManager.Instance.BuffDb.Count == 0)
        {
            Debug.LogWarning("[UpgradeSelectionUI] BuffDb trống hoặc GameConfigManager chưa sẵn sàng. Chuyển tầng trực tiếp.");
            onComplete?.Invoke();
            return;
        }

        // Tạm dừng trò chơi
        Time.timeScale = 0f;

        // Chọn ngẫu nhiên 3 Buff từ DB
        List<BuffConfig> availableBuffs = new List<BuffConfig>(GameConfigManager.Instance.BuffDb);
        List<BuffConfig> selectedBuffs = new List<BuffConfig>();

        // Chọn ngẫu nhiên không trùng lặp
        int countToSelect = Mathf.Min(3, availableBuffs.Count);
        for (int i = 0; i < countToSelect; i++)
        {
            int index = UnityEngine.Random.Range(0, availableBuffs.Count);
            selectedBuffs.Add(availableBuffs[index]);
            availableBuffs.RemoveAt(index);
        }

        // Tạo Canvas động
        GameObject canvasObj = new GameObject("UpgradeSelectionCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // Đảm bảo EventSystem tồn tại để bắt click chuột
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        // Tạo Background Overlay mờ tối
        GameObject bgObj = new GameObject("BackgroundOverlay");
        bgObj.transform.SetParent(canvasObj.transform, false);
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.05f, 0.05f, 0.07f, 0.85f); // Màu tối huyền bí

        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // Tiêu đề
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(canvasObj.transform, false);
        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = "SELECT AN UPGRADE";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 42;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(0.9f, 0.9f, 0.95f);
        titleText.fontStyle = FontStyle.Bold;

        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.8f);
        titleRect.anchorMax = new Vector2(0.5f, 0.8f);
        titleRect.sizeDelta = new Vector2(600, 80);
        titleRect.anchoredPosition = Vector2.zero;

        // Container chứa 3 thẻ
        GameObject containerObj = new GameObject("CardsContainer");
        containerObj.transform.SetParent(canvasObj.transform, false);

        RectTransform containerRect = containerObj.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.45f);
        containerRect.anchorMax = new Vector2(0.5f, 0.45f);
        containerRect.sizeDelta = new Vector2(900, 360);
        containerRect.anchoredPosition = Vector2.zero;

        // Spawn 3 thẻ
        float cardWidth = 260f;
        float cardHeight = 340f;
        float spacing = 40f;
        float startX = -((cardWidth * selectedBuffs.Count) + (spacing * (selectedBuffs.Count - 1))) / 2f + cardWidth / 2f;

        for (int i = 0; i < selectedBuffs.Count; i++)
        {
            var buff = selectedBuffs[i];
            float posX = startX + i * (cardWidth + spacing);

            // Thẻ Card
            GameObject cardObj = new GameObject($"Card_{i}");
            cardObj.transform.SetParent(containerObj.transform, false);
            
            Image cardImage = cardObj.AddComponent<Image>();
            cardImage.color = new Color(0.12f, 0.12f, 0.18f, 1f); // Sleek dark panel

            // Thêm hiệu ứng viền sáng (Outline)
            Outline outline = cardObj.AddComponent<Outline>();
            outline.effectColor = GetColorForBuffType(buff.buffType);
            outline.effectDistance = new Vector2(2, 2);

            RectTransform cardRect = cardObj.GetComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(cardWidth, cardHeight);
            cardRect.anchoredPosition = new Vector2(posX, 0f);

            // Button sự kiện click
            Button cardButton = cardObj.AddComponent<Button>();
            cardButton.onClick.AddListener(() =>
            {
                // Áp dụng buff
                if (PlayerBuffManager.Instance != null)
                {
                    PlayerBuffManager.Instance.ApplyBuff(buff);
                }

                // Hủy UI & Tiếp tục
                Destroy(canvasObj);
                Time.timeScale = 1f;
                onComplete?.Invoke();
            });

            // Thêm hiệu ứng di chuột (Micro-animation)
            cardObj.AddComponent<CardHoverEffect>();

            // Icon hoặc Ký hiệu chữ
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(cardObj.transform, false);
            Text iconText = iconObj.AddComponent<Text>();
            iconText.text = GetSymbolForBuffType(buff.buffType);
            iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            iconText.fontSize = 55;
            iconText.alignment = TextAnchor.MiddleCenter;
            iconText.color = GetColorForBuffType(buff.buffType);

            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.75f);
            iconRect.anchorMax = new Vector2(0.5f, 0.75f);
            iconRect.sizeDelta = new Vector2(100, 100);
            iconRect.anchoredPosition = Vector2.zero;

            // Tên Buff
            GameObject nameObj = new GameObject("BuffName");
            nameObj.transform.SetParent(cardObj.transform, false);
            Text nameText = nameObj.AddComponent<Text>();
            nameText.text = buff.buffName;
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontSize = 24;
            nameText.fontStyle = FontStyle.Bold;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = Color.white;

            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.5f, 0.52f);
            nameRect.anchorMax = new Vector2(0.5f, 0.52f);
            nameRect.sizeDelta = new Vector2(240, 40);
            nameRect.anchoredPosition = Vector2.zero;

            // Độ hiếm (Rarity)
            GameObject rarityObj = new GameObject("Rarity");
            rarityObj.transform.SetParent(cardObj.transform, false);
            Text rarityText = rarityObj.AddComponent<Text>();
            rarityText.text = buff.rarity.ToUpper();
            rarityText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rarityText.fontSize = 14;
            rarityText.alignment = TextAnchor.MiddleCenter;
            rarityText.color = new Color(0.7f, 0.7f, 0.75f);

            RectTransform rarityRect = rarityObj.GetComponent<RectTransform>();
            rarityRect.anchorMin = new Vector2(0.5f, 0.42f);
            rarityRect.anchorMax = new Vector2(0.5f, 0.42f);
            rarityRect.sizeDelta = new Vector2(240, 20);
            rarityRect.anchoredPosition = Vector2.zero;

            // Mô tả
            GameObject descObj = new GameObject("Description");
            descObj.transform.SetParent(cardObj.transform, false);
            Text descText = descObj.AddComponent<Text>();
            descText.text = buff.description;
            descText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            descText.fontSize = 16;
            descText.alignment = TextAnchor.MiddleCenter;
            descText.color = new Color(0.8f, 0.8f, 0.85f);

            RectTransform descRect = descObj.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0.5f, 0.22f);
            descRect.anchorMax = new Vector2(0.5f, 0.22f);
            descRect.sizeDelta = new Vector2(220, 80);
            descRect.anchoredPosition = Vector2.zero;
        }
    }

    private Color GetColorForBuffType(string type)
    {
        switch (type)
        {
            case "MaxHP": return new Color(1f, 0.25f, 0.25f);       // Đỏ tươi
            case "MaxArmor": return new Color(0.3f, 0.65f, 1f);     // Xanh lam
            case "MaxMana": return new Color(0.7f, 0.3f, 1f);       // Tím phép
            case "MoveSpeed": return new Color(0.25f, 0.9f, 0.6f);   // Xanh lục tốc chạy
            case "Damage": return new Color(1f, 0.5f, 0.1f);         // Cam sát thương
            case "CritChance": return new Color(1f, 0.8f, 0f);       // Vàng chí mạng
            case "FireRate": return new Color(0.9f, 0.2f, 0.5f);     // Hồng cánh sen
            case "CoinMultiplier": return new Color(1f, 0.85f, 0.3f); // Vàng kim tiền
            default: return Color.gray;
        }
    }

    private string GetSymbolForBuffType(string type)
    {
        switch (type)
        {
            case "MaxHP": return "💖";
            case "MaxArmor": return "🛡️";
            case "MaxMana": return "🧪";
            case "MoveSpeed": return "👟";
            case "Damage": return "⚔️";
            case "CritChance": return "🎯";
            case "FireRate": return "⚡";
            case "CoinMultiplier": return "💰";
            default: return "🌀";
        }
    }
}

// Lớp bổ trợ hiệu ứng di chuột co giãn (Hover Effect)
public class CardHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Vector3 originalScale;
    private Outline outline;

    private void Start()
    {
        originalScale = transform.localScale;
        outline = GetComponent<Outline>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Phóng to nhẹ và tăng sáng outline
        transform.localScale = originalScale * 1.05f;
        if (outline != null)
        {
            outline.effectDistance = new Vector2(4, 4);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Trả lại tỷ lệ gốc
        transform.localScale = originalScale;
        if (outline != null)
        {
            outline.effectDistance = new Vector2(2, 2);
        }
    }

    private void OnDisable()
    {
        // Reset scale tránh lỗi khi bị huỷ
        transform.localScale = originalScale;
    }
}
