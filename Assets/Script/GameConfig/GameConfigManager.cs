using System;
using System.Collections;
using System.Collections.Generic;
using System.IO; // Thêm thư viện này để đọc file
using UnityEngine;
using UnityEngine.Networking;

public class GameConfigManager : MonoBehaviour
{
    private static GameConfigManager instance;
    public static GameConfigManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameConfigManager existing = UnityEngine.Object.FindFirstObjectByType<GameConfigManager>();
                if (existing != null)
                {
                    instance = existing;
                }
                else
                {
                    GameObject go = new GameObject("GameConfigManager");
                    instance = go.AddComponent<GameConfigManager>();
                    DontDestroyOnLoad(go);
                    instance.InitManager();
                }
            }
            return instance;
        }
    }

    // Sự kiện báo hiệu khi nạp xong cấu hình từ API
    public static event Action OnConfigLoaded;

    // Xóa const cũ, thay bằng biến private để gán từ file json
    private string baseUrl;

    // BỔ SUNG: Cung cấp property để các Controller khác (Login, NetworkManager) lấy URL cấu hình động
    public string BaseUrl => baseUrl;

    public Dictionary<int, BulletConfig> BulletDb = new Dictionary<int, BulletConfig>();
    public Dictionary<int, WeaponConfig> WeaponDb = new Dictionary<int, WeaponConfig>();
    public Dictionary<string, WeaponConfig> WeaponDbByName = new Dictionary<string, WeaponConfig>(System.StringComparer.OrdinalIgnoreCase);
    public List<BuffConfig> BuffDb = new List<BuffConfig>();

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitManager();
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void InitManager()
    {
        LoadConfig();
        PopulateDefaultWeaponsAndBulletsFallback();
        PopulateDefaultBuffsFallback();
    }

    // Hàm đọc file cấu hình cục bộ
    private void LoadConfig()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "appsettings.json");

        if (File.Exists(filePath))
        {
            try
            {
                string jsonText = File.ReadAllText(filePath);
                // Xóa các dòng comment // để JsonUtility của Unity không bị lỗi
                // Dùng Multiline và ^ để chỉ xóa các comment ở đầu dòng, tránh xóa nhầm // trong URL
                jsonText = System.Text.RegularExpressions.Regex.Replace(jsonText, @"^\s*//.*", "", System.Text.RegularExpressions.RegexOptions.Multiline);
                
                ConfigData config = JsonUtility.FromJson<ConfigData>(jsonText);
                if (config != null && !string.IsNullOrEmpty(config.baseUrl))
                {
                    baseUrl = config.baseUrl;
                }
                else
                {
                    throw new Exception("baseUrl is null or empty");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Config] Lỗi đọc file appsettings.json: {ex.Message}. Sử dụng URL mặc định.");
                baseUrl = "https://rougekiebe.azurewebsites.net/api"; // URL dự phòng
            }
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

        // Thử nạp ngay khi Start (nếu đã có token lưu từ trước)
        yield return StartCoroutine(FetchConfigsRoutine());
    }

    public void ReloadConfigs()
    {
        StartCoroutine(FetchConfigsRoutine());
    }

    private IEnumerator FetchConfigsRoutine()
    {
        yield return StartCoroutine(FetchData($"{baseUrl}/bullets", (json) => {
            try
            {
                if (string.IsNullOrWhiteSpace(json)) throw new Exception("Empty response");
                string wrappedJson = "{\"data\":" + json + "}";
                var wrapper = JsonUtility.FromJson<BulletArrayWrapper>(wrappedJson);
                if (wrapper != null && wrapper.data != null)
                {
                    foreach (var b in wrapper.data) BulletDb[b.id] = b;
                    Debug.Log($"[API] Đã nạp {BulletDb.Count} cấu hình đạn thành công.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[API Error] Lỗi parse cấu hình đạn: {ex.Message} - Json: {json}");
            }
        }));

        yield return StartCoroutine(FetchData($"{baseUrl}/weapons", (json) => {
            try
            {
                if (string.IsNullOrWhiteSpace(json)) throw new Exception("Empty response");
                string wrappedJson = "{\"data\":" + json + "}";
                var wrapper = JsonUtility.FromJson<WeaponArrayWrapper>(wrappedJson);
                if (wrapper != null && wrapper.data != null)
                {
                    foreach (var w in wrapper.data)
                    {
                        WeaponDb[w.id] = w;
                        if (!string.IsNullOrEmpty(w.prefabName))
                        {
                            WeaponDbByName[w.prefabName.Trim()] = w;
                        }
                    }
                    Debug.Log($"[API] Đã nạp {WeaponDb.Count} cấu hình vũ khí ({WeaponDbByName.Count} theo tên Prefab) thành công.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[API Error] Lỗi parse cấu hình vũ khí: {ex.Message} - Json: {json}");
            }
        }));

        yield return StartCoroutine(FetchData($"{baseUrl}/buffs", (json) => {
            try
            {
                if (string.IsNullOrWhiteSpace(json)) throw new Exception("Empty response");
                string wrappedJson = "{\"data\":" + json + "}";
                var wrapper = JsonUtility.FromJson<BuffArrayWrapper>(wrappedJson);
                if (wrapper != null && wrapper.data != null)
                {
                    BuffDb.Clear();
                    foreach (var b in wrapper.data) BuffDb.Add(b);
                    Debug.Log($"[API] Đã nạp {BuffDb.Count} cấu hình Buff thành công.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[API Error] Lỗi parse cấu hình Buff: {ex.Message} - Json: {json}");
            }
        }));

        // Kích hoạt sự kiện báo hiệu cấu hình đã được nạp xong từ API
        OnConfigLoaded?.Invoke();
    }

    private IEnumerator FetchData(string url, Action<string> onSuccess)
    {
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            string token = PlayerPrefs.GetString("jwt_token", "");
            if (!string.IsNullOrEmpty(token))
            {
                webRequest.SetRequestHeader("Authorization", "Bearer " + token);
            }

            webRequest.certificateHandler = new BypassCert();
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
                onSuccess?.Invoke(webRequest.downloadHandler.text);
            else
                Debug.LogError($"[API Error] Lỗi kết nối đến {url}: {webRequest.error}");
        }
    }

    public void PopulateDefaultBuffsFallback()
    {
        if (BuffDb == null) BuffDb = new List<BuffConfig>();
        if (BuffDb.Count > 0) return;

        BuffDb.Add(new BuffConfig { id = 1, buffName = "Tăng Máu Tối Đa", description = "+20 Máu tối đa", buffType = "HP", value = 20, rarity = "Common" });
        BuffDb.Add(new BuffConfig { id = 2, buffName = "Tăng Giáp Tối Đa", description = "+2 Giáp tối đa", buffType = "Armor", value = 2, rarity = "Common" });
        BuffDb.Add(new BuffConfig { id = 3, buffName = "Tăng Năng Lượng Tối Đa", description = "+30 Năng lượng tối đa", buffType = "Mana", value = 30, rarity = "Common" });
        BuffDb.Add(new BuffConfig { id = 4, buffName = "Sức Mạnh Toàn Diện", description = "+10 Máu tối đa", buffType = "HP", value = 10, rarity = "Rare" });

        Debug.Log($"[GameConfigManager] Đã khởi tạo thành công {BuffDb.Count} Buff mặc định dự phòng.");
    }

    public void PopulateDefaultWeaponsAndBulletsFallback()
    {
        if (BulletDb == null) BulletDb = new Dictionary<int, BulletConfig>();
        if (WeaponDb == null) WeaponDb = new Dictionary<int, WeaponConfig>();
        if (WeaponDbByName == null) WeaponDbByName = new Dictionary<string, WeaponConfig>(StringComparer.OrdinalIgnoreCase);

        // Pre-fill Bullets nếu chưa nạp API
        if (!BulletDb.ContainsKey(1)) BulletDb[1] = new BulletConfig { id = 1, bulletName = "Normal Bullet", damage = 15, flightSpeed = 22f, critRate = 0.1f, critMultiplier = 1.5f };
        if (!BulletDb.ContainsKey(2)) BulletDb[2] = new BulletConfig { id = 2, bulletName = "Shotgun Bullet", damage = 10, flightSpeed = 20f, critRate = 0.05f, critMultiplier = 1.5f };
        if (!BulletDb.ContainsKey(3)) BulletDb[3] = new BulletConfig { id = 3, bulletName = "Magnum Bullet", damage = 35, flightSpeed = 25f, critRate = 0.2f, critMultiplier = 2.0f };
        if (!BulletDb.ContainsKey(4)) BulletDb[4] = new BulletConfig { id = 4, bulletName = "Laser Beam", damage = 20, flightSpeed = 30f, critRate = 0.15f, critMultiplier = 1.5f };
        if (!BulletDb.ContainsKey(10)) BulletDb[10] = new BulletConfig { id = 10, bulletName = "Rocket Bullet", damage = 45, flightSpeed = 22f, critRate = 0.15f, critMultiplier = 1.8f, prefabName = "RocketBullet" };
        if (!BulletDb.ContainsKey(11)) BulletDb[11] = new BulletConfig { id = 11, bulletName = "Missile Bullet", damage = 40, flightSpeed = 20f, critRate = 0.15f, critMultiplier = 1.8f, prefabName = "MissileBullet" };

        // Pre-fill Weapons nếu chưa nạp API
        AddFallbackWeapon(new WeaponConfig { id = 1, weaponName = "AK-47 Gold", prefabName = "AK_47A_Gold", bulletsPerShot = 1, spreadAngle = 3f, fireRate = 0.12f, bulletId = 1, manaCost = 0 });
        AddFallbackWeapon(new WeaponConfig { id = 2, weaponName = "AK47", prefabName = "AK47", bulletsPerShot = 1, spreadAngle = 4f, fireRate = 0.15f, bulletId = 1, manaCost = 0 });
        AddFallbackWeapon(new WeaponConfig { id = 3, weaponName = "Rocket Launcher", prefabName = "Rocket_Launcher", bulletsPerShot = 5, spreadAngle = 25f, fireRate = 0.6f, bulletId = 10, manaCost = 1 });
        AddFallbackWeapon(new WeaponConfig { id = 4, weaponName = "Missile Launcher", prefabName = "Missile_Launcher", bulletsPerShot = 5, spreadAngle = 30f, fireRate = 0.5f, bulletId = 11, manaCost = 1 });
        AddFallbackWeapon(new WeaponConfig { id = 5, weaponName = "Assault Shotgun", prefabName = "Assault_Shotgun", bulletsPerShot = 6, spreadAngle = 35f, fireRate = 0.4f, bulletId = 2, manaCost = 1 });
        AddFallbackWeapon(new WeaponConfig { id = 6, weaponName = "Desert Eagle", prefabName = "Desert_Eagle", bulletsPerShot = 1, spreadAngle = 2f, fireRate = 0.3f, bulletId = 3, manaCost = 0 });
        AddFallbackWeapon(new WeaponConfig { id = 7, weaponName = "Laser Gun MK1", prefabName = "Laser_Gun_MK1", bulletsPerShot = 1, spreadAngle = 0f, fireRate = 0.1f, bulletId = 4, manaCost = 0 });
        AddFallbackWeapon(new WeaponConfig { id = 8, weaponName = "M249", prefabName = "M249", bulletsPerShot = 1, spreadAngle = 6f, fireRate = 0.08f, bulletId = 1, manaCost = 0 });
        AddFallbackWeapon(new WeaponConfig { id = 9, weaponName = "M4", prefabName = "M4", bulletsPerShot = 1, spreadAngle = 3f, fireRate = 0.13f, bulletId = 1, manaCost = 0 });
        AddFallbackWeapon(new WeaponConfig { id = 10, weaponName = "Odin", prefabName = "Odin", bulletsPerShot = 1, spreadAngle = 2f, fireRate = 0.2f, bulletId = 1, manaCost = 0 });
        AddFallbackWeapon(new WeaponConfig { id = 11, weaponName = "Snipe", prefabName = "Snipe", bulletsPerShot = 1, spreadAngle = 0f, fireRate = 0.8f, bulletId = 3, manaCost = 1 });
        AddFallbackWeapon(new WeaponConfig { id = 12, weaponName = "Uzi", prefabName = "Uzi", bulletsPerShot = 1, spreadAngle = 8f, fireRate = 0.06f, bulletId = 1, manaCost = 0 });
        AddFallbackWeapon(new WeaponConfig { id = 13, weaponName = "Wooden Bow", prefabName = "Wooden_Bow", bulletsPerShot = 1, spreadAngle = 0f, fireRate = 0.5f, bulletId = 1, manaCost = 0 });

        Debug.Log($"[GameConfigManager] Đã nạp thành công {WeaponDbByName.Count} cấu hình vũ khí mặc định dự phòng.");
    }

    private void AddFallbackWeapon(WeaponConfig w)
    {
        if (!WeaponDb.ContainsKey(w.id)) WeaponDb[w.id] = w;
        if (!string.IsNullOrEmpty(w.prefabName) && !WeaponDbByName.ContainsKey(w.prefabName))
        {
            WeaponDbByName[w.prefabName] = w;
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