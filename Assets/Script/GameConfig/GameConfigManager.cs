using System;
using System.Collections;
using System.Collections.Generic;
using System.IO; // Thêm thư viện này để đọc file
using UnityEngine;
using UnityEngine.Networking;

public class GameConfigManager : MonoBehaviour
{
    public static GameConfigManager Instance { get; private set; }

    // Xóa const cũ, thay bằng biến private để gán từ file json
    private string baseUrl;

    // BỔ SUNG: Cung cấp property để các Controller khác (Login, NetworkManager) lấy URL cấu hình động
    public string BaseUrl => baseUrl;

    public Dictionary<int, BulletConfig> BulletDb = new Dictionary<int, BulletConfig>();
    public Dictionary<int, WeaponConfig> WeaponDb = new Dictionary<int, WeaponConfig>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadConfig(); // Đọc file config ngay khi khởi tạo
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Hàm đọc file cấu hình cục bộ
    private void LoadConfig()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "appsettings.json");

        if (File.Exists(filePath))
        {
            string jsonText = File.ReadAllText(filePath);
            ConfigData config = JsonUtility.FromJson<ConfigData>(jsonText);
            baseUrl = config.baseUrl;
        }
        else
        {
            // Fallback nếu không tìm thấy file (ví dụ: khi build lên môi trường production)
            baseUrl = "https://your-production-api.com/api";
            Debug.LogWarning("[Config] Không tìm thấy appsettings.json. Sử dụng URL mặc định.");
        }
    }

    private IEnumerator Start()
    {
        // Chờ nạp xong baseUrl (đề phòng trường hợp bất đồng bộ)
        if (string.IsNullOrEmpty(baseUrl)) yield return null;

        yield return StartCoroutine(FetchData($"{baseUrl}/bullets", (json) => {
            string wrappedJson = "{\"data\":" + json + "}";
            var wrapper = JsonUtility.FromJson<BulletArrayWrapper>(wrappedJson);
            foreach (var b in wrapper.data) BulletDb[b.id] = b;
            Debug.Log($"[API] Đã nạp {BulletDb.Count} cấu hình đạn thành công.");
        }));

        yield return StartCoroutine(FetchData($"{baseUrl}/weapons", (json) => {
            string wrappedJson = "{\"data\":" + json + "}";
            var wrapper = JsonUtility.FromJson<WeaponArrayWrapper>(wrappedJson);
            foreach (var w in wrapper.data) WeaponDb[w.id] = w;
            Debug.Log($"[API] Đã nạp {WeaponDb.Count} cấu hình vũ khí thành công.");
        }));
    }

    private IEnumerator FetchData(string url, Action<string> onSuccess)
    {
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            webRequest.certificateHandler = new BypassCert();
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
                onSuccess?.Invoke(webRequest.downloadHandler.text);
            else
                Debug.LogError($"[API Error] Lỗi kết nối đến {url}: {webRequest.error}");
        }
    }
}

// Class bổ trợ để map dữ liệu từ file appsettings.json
[Serializable]
public class ConfigData
{
    public string baseUrl;
}

public class BypassCert : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] d) => true;
}