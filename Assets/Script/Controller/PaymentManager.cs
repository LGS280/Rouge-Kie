using System;
using System.Collections;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class PaymentManager : MonoBehaviour
{
    private static PaymentManager instance;
    public static PaymentManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = UnityEngine.Object.FindFirstObjectByType<PaymentManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("PaymentManager");
                    instance = go.AddComponent<PaymentManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    [Header("UI Payment QR Modal (Optional)")]
    public GameObject qrModalPanel;
    public RawImage qrImageDisplay;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI amountText;
    public TextMeshProUGUI orderCodeText;
    public Button closeButton;
    public Button openBrowserButton;
    public Button devSimulateSuccessButton;
    public string pendingBoughtWeaponPrefab = "";

    private Coroutine pollingCoroutine;
    private string currentPaymentUrl = "";
    private long currentOrderCode = 0;
    private bool isModalOpen = false;

    [Serializable]
    public class CreatePaymentRequestData
    {
        public int? shopItemId;
        public int amount;
        public string description;
        public string currencyType;
    }

    [Serializable]
    public class PaymentResponseData
    {
        public int transactionId;
        public long orderCode;
        public int amount;
        public string description;
        public string paymentUrl;
        public string qrCodeUrl;
        public string status;
        public string currencyType;
        public string createdAt;
        public string paidAt;
    }

    private const string PAYOS_CLIENT_ID = "e9157d63-27cc-4afe-bf60-b4c48dad5786";
    private const string PAYOS_API_KEY = "091a6e8e-a064-4ada-ac74-8c571db289e3";
    private const string PAYOS_CHECKSUM_KEY = "b3923181eb25e58c09b5835e3ed7ac4a6a427c602b5348d463177f1b9abb3463";

    [Serializable]
    public class PayOSCreateOrderRequest
    {
        public long orderCode;
        public int amount;
        public string description;
        public string cancelUrl;
        public string returnUrl;
        public string signature;
    }

    [Serializable]
    public class PayOSCreateOrderResponse
    {
        public string code;
        public string desc;
        public PayOSOrderData data;
    }

    [Serializable]
    public class PayOSOrderData
    {
        public string bin;
        public string accountNumber;
        public string accountName;
        public int amount;
        public string description;
        public long orderCode;
        public string currency;
        public string paymentLinkId;
        public string status;
        public string checkoutUrl;
        public string qrCode;
    }

    [Serializable]
    public class PayOSGetStatusResponse
    {
        public string code;
        public string desc;
        public PayOSStatusData data;
    }

    [Serializable]
    public class PayOSStatusData
    {
        public long orderCode;
        public int amount;
        public string status;
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseQRModal);
        }

        if (openBrowserButton != null)
        {
            openBrowserButton.onClick.RemoveAllListeners();
            openBrowserButton.onClick.AddListener(OpenPaymentInBrowser);
        }

        if (devSimulateSuccessButton != null)
        {
            devSimulateSuccessButton.onClick.RemoveAllListeners();
            devSimulateSuccessButton.onClick.AddListener(TriggerDevSimulateSuccess);
        }

        // Không bao giờ tự động tắt nếu Modal đang trong trạng thái được kích hoạt mở
        if (qrModalPanel != null && !isModalOpen)
        {
            qrModalPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Tạo yêu cầu nạp tiền / mua gói Gem qua PayOS VietQR
    /// </summary>
    public void RequestPayment(int amount, string description = "Nap Gem RogueKie", string currencyType = "GEMS", Action<PaymentResponseData> onSuccess = null, Action<string> onError = null)
    {
        string token = PlayerPrefs.GetString("jwt_token", "");

        // 1. Nếu đã đăng nhập: Gọi Backend API để tạo Transaction trong DB và nhận mã QR PayOS chuẩn
        if (!string.IsNullOrEmpty(token))
        {
            var req = new CreatePaymentRequestData
            {
                amount = amount,
                description = description,
                currencyType = currencyType
            };

            string json = JsonUtility.ToJson(req);
            if (statusText != null) statusText.text = "Đang kết nối Backend tạo mã VietQR...";

            ApiClient.Instance.Post("/Payment/create-payment-link", json, (responseJson) =>
            {
                try
                {
                    PaymentResponseData res = JsonUtility.FromJson<PaymentResponseData>(responseJson);
                    currentPaymentUrl = res.paymentUrl;
                    currentOrderCode = res.orderCode;

                    Debug.Log($"[PaymentManager] Backend tạo đơn thành công! OrderCode: {res.orderCode}, URL: {res.paymentUrl}");
                    ShowQRModal(res);
                    onSuccess?.Invoke(res);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PaymentManager] Parse Backend response lỗi ({ex.Message}) -> Chuyển sang tạo đơn PayOS trực tiếp.");
                    StartCoroutine(CreatePayOSOrderDirectly(amount, description, onSuccess, onError));
                }
            }, (err) =>
            {
                Debug.LogWarning($"[PaymentManager] Backend API lỗi ({err}) -> Chuyển sang tạo đơn PayOS trực tiếp.");
                StartCoroutine(CreatePayOSOrderDirectly(amount, description, onSuccess, onError));
            });
        }
        else
        {
            // 2. Nếu chưa đăng nhập (Guest mode): Tạo đơn hàng trực tiếp với PayOS API
            StartCoroutine(CreatePayOSOrderDirectly(amount, description, onSuccess, onError));
        }
    }

    /// <summary>
    /// Tạo đơn hàng THẬT trực tiếp với PayOS API (đảm bảo mã QR sử dụng đúng tài khoản ảo VQRQ và khớp 100% với Web)
    /// </summary>
    private IEnumerator CreatePayOSOrderDirectly(int amount, string description, Action<PaymentResponseData> onSuccess, Action<string> onError)
    {
        if (statusText != null) statusText.text = "Đang kết nối cổng PayOS tạo mã VietQR...";

        long orderCode = (long)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 100000000);
        if (orderCode < 100000) orderCode += 100000;

        string cleanDesc = SanitizePayOSDescription(description);
        string cancelUrl = "https://roguekie.com/cancel";
        string returnUrl = "https://roguekie.com/success";

        string signatureData = $"amount={amount}&cancelUrl={cancelUrl}&description={cleanDesc}&orderCode={orderCode}&returnUrl={returnUrl}";
        string signature = ComputeHmacSha256(signatureData, PAYOS_CHECKSUM_KEY);

        PayOSCreateOrderRequest payload = new PayOSCreateOrderRequest
        {
            orderCode = orderCode,
            amount = amount,
            description = cleanDesc,
            cancelUrl = cancelUrl,
            returnUrl = returnUrl,
            signature = signature
        };

        string jsonPayload = JsonUtility.ToJson(payload);

        using (UnityWebRequest req = new UnityWebRequest("https://api-merchant.payos.vn/v2/payment-requests", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("x-client-id", PAYOS_CLIENT_ID);
            req.SetRequestHeader("x-api-key", PAYOS_API_KEY);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                string resJson = req.downloadHandler.text;
                Debug.Log($"[PaymentManager] PayOS API Response: {resJson}");

                PayOSCreateOrderResponse payosRes = JsonUtility.FromJson<PayOSCreateOrderResponse>(resJson);
                if (payosRes != null && (payosRes.code == "00" || payosRes.code == "0") && payosRes.data != null)
                {
                    string checkoutUrl = payosRes.data.checkoutUrl;
                    string accountNumber = payosRes.data.accountNumber;
                    string bin = payosRes.data.bin;
                    if (string.IsNullOrEmpty(bin)) bin = "970422";
                    string accountName = payosRes.data.accountName;
                    if (string.IsNullOrEmpty(accountName)) accountName = "TRAN VU QUOC DAI";
                    string rawQr = payosRes.data.qrCode;

                    // 1. Ưu tiên render ảnh QR từ chính chuỗi EMVCo chuẩn do PayOS sinh ra (chuẩn 100% như trên Web của PayOS)
                    // 2. Dự phòng bằng link VietQR image với đúng số tài khoản ảo PayOS (VQRQ...)
                    string vietQrUrl;
                    if (!string.IsNullOrEmpty(rawQr))
                    {
                        vietQrUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=400x400&data={Uri.EscapeDataString(rawQr)}";
                    }
                    else
                    {
                        vietQrUrl = $"https://img.vietqr.io/image/{bin}-{accountNumber}-compact2.jpg?amount={amount}&addInfo={Uri.EscapeDataString(cleanDesc)}&accountName={Uri.EscapeDataString(accountName)}";
                    }

                    string fallbackUrl = $"https://img.vietqr.io/image/{bin}-{accountNumber}-compact2.jpg?amount={amount}&addInfo={Uri.EscapeDataString(cleanDesc)}&accountName={Uri.EscapeDataString(accountName)}";

                    PaymentResponseData resData = new PaymentResponseData
                    {
                        orderCode = orderCode,
                        amount = amount,
                        description = cleanDesc,
                        paymentUrl = checkoutUrl,
                        qrCodeUrl = vietQrUrl,
                        status = "PENDING"
                    };

                    currentPaymentUrl = checkoutUrl;
                    currentOrderCode = orderCode;

                    ShowQRModal(resData, fallbackUrl);
                    onSuccess?.Invoke(resData);
                    yield break;
                }
            }

            Debug.LogWarning($"[PaymentManager] PayOS direct API error: {req.error} - {req.downloadHandler?.text}");
            onError?.Invoke(req.error);
        }
    }

    private static string ComputeHmacSha256(string data, string key)
    {
        using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
        {
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            var sb = new StringBuilder();
            foreach (byte b in hash)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }
    }

    private static string SanitizePayOSDescription(string text)
    {
        if (string.IsNullOrEmpty(text)) return "Nap Gem RogueKie";
        string normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (char c in normalized)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        string clean = Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC), @"[^a-zA-Z0-9 ]", "").Trim();
        if (string.IsNullOrEmpty(clean)) clean = "Nap Gem RogueKie";
        if (clean.Length > 25) clean = clean.Substring(0, 25);
        return clean;
    }

    /// <summary>
    /// Hiển thị Modal VietQR và nạp ảnh QR Code từ URL
    /// </summary>
    public void ShowQRModal(PaymentResponseData res, string fallbackQrUrl = "")
    {
        isModalOpen = true;

        if (qrModalPanel == null)
        {
            BuildAutoQRUI();
        }

        GameObject canvasObj = GameObject.Find("PaymentUICanvas");
        if (canvasObj != null)
        {
            canvasObj.SetActive(true);
        }

        if (qrModalPanel != null)
        {
            qrModalPanel.SetActive(true);
            qrModalPanel.transform.SetAsLastSibling();
        }

        if (amountText != null) amountText.text = $"Amount: {res.amount:N0} VND";
        if (orderCodeText != null) orderCodeText.text = $"Order: #{res.orderCode}";
        if (statusText != null) statusText.text = "Scan VietQR with Banking App / MoMo...";

        if (qrImageDisplay != null)
        {
            qrImageDisplay.color = Color.clear;
            if (!string.IsNullOrEmpty(res.qrCodeUrl))
            {
                StartCoroutine(DownloadQRTexture(res.qrCodeUrl, fallbackQrUrl));
            }
        }

        // Bắt đầu luồng kiểm tra (Polling) tự động mỗi 2.5 giây
        if (pollingCoroutine != null) StopCoroutine(pollingCoroutine);
        pollingCoroutine = StartCoroutine(PollPaymentStatusRoutine(res.orderCode));
    }

    private IEnumerator DownloadQRTexture(string url, string fallbackUrl = "")
    {
        using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
        {
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(www);
                if (qrImageDisplay != null)
                {
                    qrImageDisplay.texture = texture;
                    qrImageDisplay.color = Color.white;
                }
            }
            else if (!string.IsNullOrEmpty(fallbackUrl) && url != fallbackUrl)
            {
                Debug.LogWarning($"[PaymentManager] Không tải được từ primary URL ({www.error}), thử fallback: {fallbackUrl}");
                yield return StartCoroutine(DownloadQRTexture(fallbackUrl));
            }
            else
            {
                Debug.LogWarning($"[PaymentManager] Không thể tải ảnh VietQR: {www.error}");
                if (statusText != null)
                {
                    statusText.text = "<color=yellow>Failed to load QR code. Please close and try again!</color>";
                }
            }
        }
    }

    private IEnumerator PollPaymentStatusRoutine(long orderCode)
    {
        while (true)
        {
            yield return new WaitForSeconds(2.5f);

            // 1. Kiểm tra trạng thái đơn hàng trực tiếp từ PayOS Server
            StartCoroutine(CheckPayOSStatusDirectly(orderCode));

            // 2. Nếu có token, kiểm tra thêm qua Backend API
            string token = PlayerPrefs.GetString("jwt_token", "");
            if (!string.IsNullOrEmpty(token))
            {
                ApiClient.Instance.Get($"/Payment/check-status/{orderCode}", (responseJson) =>
                {
                    try
                    {
                        PaymentResponseData res = JsonUtility.FromJson<PaymentResponseData>(responseJson);
                        if (res.status == "PAID")
                        {
                            OnPaymentCompletedSuccessfully(orderCode);
                        }
                    }
                    catch { }
                }, (err) => { });
            }
        }
    }

    private IEnumerator CheckPayOSStatusDirectly(long orderCode)
    {
        string url = $"https://api-merchant.payos.vn/v2/payment-requests/{orderCode}";
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.SetRequestHeader("x-client-id", PAYOS_CLIENT_ID);
            req.SetRequestHeader("x-api-key", PAYOS_API_KEY);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                string json = req.downloadHandler.text;
                PayOSGetStatusResponse resp = JsonUtility.FromJson<PayOSGetStatusResponse>(json);
                if (resp != null && resp.data != null && resp.data.status == "PAID")
                {
                    OnPaymentCompletedSuccessfully(orderCode);
                }
            }
        }
    }

    private void OnPaymentCompletedSuccessfully(long orderCode)
    {
        Debug.Log($"[PaymentManager] Giao dịch OrderCode {orderCode} đã được THANH TOÁN thành công!");
        if (statusText != null) statusText.text = "<color=green>Payment Successful! Weapon spawned on table!</color>";

        // Gọi Backend để đồng bộ trạng thái đơn hàng sang PAID trong Database
        string token = PlayerPrefs.GetString("jwt_token", "");
        if (!string.IsNullOrEmpty(token))
        {
            ApiClient.Instance.Get($"/Payment/check-status/{orderCode}", null, null);
        }

        // Tự động làm mới UI Profile Gems/Coins người chơi
        if (PlayerProfileUI.Instance != null)
        {
            PlayerProfileUI.Instance.RefreshProfile();
        }

        // Tự động sinh súng vừa mua bằng VietQR lên Bàn Trưng Bày
        ShopUIController shop = ShopUIController.Instance;
        if (shop == null) shop = UnityEngine.Object.FindFirstObjectByType<ShopUIController>();

        if (!string.IsNullOrEmpty(pendingBoughtWeaponPrefab) && shop != null)
        {
            shop.SpawnBoughtWeaponOnTable(pendingBoughtWeaponPrefab);
            pendingBoughtWeaponPrefab = "";
        }

        // Dừng polling và tự động đóng modal sau 1.5s
        if (pollingCoroutine != null)
        {
            StopCoroutine(pollingCoroutine);
            pollingCoroutine = null;
        }
        Invoke(nameof(CloseQRModal), 1.5f);
    }

    public void OpenPaymentInBrowser()
    {
        if (!string.IsNullOrEmpty(currentPaymentUrl))
        {
            Application.OpenURL(currentPaymentUrl);
        }
    }

    public void TriggerDevSimulateSuccess()
    {
        if (statusText != null) statusText.text = "Simulating dev payment success...";

        long code = currentOrderCode > 0 ? currentOrderCode : 999999;
        OnPaymentCompletedSuccessfully(code);

        // Gọi Backend để đồng bộ nếu có đăng nhập
        string token = PlayerPrefs.GetString("jwt_token", "");
        if (!string.IsNullOrEmpty(token) && currentOrderCode > 0)
        {
            ApiClient.Instance.Post($"/Payment/dev-simulate-success/{currentOrderCode}", "{}", null, null);
        }
    }

    public void CloseQRModal()
    {
        isModalOpen = false;

        if (pollingCoroutine != null)
        {
            StopCoroutine(pollingCoroutine);
            pollingCoroutine = null;
        }

        if (qrModalPanel != null)
        {
            qrModalPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Tự động khởi tạo giao diện Popup QR thanh toán VietQR nếu chưa được kéo thả trong Inspector
    /// </summary>
    private void BuildAutoQRUI()
    {
        GameObject canvasObj = GameObject.Find("PaymentUICanvas");
        Canvas c;
        if (canvasObj == null)
        {
            canvasObj = new GameObject("PaymentUICanvas");
            c = canvasObj.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 200; // Đè lên trên ShopUICanvas (sortingOrder 100)

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasObj);
        }
        else
        {
            c = canvasObj.GetComponent<Canvas>();
            if (c != null)
            {
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 200;
            }
        }

        // 1. Panel nền tối che mờ màn hình (Dim overlay)
        qrModalPanel = new GameObject("QRModalPanel", typeof(RectTransform), typeof(Image));
        qrModalPanel.transform.SetParent(canvasObj.transform, false);

        RectTransform panelRect = qrModalPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.localScale = Vector3.one;

        Image panelImg = qrModalPanel.GetComponent<Image>();
        panelImg.color = new Color(0.01f, 0.02f, 0.05f, 0.88f);
        panelImg.raycastTarget = true;

        // 2. Khung cửa sổ Dialog chính (QRDialog)
        GameObject dialog = new GameObject("QRDialog", typeof(RectTransform), typeof(Image));
        dialog.transform.SetParent(qrModalPanel.transform, false);

        RectTransform dialogRect = dialog.GetComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.pivot = new Vector2(0.5f, 0.5f);
        dialogRect.anchoredPosition = Vector2.zero;
        dialogRect.sizeDelta = new Vector2(480, 580);
        dialogRect.localScale = Vector3.one;

        Image dialogImg = dialog.GetComponent<Image>();
        dialogImg.color = new Color(0.1f, 0.14f, 0.22f, 0.98f);

        // 3. Tiêu đề
        GameObject headerObj = new GameObject("HeaderTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        headerObj.transform.SetParent(dialog.transform, false);
        RectTransform headerRect = headerObj.GetComponent<RectTransform>();
        headerRect.anchoredPosition = new Vector2(0, 255);
        headerRect.sizeDelta = new Vector2(440, 36);
        headerRect.localScale = Vector3.one;

        TextMeshProUGUI headerTxt = headerObj.GetComponent<TextMeshProUGUI>();
        headerTxt.text = "VIETQR PAYMENT";
        headerTxt.fontSize = 22;
        headerTxt.color = new Color(1f, 0.85f, 0.3f);
        headerTxt.alignment = TextAlignmentOptions.Center;
        headerTxt.fontStyle = FontStyles.Bold;

        // Nút Đóng [X] góc trên bên phải
        GameObject closeObj = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        closeObj.transform.SetParent(dialog.transform, false);
        RectTransform closeRect = closeObj.GetComponent<RectTransform>();
        closeRect.anchoredPosition = new Vector2(205, 255);
        closeRect.sizeDelta = new Vector2(34, 34);
        closeRect.localScale = Vector3.one;

        Image closeImg = closeObj.GetComponent<Image>();
        closeImg.color = new Color(0.85f, 0.25f, 0.25f);

        GameObject closeTxtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        closeTxtObj.transform.SetParent(closeObj.transform, false);
        RectTransform closeTxtRect = closeTxtObj.GetComponent<RectTransform>();
        closeTxtRect.anchorMin = Vector2.zero;
        closeTxtRect.anchorMax = Vector2.one;
        closeTxtRect.offsetMin = Vector2.zero;
        closeTxtRect.offsetMax = Vector2.zero;
        closeTxtRect.localScale = Vector3.one;

        TextMeshProUGUI closeTxt = closeTxtObj.GetComponent<TextMeshProUGUI>();
        closeTxt.text = "X";
        closeTxt.fontSize = 18;
        closeTxt.color = Color.white;
        closeTxt.alignment = TextAlignmentOptions.Center;
        closeTxt.fontStyle = FontStyles.Bold;

        closeButton = closeObj.GetComponent<Button>();
        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(CloseQRModal);

        // 4. Mã đơn hàng & Số tiền
        GameObject orderObj = new GameObject("OrderCodeText", typeof(RectTransform), typeof(TextMeshProUGUI));
        orderObj.transform.SetParent(dialog.transform, false);
        RectTransform orderRect = orderObj.GetComponent<RectTransform>();
        orderRect.anchoredPosition = new Vector2(0, 218);
        orderRect.sizeDelta = new Vector2(440, 24);
        orderRect.localScale = Vector3.one;

        orderCodeText = orderObj.GetComponent<TextMeshProUGUI>();
        orderCodeText.text = "Order: #------";
        orderCodeText.fontSize = 15;
        orderCodeText.color = new Color(0.7f, 0.85f, 1f);
        orderCodeText.alignment = TextAlignmentOptions.Center;

        GameObject amountObj = new GameObject("AmountText", typeof(RectTransform), typeof(TextMeshProUGUI));
        amountObj.transform.SetParent(dialog.transform, false);
        RectTransform amountRect = amountObj.GetComponent<RectTransform>();
        amountRect.anchoredPosition = new Vector2(0, 190);
        amountRect.sizeDelta = new Vector2(440, 28);
        amountRect.localScale = Vector3.one;

        amountText = amountObj.GetComponent<TextMeshProUGUI>();
        amountText.text = "Amount: -- VND";
        amountText.fontSize = 20;
        amountText.color = new Color(0.35f, 0.95f, 0.45f);
        amountText.alignment = TextAlignmentOptions.Center;
        amountText.fontStyle = FontStyles.Bold;

        // 5. Thẻ nền trắng chứa ảnh mã QR
        GameObject qrBg = new GameObject("QRBackground", typeof(RectTransform), typeof(Image));
        qrBg.transform.SetParent(dialog.transform, false);
        RectTransform qrBgRect = qrBg.GetComponent<RectTransform>();
        qrBgRect.anchoredPosition = new Vector2(0, 45);
        qrBgRect.sizeDelta = new Vector2(240, 240);
        qrBgRect.localScale = Vector3.one;
        Image qrBgImg = qrBg.GetComponent<Image>();
        qrBgImg.color = Color.white;

        // Text loading tạm thời nếu ảnh QR chưa nạp xong
        GameObject qrLoadingObj = new GameObject("QRLoadingText", typeof(RectTransform), typeof(TextMeshProUGUI));
        qrLoadingObj.transform.SetParent(qrBg.transform, false);
        RectTransform qrLoadingRect = qrLoadingObj.GetComponent<RectTransform>();
        qrLoadingRect.anchorMin = Vector2.zero;
        qrLoadingRect.anchorMax = Vector2.one;
        qrLoadingRect.offsetMin = Vector2.zero;
        qrLoadingRect.offsetMax = Vector2.zero;
        qrLoadingRect.localScale = Vector3.one;
        TextMeshProUGUI qrLoadingTxt = qrLoadingObj.GetComponent<TextMeshProUGUI>();
        qrLoadingTxt.text = "Loading VietQR code...";
        qrLoadingTxt.fontSize = 15;
        qrLoadingTxt.color = new Color(0.3f, 0.3f, 0.3f);
        qrLoadingTxt.alignment = TextAlignmentOptions.Center;

        GameObject qrImgObj = new GameObject("QRImage", typeof(RectTransform), typeof(RawImage));
        qrImgObj.transform.SetParent(qrBg.transform, false);
        RectTransform qrImgRect = qrImgObj.GetComponent<RectTransform>();
        qrImgRect.anchorMin = Vector2.zero;
        qrImgRect.anchorMax = Vector2.one;
        qrImgRect.offsetMin = new Vector2(5, 5);
        qrImgRect.offsetMax = new Vector2(-5, -5);
        qrImgRect.localScale = Vector3.one;
        qrImageDisplay = qrImgObj.GetComponent<RawImage>();
        qrImageDisplay.color = Color.clear; // Ban đầu trong suốt cho đến khi nạp xong texture

        // 6. Dòng trạng thái (Status)
        GameObject statusObj = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
        statusObj.transform.SetParent(dialog.transform, false);
        RectTransform statusRect = statusObj.GetComponent<RectTransform>();
        statusRect.anchoredPosition = new Vector2(0, -98);
        statusRect.sizeDelta = new Vector2(460, 32);
        statusRect.localScale = Vector3.one;

        statusText = statusObj.GetComponent<TextMeshProUGUI>();
        statusText.text = "Scan VietQR with Banking App / MoMo...";
        statusText.fontSize = 15;
        statusText.color = new Color(0.9f, 0.9f, 0.9f);
        statusText.alignment = TextAlignmentOptions.Center;

        // 7. Nút Giả Lập Thanh Toán Thành Công (Dev Test)
        GameObject devBtnObj = new GameObject("DevSimulateButton", typeof(RectTransform), typeof(Image), typeof(Button));
        devBtnObj.transform.SetParent(dialog.transform, false);
        RectTransform devRect = devBtnObj.GetComponent<RectTransform>();
        devRect.anchoredPosition = new Vector2(0, -150);
        devRect.sizeDelta = new Vector2(380, 38);
        devRect.localScale = Vector3.one;

        Image devImg = devBtnObj.GetComponent<Image>();
        devImg.color = new Color(0.2f, 0.65f, 0.35f);

        GameObject devTxtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        devTxtObj.transform.SetParent(devBtnObj.transform, false);
        RectTransform devTxtRect = devTxtObj.GetComponent<RectTransform>();
        devTxtRect.anchorMin = Vector2.zero;
        devTxtRect.anchorMax = Vector2.one;
        devTxtRect.offsetMin = Vector2.zero;
        devTxtRect.offsetMax = Vector2.zero;
        devTxtRect.localScale = Vector3.one;

        TextMeshProUGUI devTxt = devTxtObj.GetComponent<TextMeshProUGUI>();
        devTxt.text = "SIMULATE PAYMENT (DEV TEST)";
        devTxt.fontSize = 14;
        devTxt.color = Color.white;
        devTxt.alignment = TextAlignmentOptions.Center;
        devTxt.fontStyle = FontStyles.Bold;

        devSimulateSuccessButton = devBtnObj.GetComponent<Button>();
        devSimulateSuccessButton.onClick.RemoveAllListeners();
        devSimulateSuccessButton.onClick.AddListener(TriggerDevSimulateSuccess);

        // 8. Nút Hủy / Đóng giao dịch phía dưới
        GameObject cancelBtnObj = new GameObject("CancelButton", typeof(RectTransform), typeof(Image), typeof(Button));
        cancelBtnObj.transform.SetParent(dialog.transform, false);
        RectTransform cancelRect = cancelBtnObj.GetComponent<RectTransform>();
        cancelRect.anchoredPosition = new Vector2(0, -200);
        cancelRect.sizeDelta = new Vector2(380, 38);
        cancelRect.localScale = Vector3.one;

        Image cancelImg = cancelBtnObj.GetComponent<Image>();
        cancelImg.color = new Color(0.35f, 0.38f, 0.45f);

        GameObject cancelTxtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        cancelTxtObj.transform.SetParent(cancelBtnObj.transform, false);
        RectTransform cancelTxtRect = cancelTxtObj.GetComponent<RectTransform>();
        cancelTxtRect.anchorMin = Vector2.zero;
        cancelTxtRect.anchorMax = Vector2.one;
        cancelTxtRect.offsetMin = Vector2.zero;
        cancelTxtRect.offsetMax = Vector2.zero;
        cancelTxtRect.localScale = Vector3.one;

        TextMeshProUGUI cancelTxt = cancelTxtObj.GetComponent<TextMeshProUGUI>();
        cancelTxt.text = "CLOSE / CANCEL TRANSACTION";
        cancelTxt.fontSize = 14;
        cancelTxt.color = Color.white;
        cancelTxt.alignment = TextAlignmentOptions.Center;
        cancelTxt.fontStyle = FontStyles.Bold;

        Button cancelBtn = cancelBtnObj.GetComponent<Button>();
        cancelBtn.onClick.AddListener(CloseQRModal);
    }
}
