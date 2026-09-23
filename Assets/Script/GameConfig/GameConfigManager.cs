using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class GameConfigManager : MonoBehaviour
{
    public static GameConfigManager Instance { get; private set; }

    public static event Action OnConfigLoaded;

    private string baseUrl;

    public string BaseUrl => baseUrl;

    public Dictionary<int, BulletConfig> BulletDb = new Dictionary<int, BulletConfig>();
    public Dictionary<int, WeaponConfig> WeaponDb = new Dictionary<int, WeaponConfig>();
    public Dictionary<string, WeaponConfig> WeaponDbByName = new Dictionary<string, WeaponConfig>(System.StringComparer.OrdinalIgnoreCase);
    public List<BuffConfig> BuffDb = new List<BuffConfig>();
    public Dictionary<int, CharacterConfig> CharacterDb = new Dictionary<int, CharacterConfig>();
    public Dictionary<string, CharacterConfig> CharacterDbByName = new Dictionary<string, CharacterConfig>(System.StringComparer.OrdinalIgnoreCase);
    public Dictionary<int, EnemyConfig> EnemyDb = new Dictionary<int, EnemyConfig>();
    public Dictionary<string, EnemyConfig> EnemyDbByName = new Dictionary<string, EnemyConfig>(System.StringComparer.OrdinalIgnoreCase);
    public Dictionary<int, LevelConfig> LevelDb = new Dictionary<int, LevelConfig>();
    public MaintenanceStatus CurrentMaintenance { get; private set; } = new MaintenanceStatus();
    public bool IsUnderMaintenance => CurrentMaintenance != null && CurrentMaintenance.isUnderMaintenance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadConfig();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void LoadConfig()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        string filePath = Path.Combine(Application.streamingAssetsPath, "appsettings.json");

        if (File.Exists(filePath))
        {
            try
            {
                string jsonText = File.ReadAllText(filePath);
                jsonText = System.Text.RegularExpressions.Regex.Replace(jsonText, @"^\s*//.*", "", System.Text.RegularExpressions.RegexOptions.Multiline);

                ConfigData config = JsonUtility.FromJson<ConfigData>(jsonText);
                if (config != null && !string.IsNullOrEmpty(config.baseUrl))
                {
                    baseUrl = config.baseUrl;
                }
            }
            catch
            {
            }
        }
#endif
    }

    private IEnumerator Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        string streamingPath = Path.Combine(Application.streamingAssetsPath, "appsettings.json");
        using (UnityWebRequest www = UnityWebRequest.Get(streamingPath))
        {
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                string jsonText = www.downloadHandler.text;
                jsonText = System.Text.RegularExpressions.Regex.Replace(jsonText, @"^\s*//.*", "", System.Text.RegularExpressions.RegexOptions.Multiline);
                ConfigData config = JsonUtility.FromJson<ConfigData>(jsonText);
                if (config != null && !string.IsNullOrEmpty(config.baseUrl))
                {
                    baseUrl = config.baseUrl;
                }
            }
        }
