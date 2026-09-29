using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Quản lý hệ thống nhân vật (Character / Agent System):
/// - Quản lý nhân vật đang chọn (Rookie, Hero Zero).
/// - Thực hiện hoán đổi nhân vật tức thời tại Lobby (Runtime Swap).
/// - Đồng bộ nhân vật đã chọn khi bước vào Dungeon.
/// - Tự động thiết lập Trigger và Biển Bảng AGENT tại nhà kho bên cạnh Weapon Vault.
/// </summary>
public class CharacterManager : MonoBehaviour
{
    private static CharacterManager instance;
    public static CharacterManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = UnityEngine.Object.FindFirstObjectByType<CharacterManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("CharacterManager");
                    instance = go.AddComponent<CharacterManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    [Header("Character Prefabs")]
    public GameObject rookiePrefab;
    public GameObject zeroPrefab;

    public const string PREFS_SELECTED_CHAR = "SelectedCharacter";

    public event Action<string> OnCharacterChanged;

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
            return;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        var mgr = Instance;
        mgr.OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Lobby_Scene")
        {
            EnsureHangarTriggerAndBoard();
            EnsureSelectedCharacterSpawned();
        }
        else if (scene.name == "SampleScene")
        {
            EnsureSelectedCharacterSpawned();
        }
    }

    /// <summary>
    /// Lấy key PlayerPrefs lưu nhân vật đang chọn theo từng tài khoản người chơi (UserId)
    /// </summary>
    public static string GetUserSelectedCharacterKey()
    {
        int userId = PlayerPrefs.GetInt("user_id", 0);
        return userId > 0 ? $"SelectedCharacter_{userId}" : "SelectedCharacter_guest";
    }

    /// <summary>
    /// Lấy tên nhân vật đang được chọn (mặc định: "Rookie")
    /// </summary>
    public string GetSelectedCharacter()
    {
        string key = GetUserSelectedCharacterKey();
        string selected = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(selected))
        {
            selected = PlayerPrefs.GetString(PREFS_SELECTED_CHAR, "Rookie");
        }

        // Đảm bảo nhân vật đang chọn phải thực sự đã được tài khoản hiện tại mở khóa
        if (!ShopUIController.IsCharacterUnlocked(selected))
        {
            selected = "Rookie";
            PlayerPrefs.SetString(key, "Rookie");
            PlayerPrefs.SetString(PREFS_SELECTED_CHAR, "Rookie");
            PlayerPrefs.Save();
        }
        return selected;
    }

    /// <summary>
    /// Đặt nhân vật đang chọn vào PlayerPrefs theo tài khoản
    /// </summary>
    public void SetSelectedCharacter(string characterName)
    {
        string key = GetUserSelectedCharacterKey();
        PlayerPrefs.SetString(key, characterName);
        PlayerPrefs.SetString(PREFS_SELECTED_CHAR, characterName);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Đặt lại nhân vật về Rookie (khi Đăng xuất tài khoản)
    /// </summary>
    public void ResetToDefaultCharacter()
    {
        SetSelectedCharacter("Rookie");
        GameObject current = GameObject.FindGameObjectWithTag("Player");
        if (current != null && current.name.Replace("(Clone)", "").Trim().Equals("Zero", StringComparison.OrdinalIgnoreCase))
        {
            SwitchCharacter("Rookie");
        }
    }

    /// <summary>
    /// Nạp Prefab nhân vật tương ứng từ Inspector hoặc AssetDatabase
    /// </summary>
    public GameObject GetCharacterPrefab(string charName)
    {
        string clean = (charName ?? "").ToLower().Trim();
        if (clean.Contains("zero") || clean.Contains("mage"))
        {
            if (zeroPrefab != null) return zeroPrefab;
#if UNITY_EDITOR
            zeroPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Zero/Zero.prefab");
            if (zeroPrefab != null) return zeroPrefab;
#endif
        }
        else
        {
            if (rookiePrefab != null) return rookiePrefab;
#if UNITY_EDITOR
            rookiePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Rookie/Rookie.prefab");
            if (rookiePrefab != null) return rookiePrefab;
#endif
        }
        return null;
    }

    /// <summary>
    /// Hoán đổi sang nhân vật mới tức thời (Runtime Swap)
    /// </summary>
    public bool SwitchCharacter(string characterName)
    {
        if (string.IsNullOrEmpty(characterName)) return false;
        string clean = characterName.Trim();
        if (clean.ToLower().Contains("zero") || clean.ToLower().Contains("mage")) clean = "Zero";
        else clean = "Rookie";

        GameObject current = GameObject.FindGameObjectWithTag("Player");
        if (current != null && current.name.Replace("(Clone)", "").Trim().Equals(clean, StringComparison.OrdinalIgnoreCase))
        {
            SetSelectedCharacter(clean);
            OnCharacterChanged?.Invoke(clean);
            return true;
        }

        Vector3 spawnPos = new Vector3(0f, -2f, 0f);
        Quaternion spawnRot = Quaternion.identity;

        if (current != null)
        {
            spawnPos = current.transform.position;
            spawnRot = current.transform.rotation;

            // Lưu lại vũ khí đang cầm trên tay và lưng
            if (WeaponManager.Instance != null)
            {
                WeaponManager.Instance.SaveEquippedWeapons();
            }

            Destroy(current);
        }
        else
        {
            GameObject sp = GameObject.Find("PlayerSpawnPoint");
            if (sp != null) spawnPos = sp.transform.position;
        }

        GameObject prefab = GetCharacterPrefab(clean);
        if (prefab == null)
        {
            Debug.LogError($"[CharacterManager] Không tìm thấy Prefab cho nhân vật '{clean}'!");
            return false;
        }

        GameObject newPlayer = Instantiate(prefab, spawnPos, spawnRot);
        newPlayer.name = clean;
        newPlayer.tag = "Player";

        // Cập nhật Camera đi theo nhân vật mới
        CameraController cam = UnityEngine.Object.FindFirstObjectByType<CameraController>();
        if (cam != null)
        {
            cam.target = newPlayer.transform;
        }

        // Khôi phục vũ khí trên nhân vật mới
        WeaponManager newWm = newPlayer.GetComponent<WeaponManager>();
        if (newWm != null)
        {
            newWm.RestoreSavedEquippedWeapons();
        }

        // Đảm bảo có mũi tên chỉ báo người chơi (LocalPlayer_Arrow) lơ lửng trên đầu
        if (newPlayer.transform.Find("LocalPlayer_Arrow") == null)
        {
            GameObject rookiePref = GetCharacterPrefab("Rookie");
            if (rookiePref != null)
            {
                Transform arrowTemplate = rookiePref.transform.Find("LocalPlayer_Arrow");
                if (arrowTemplate != null)
                {
                    GameObject arrowInst = Instantiate(arrowTemplate.gameObject, newPlayer.transform);
                    arrowInst.name = "LocalPlayer_Arrow";
                    arrowInst.transform.localPosition = arrowTemplate.localPosition;
                    arrowInst.transform.localRotation = arrowTemplate.localRotation;
                    arrowInst.transform.localScale = arrowTemplate.localScale;
                }
            }
        }

        // Đồng bộ lại PlayerHUD
        RookieHealth newHealth = newPlayer.GetComponent<RookieHealth>();
        if (newHealth != null)
        {
            PlayerHUD[] huds = UnityEngine.Object.FindObjectsByType<PlayerHUD>(FindObjectsSortMode.None);
            foreach (var hud in huds)
            {
                if (hud != null) hud.SetTarget(newHealth);
            }
        }

        SetSelectedCharacter(clean);

        Debug.Log($"[CharacterManager] Đã đổi nhân vật thành công sang '{clean}' tại vị trí {spawnPos}!");
        OnCharacterChanged?.Invoke(clean);

        if (CharacterSelectUIController.Instance != null && CharacterSelectUIController.Instance.IsRosterOpen())
        {
            CharacterSelectUIController.Instance.RefreshCardsDisplay();
        }

        return true;
    }

    /// <summary>
    /// Đảm bảo nhân vật xuất hiện trong màn chơi khớp với nhân vật đã chọn
    /// </summary>
    public void EnsureSelectedCharacterSpawned()
    {
        string selected = GetSelectedCharacter();
        GameObject current = GameObject.FindGameObjectWithTag("Player");

        if (current != null)
        {
            string curName = current.name.Replace("(Clone)", "").Trim();
            if (selected.Equals("Zero", StringComparison.OrdinalIgnoreCase) && !curName.Equals("Zero", StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[CharacterManager] Nhân vật hiện tại là '{curName}' khác với lựa chọn '{selected}'. Đang tự động đổi sang '{selected}'...");
                SwitchCharacter("Zero");
            }
            else if (selected.Equals("Rookie", StringComparison.OrdinalIgnoreCase) && curName.Equals("Zero", StringComparison.OrdinalIgnoreCase))
            {
                SwitchCharacter("Rookie");
            }
        }
    }

    /// <summary>
    /// Tự động kiểm tra Trigger Zone cho Nhà Kho Nhân Vật kế bên Kho Vũ Khí (Bỏ biển bảng Agent theo yêu cầu)
    /// </summary>
    public void EnsureHangarTrigger()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene != "Lobby_Scene") return;

        // Dọn dẹp dứt điểm các biển bảng AgentBoard hoặc CharacterBoard nếu có
        GameObject boardObj = GameObject.Find("AgentBoard");
        if (boardObj != null) Destroy(boardObj);
        GameObject charBoardObj = GameObject.Find("CharacterBoard");
        if (charBoardObj != null) Destroy(charBoardObj);

        // Kiểm tra / Tạo Trigger Zone cho Kho Đổi Nhân Vật
        GameObject triggerObj = GameObject.Find("CharacterVault_Trigger");
        if (triggerObj == null)
        {
            triggerObj = GameObject.Find("AgentRoster_Trigger");
        }

        if (triggerObj == null)
        {
            triggerObj = new GameObject("CharacterVault_Trigger");
            triggerObj.transform.position = new Vector3(-10.45f, 7.24f, 0f);

            BoxCollider2D col = triggerObj.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(4f, 3f);

            LobbyNPCInteraction inter = triggerObj.AddComponent<LobbyNPCInteraction>();
            inter.entityName = "Agent Character Vault";
            inter.promptText = "<b>[E]</b>";
            inter.interactionType = LobbyInteractionType.CharacterVault;
            inter.textOffset = new Vector2(0f, -0.21f);
            inter.fontSize = 70f;
            inter.textColor = Color.white;

            Debug.Log("[CharacterManager] Đã khởi tạo Trigger Zone cho Kho Nhân Vật tại (-10.45, 7.24)!");
        }
        else
        {
            LobbyNPCInteraction inter = triggerObj.GetComponent<LobbyNPCInteraction>();
            if (inter != null)
            {
                inter.interactionType = LobbyInteractionType.CharacterVault;
                inter.promptText = "<b>[E]</b>";
                inter.fontSize = 70f;
                inter.textOffset = new Vector2(0f, -0.21f);
            }
        }
    }

    public void EnsureHangarTriggerAndBoard()
    {
        EnsureHangarTrigger();
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Rouge-Kie/Setup Agent Hangar in Scene")]
    public static void EditorSetupHangar()
    {
        Instance.EnsureHangarTriggerAndBoard();
        Debug.Log("[CharacterManager] Đã thiết lập thành công Kho Nhân Vật AGENT vào Scene!");
    }
#endif
}
