using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.Characters.Rookie
{
    /// <summary>
    /// Xử lý vùng Hồi sinh Đồng đội [E] khi có một thành viên gục ngã trong Co-op.
    /// Giữ phím E trong 2.5s để hồi sinh đồng đội.
    /// </summary>
    public class TeammateReviveArea : MonoBehaviour
    {
        public static TeammateReviveArea Instance { get; private set; }

        [Header("Cấu hình Hồi Sinh")]
        public float reviveHoldDuration = 2.5f; // Thời gian giữ phím E
        public float reviveRadius = 3.5f;       // Khoảng cách hồi sinh tối đa (bán kính 3.5m)

        private float currentReviveProgress = 0f;
        private bool isReviving = false;
        private string targetReviveConnId = "";

        [Header("Giao Diện UI Hồi Sinh (Gán từ Inspector)")]
        public GameObject reviveCanvasObj; // Root Canvas / Panel chứa UI Hồi Sinh
        public Slider reviveSlider;       // Component UI Slider Hồi sinh
        public Image progressBarFill;     // Lớp Image Fill tiến trình
        public Text progressText;         // Dòng chữ hướng dẫn "Giữ [E] 2.5s..."

        [Header("Tùy Chỉnh Sprites Slider (Tùy chọn)")]
        public Sprite sliderBackgroundSprite; // Sprite làm hình nền Slider Background
        public Sprite sliderFillSprite;       // Sprite làm hình thanh Fill Slider

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void Start()
        {
            if (reviveCanvasObj == null)
            {
                CreateFallbackReviveUI();
            }
            else
            {
                reviveCanvasObj.SetActive(false);
            }
        }

        private void CreateFallbackReviveUI()
        {
            if (reviveCanvasObj != null) return;

            reviveCanvasObj = new GameObject("ReviveProgressCanvas");
            reviveCanvasObj.transform.SetParent(transform);

            Canvas canvas = reviveCanvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = reviveCanvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // Nền & Component Slider
            GameObject sliderObj = new GameObject("ReviveSlider");
            sliderObj.transform.SetParent(reviveCanvasObj.transform, false);
            RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
            sliderRect.sizeDelta = new Vector2(400, 50);
            sliderRect.anchoredPosition = new Vector2(0, -250);

            reviveSlider = sliderObj.AddComponent<Slider>();
            reviveSlider.interactable = false;
            reviveSlider.transition = Selectable.Transition.None;
            reviveSlider.minValue = 0f;
            reviveSlider.maxValue = 1f;
            reviveSlider.value = 0f;

            // Background Image
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(sliderObj.transform, false);
            Image bgImg = bgObj.AddComponent<Image>();
            if (sliderBackgroundSprite != null)
            {
                bgImg.sprite = sliderBackgroundSprite;
                bgImg.type = Image.Type.Sliced;
            }
            else
            {
                bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
            }
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            // Fill Area & Fill Image
            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.sizeDelta = Vector2.zero;

            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillArea.transform, false);
            progressBarFill = fillObj.AddComponent<Image>();
            if (sliderFillSprite != null)
            {
                progressBarFill.sprite = sliderFillSprite;
                progressBarFill.type = Image.Type.Sliced;
            }
            else
            {
                progressBarFill.color = new Color(0f, 0.9f, 0.4f, 0.95f);
            }
            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;

            reviveSlider.targetGraphic = bgImg;
            reviveSlider.fillRect = fillRect;

            // Chữ hướng dẫn
            GameObject textObj = new GameObject("ReviveProgressText");
            textObj.transform.SetParent(sliderObj.transform, false);
            progressText = textObj.AddComponent<Text>();
            progressText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            progressText.fontSize = 22;
            progressText.alignment = TextAnchor.MiddleCenter;
            progressText.color = Color.white;
            progressText.text = "Giữ [E] 2.5s để Hồi Sinh Đồng Đội";
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            reviveCanvasObj.SetActive(false);
        }

        private void Update()
        {
            // Chỉ hoạt động trong Co-op mode
            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsLoggedIn || string.IsNullOrEmpty(NetworkManager.Instance.CurrentRoomId))
            {
                if (reviveCanvasObj != null && reviveCanvasObj.activeSelf) reviveCanvasObj.SetActive(false);
                return;
            }

            // Nếu local player đã chết -> Không thể đi hồi sinh người khác
            GameObject localPlayer = GameObject.FindWithTag("Player");
            if (localPlayer == null) return;
            RookieHealth localHealth = localPlayer.GetComponent<RookieHealth>();
            if (localHealth != null && localHealth.isDead)
            {
                CancelRevive();
                return;
            }

            // Tìm remote player gục ngã gần nhất
            GameObject nearestDeadRemote = FindNearestDeadRemotePlayer(localPlayer.transform.position);

            if (nearestDeadRemote != null)
            {
                RemotePlayerController rpc = nearestDeadRemote.GetComponent<RemotePlayerController>();
                targetReviveConnId = (rpc != null) ? rpc.connectionId : "";

                if (reviveCanvasObj != null && !reviveCanvasObj.activeSelf && !isReviving)
                {
                    reviveCanvasObj.SetActive(true);
                    if (progressText != null) progressText.text = "Giữ [E] 2.5s để Hồi Sinh Đồng Đội";
                    if (reviveSlider != null) reviveSlider.value = 0f;
                    if (progressBarFill != null) progressBarFill.fillAmount = 0f;
                }

                // Người chơi giữ phím [E]
                if (Input.GetKey(KeyCode.E))
                {
                    isReviving = true;
                    currentReviveProgress += Time.deltaTime;
                    float progressRatio = Mathf.Clamp01(currentReviveProgress / reviveHoldDuration);

                    if (reviveSlider != null) reviveSlider.value = progressRatio;
                    if (progressBarFill != null) progressBarFill.fillAmount = progressRatio;
                    if (progressText != null) progressText.text = $"Đang Hồi Sinh Đồng Đội... ({progressRatio * 100f:F0}%)";

                    if (currentReviveProgress >= reviveHoldDuration)
                    {
                        // Hoàn tất hồi sinh!
                        CompleteRevive();
                    }
                }
                else
                {
                    // Thả phím E -> Reset tiến trình
                    if (isReviving)
                    {
                        currentReviveProgress = 0f;
                        isReviving = false;
                        if (reviveSlider != null) reviveSlider.value = 0f;
                        if (progressBarFill != null) progressBarFill.fillAmount = 0f;
                        if (progressText != null) progressText.text = "Giữ [E] 2.5s để Hồi Sinh Đồng Đội";
                    }
                }
            }
            else
            {
                CancelRevive();
            }
        }

        private GameObject FindNearestDeadRemotePlayer(Vector3 localPos)
        {
            if (MultiplayerSyncManager.Instance == null) return null;

            GameObject nearest = null;
            float minDistance = reviveRadius;

            foreach (var kv in MultiplayerSyncManager.Instance.remotePlayers)
            {
                GameObject remoteObj = kv.Value;
                if (remoteObj != null)
                {
                    RemotePlayerController rpc = remoteObj.GetComponent<RemotePlayerController>();
                    if (rpc != null && rpc.isDead)
                    {
                        float dist = Vector3.Distance(localPos, remoteObj.transform.position);
                        if (dist <= minDistance)
                        {
                            minDistance = dist;
                            nearest = remoteObj;
                        }
                    }
                }
            }

            return nearest;
        }

        private void CompleteRevive()
        {
            currentReviveProgress = 0f;
            isReviving = false;

            if (reviveCanvasObj != null) reviveCanvasObj.SetActive(false);

            if (!string.IsNullOrEmpty(targetReviveConnId) && NetworkManager.Instance != null)
            {
                int reviveHp = 50; // Mặc định hồi 50 HP
                Debug.Log($"[TeammateReviveArea] Đã hoàn thành giữ [E] 2.5s! Gửi lệnh Hồi sinh cho đồng đội: {targetReviveConnId}");
                NetworkManager.Instance.SendPlayerRevive(targetReviveConnId, reviveHp);
            }
        }

        private void CancelRevive()
        {
            currentReviveProgress = 0f;
            isReviving = false;
            targetReviveConnId = "";
            if (reviveCanvasObj != null && reviveCanvasObj.activeSelf)
            {
                reviveCanvasObj.SetActive(false);
            }
        }
    }
}