#endif

        if (string.IsNullOrEmpty(baseUrl)) yield return null;

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

                }
            }
            catch (Exception ex)
            {

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

                }
            }
            catch (Exception ex)
            {

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

                }
            }
            catch (Exception ex)
            {

            }
        }));

        yield return StartCoroutine(FetchData($"{baseUrl}/characters", (json) => {
            try
            {
                if (string.IsNullOrWhiteSpace(json)) throw new Exception("Empty response");
                string wrappedJson = "{\"data\":" + json + "}";
                var wrapper = JsonUtility.FromJson<CharacterArrayWrapper>(wrappedJson);
                if (wrapper != null && wrapper.data != null)
                {
                    foreach (var c in wrapper.data)
                    {
                        CharacterDb[c.GetId()] = c;
                        if (!string.IsNullOrEmpty(c.prefabName))
                        {
                            CharacterDbByName[c.prefabName.Trim()] = c;
                        }
                        if (!string.IsNullOrEmpty(c.name))
                        {
                            CharacterDbByName[c.name.Trim()] = c;
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }
        }));

        yield return StartCoroutine(FetchData($"{baseUrl}/enemies", (json) => {
            try
            {
                if (string.IsNullOrWhiteSpace(json)) throw new Exception("Empty response");
                string wrappedJson = "{\"data\":" + json + "}";
                var wrapper = JsonUtility.FromJson<EnemyArrayWrapper>(wrappedJson);
                if (wrapper != null && wrapper.data != null)
                {
                    foreach (var e in wrapper.data)
                    {
                        EnemyDb[e.id] = e;
                        if (!string.IsNullOrEmpty(e.prefabName))
                        {
                            EnemyDbByName[e.prefabName.Trim()] = e;
                        }
                        if (!string.IsNullOrEmpty(e.enemyName))
                        {
                            EnemyDbByName[e.enemyName.Trim()] = e;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }));

        yield return StartCoroutine(FetchData($"{baseUrl}/levels", (json) => {
            try
            {
                if (string.IsNullOrWhiteSpace(json)) throw new Exception("Empty response");
                string wrappedJson = "{\"data\":" + json + "}";
                var wrapper = JsonUtility.FromJson<LevelArrayWrapper>(wrappedJson);
                if (wrapper != null && wrapper.data != null)
                {
                    LevelDb.Clear();
                    foreach (var l in wrapper.data)
                    {
                        LevelDb[l.floorNumber] = l;
                    }
                }
            }
            catch (Exception)
            {
            }
        }));

        // Tự động kiểm tra trạng thái bảo trì hệ thống từ server
        yield return StartCoroutine(FetchData($"{baseUrl}/maintenance/current", (json) => {
            try
            {
                if (!string.IsNullOrWhiteSpace(json))
                {
                    CurrentMaintenance = JsonUtility.FromJson<MaintenanceStatus>(json);
                    if (CurrentMaintenance != null && CurrentMaintenance.isUnderMaintenance)
                    {
                        Debug.LogWarning($"[GameConfigManager] Máy chủ đang bảo trì: {CurrentMaintenance.title} - {CurrentMaintenance.message}");
                    }
                }
            }
            catch (Exception)
            {
            }
        }));

        OnConfigLoaded?.Invoke();
    }

    /// <summary>
    /// Kiểm tra trạng thái bảo trì máy chủ trực tiếp theo thời gian thực
    /// </summary>
    public IEnumerator CheckMaintenanceStatus(Action<MaintenanceStatus> onResult = null)
    {
        if (string.IsNullOrEmpty(baseUrl))
        {
            onResult?.Invoke(CurrentMaintenance);
            yield break;
        }

        yield return StartCoroutine(FetchData($"{baseUrl}/maintenance/current", (json) => {
            try
            {
                if (!string.IsNullOrWhiteSpace(json))
                {
                    CurrentMaintenance = JsonUtility.FromJson<MaintenanceStatus>(json);
                }
            }
            catch (Exception)
            {
            }
            onResult?.Invoke(CurrentMaintenance);
        }));
    }

    public CharacterConfig GetCharacterConfig(string identifier)
    {
        if (string.IsNullOrEmpty(identifier)) return null;
        string cleanKey = identifier.Replace("(Clone)", "").Trim();
        if (CharacterDbByName.TryGetValue(cleanKey, out var config))
        {
            return config;
        }
        return null;
    }

    public CharacterConfig GetCharacterConfig(int id)
    {
        if (CharacterDb.TryGetValue(id, out var config))
        {
            return config;
        }
        return null;
    }

    public EnemyConfig GetEnemyConfig(string identifier)
    {
        if (string.IsNullOrEmpty(identifier)) return null;
        string cleanKey = identifier.Replace("(Clone)", "").Trim();
        if (EnemyDbByName.TryGetValue(cleanKey, out var config))
        {
            return config;
        }
        return null;
    }

    public EnemyConfig GetEnemyConfig(int id)
    {
        if (EnemyDb.TryGetValue(id, out var config))
        {
            return config;
        }
        return null;
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
        }
    }

    public LevelConfig GetLevelConfig(int floor)
    {
        if (LevelDb != null && LevelDb.TryGetValue(floor, out var config))
        {
            return config;
        }
        return null;
    }

    public float GetDifficultyMultiplier(int floor, int stage = 1)
    {
        if (LevelDb.TryGetValue(floor, out var config) && config.difficultyMultiplier > 0f)
        {
            return config.difficultyMultiplier;
        }
        // Hệ số dự phòng nếu chưa tải xong dữ liệu từ server
        return 1.0f + (floor - 1) * 0.3f;
    }

    public int GetMaxFloor(int defaultMax = 5)
    {
        if (LevelDb != null && LevelDb.Count > 0)
        {
            int max = 0;
            foreach (var k in LevelDb.Keys)
            {
                if (k > max) max = k;
            }
            return max > 0 ? max : defaultMax;
        }
        return defaultMax;
    }

    public void PopulateDefaultBuffsFallback()
    {
        if (BuffDb == null) BuffDb = new List<BuffConfig>();
        if (BuffDb.Count > 0) return;

        // Dữ liệu Buffs dự phòng (Tiếng Anh trên UI, comment giữ nguyên tiếng Việt)
        BuffDb.Add(new BuffConfig { id = 1, buffName = "Max HP Boost", description = "+20 Max Health", buffType = "HP", value = 20, rarity = "Common" });
        BuffDb.Add(new BuffConfig { id = 2, buffName = "Max Armor Boost", description = "+2 Max Armor", buffType = "Armor", value = 2, rarity = "Common" });
        BuffDb.Add(new BuffConfig { id = 3, buffName = "Max Mana Boost", description = "+30 Max Energy", buffType = "Mana", value = 30, rarity = "Common" });
        BuffDb.Add(new BuffConfig { id = 4, buffName = "All-Around Boost", description = "+10 Max Health", buffType = "HP", value = 10, rarity = "Rare" });
    }
}

[Serializable]
public class ConfigData
{
    public string baseUrl;
}

public class BypassCert : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] d) => true;
}
