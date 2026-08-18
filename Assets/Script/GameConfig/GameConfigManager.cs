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
