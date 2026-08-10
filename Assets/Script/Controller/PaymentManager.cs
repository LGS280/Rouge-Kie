using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class PaymentManager : MonoBehaviour
{
    public static PaymentManager Instance { get; private set; }

    [Header("UI Payment QR Modal (Optional)")]
    public GameObject qrModalPanel;
    public RawImage qrImageDisplay;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI amountText;
    public TextMeshProUGUI orderCodeText;
    public Button closeButton;
    public Button openBrowserButton;
    public Button devSimulateSuccessButton;

    private Coroutine pollingCoroutine;
    private string currentPaymentUrl = "";
    private long currentOrderCode = 0;

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

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseQRModal);
        }

        if (openBrowserButton != null)
        {
            openBrowserButton.onClick.AddListener(OpenPaymentInBrowser);
        }

        if (devSimulateSuccessButton != null)
        {
            devSimulateSuccessButton.onClick.AddListener(TriggerDevSimulateSuccess);
        }

        if (qrModalPanel != null)
        {
            qrModalPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Tạo yêu cầu nạp tiền / mua gói Gem qua PayOS VietQR
    /// </summary>
    public void RequestPayment(int amount, string description = "Nap Gem RogueKie", string currencyType = "GEMS", Action<PaymentResponseData> onSuccess = null, Action<string> onError = null)
    {
        var req = new CreatePaymentRequestData
        {
            amount = amount,
            description = description,
            currencyType = currencyType
        };

        string json = JsonUtility.ToJson(req);
        if (statusText != null) statusText.text = "Creating VietQR payment link...";

        ApiClient.Instance.Post("/Payment/create-payment-link", json, (responseJson) =>
        {
            try
            {
                PaymentResponseData res = JsonUtility.FromJson<PaymentResponseData>(responseJson);
                currentPaymentUrl = res.paymentUrl;
                currentOrderCode = res.orderCode;

                Debug.Log($"[PaymentManager] tao lien ket thanh toan thanh cong! OrderCode: {res.orderCode}, URL: {res.paymentUrl}");

                // Hiển thị Modal VietQR nếu có
                ShowQRModal(res);

                onSuccess?.Invoke(res);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PaymentManager] Lỗi parse JSON response: {ex.Message}");
                if (statusText != null) statusText.text = "Error parsing payment data.";
                onError?.Invoke(ex.Message);
            }
        }, (err) =>
        {
            Debug.LogError($"[PaymentManager] API Error: {err}");
            if (statusText != null) statusText.text = "Payment request failed.";
            onError?.Invoke(err);
        });
    }

    /// <summary>
    /// Hiển thị Modal VietQR và nạp ảnh QR Code từ URL
    /// </summary>
    public void ShowQRModal(PaymentResponseData res)
    {
        if (qrModalPanel != null)
        {
            qrModalPanel.SetActive(true);
        }

        if (amountText != null) amountText.text = $"Amount: {res.amount:N0} VND";
        if (orderCodeText != null) orderCodeText.text = $"Order #: {res.orderCode}";
        if (statusText != null) statusText.text = "Scan VietQR Code with Banking App...";

        if (qrImageDisplay != null && !string.IsNullOrEmpty(res.qrCodeUrl))
        {
            StartCoroutine(DownloadQRTexture(res.qrCodeUrl));
        }

        // Bắt đầu luồng kiểm tra (Polling) tự động mỗi 2.5 giây
        if (pollingCoroutine != null) StopCoroutine(pollingCoroutine);
        pollingCoroutine = StartCoroutine(PollPaymentStatusRoutine(res.orderCode));
    }

    private IEnumerator DownloadQRTexture(string url)
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
                }
            }
            else
            {
                Debug.LogWarning($"[PaymentManager] Không thể tải ảnh VietQR: {www.error}");
            }
        }
    }

    private IEnumerator PollPaymentStatusRoutine(long orderCode)
    {
        while (true)
        {
            yield return new WaitForSeconds(2.5f);

            ApiClient.Instance.Get($"/Payment/check-status/{orderCode}", (responseJson) =>
            {
                try
                {
                    PaymentResponseData res = JsonUtility.FromJson<PaymentResponseData>(responseJson);
                    if (res.status == "PAID")
                    {
                        Debug.Log($"[PaymentManager] Giao dich OrderCode {orderCode} da duoc THANH TOAN thanh cong!");
                        if (statusText != null) statusText.text = "<color=green>Payment SUCCESSFUL! Assets added!</color>";

                        // Tự động làm mới UI Profile Gems/Coins người chơi
                        if (PlayerProfileUI.Instance != null)
                        {
                            PlayerProfileUI.Instance.RefreshProfile();
                        }

                        // Dừng polling và tự động đóng modal sau 2s
                        if (pollingCoroutine != null) StopCoroutine(pollingCoroutine);
                        Invoke(nameof(CloseQRModal), 2f);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PaymentManager] Polling parse error: {ex.Message}");
                }
            }, (err) =>
            {
                Debug.LogWarning($"[PaymentManager] Polling status check error: {err}");
            });
        }
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
        if (currentOrderCode <= 0) return;

        if (statusText != null) statusText.text = "Simulating dev payment success...";

        ApiClient.Instance.Post($"/Payment/dev-simulate-success/{currentOrderCode}", "{}", (res) =>
        {
            Debug.Log("[PaymentManager] Gia lap thanh toan thanh cong!");
            if (statusText != null) statusText.text = "<color=green>Dev Simulation Successful!</color>";

            if (PlayerProfileUI.Instance != null)
            {
                PlayerProfileUI.Instance.RefreshProfile();
            }

            Invoke(nameof(CloseQRModal), 1.5f);
        }, (err) =>
        {
            Debug.LogError($"[PaymentManager] Dev simulation error: {err}");
        });
    }

    public void CloseQRModal()
    {
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
}
