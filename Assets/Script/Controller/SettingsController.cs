using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class SettingsController : MonoBehaviour
{
    [Header("Audio UI")]
    public Slider masterSlider;
    public Slider bgmSlider;
    public Slider sfxSlider;
    public AudioSource bgmSource;

    [Header("Graphics UI")]
    public Toggle fullscreenToggle;
    public TMP_Dropdown resolutionDropdown;
    public TMP_Dropdown fpsDropdown;

    private Resolution[] resolutions;
    private bool _isInitializing = false;

    private void Awake()
    {
        if (resolutionDropdown != null)
            SetupResolutionDropdown();
        else
            Debug.LogError("resolutionDropdown chưa được gán trong Inspector!");

        if (fpsDropdown != null)
            SetupFPSDropdown();
    }

    private void OnEnable()
    {
        LoadSettings();
    }

    private void Start()
    {
        LoadSettings();
    }

    // --- CẤU HÌNH ĐỒ HỌA ---

    private void SetupResolutionDropdown()
    {
        if (resolutionDropdown == null) return;

        resolutionDropdown.ClearOptions();

        System.Collections.Generic.List<string> options = new System.Collections.Generic.List<string>
{
    "1920 x 1080",
    "1600 x 900",
    "1280 x 720"
};

        resolutions = new Resolution[]
        {
    new Resolution { width = 1920, height = 1080 },
    new Resolution { width = 1600, height = 900 },
    new Resolution { width = 1280, height = 720 }
        };

        resolutionDropdown.AddOptions(options);

        int savedIndex = PlayerPrefs.GetInt("ResolutionIndex", 0);
        resolutionDropdown.value = savedIndex;
        resolutionDropdown.RefreshShownValue();

        // Gắn event ở đây luôn cho chắc
        resolutionDropdown.onValueChanged.RemoveAllListeners();
        resolutionDropdown.onValueChanged.AddListener(SetResolution);

        Debug.Log("Dropdown setup done, options: " + options.Count);
    }
    public void SetResolution(int resolutionIndex)
    {
        

        if (resolutions == null || resolutionIndex >= resolutions.Length)
        {
            
            return;
        }

        Resolution resolution = resolutions[resolutionIndex];
        
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreenMode);
        PlayerPrefs.SetInt("ResolutionIndex", resolutionIndex);
        PlayerPrefs.Save();
    }

    public void SetFullscreen(bool isFullscreen)
    {
        if (isFullscreen)
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
        else
            Screen.fullScreenMode = FullScreenMode.Windowed;

        PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void SetupFPSDropdown()
    {
        if (fpsDropdown == null) return;

        fpsDropdown.ClearOptions();

        System.Collections.Generic.List<string> options = new System.Collections.Generic.List<string>
        {
            "60 FPS",
            "120 FPS",
            "144 FPS"
        };

        fpsDropdown.AddOptions(options);

        int savedIndex = PlayerPrefs.GetInt("FPSLimitIndex", 0);
        if (savedIndex < 0 || savedIndex >= options.Count)
        {
            savedIndex = 0;
        }

        fpsDropdown.value = savedIndex;
        fpsDropdown.RefreshShownValue();

        fpsDropdown.onValueChanged.RemoveAllListeners();
        fpsDropdown.onValueChanged.AddListener(SetFPSLimit);
    }

    public void SetFPSLimit(int index)
    {
        switch (index)
        {
            case 0: Application.targetFrameRate = 60; break;
            case 1: Application.targetFrameRate = 120; break;
            case 2: Application.targetFrameRate = 144; break;
            default: Application.targetFrameRate = 60; break;
        }
        PlayerPrefs.SetInt("FPSLimitIndex", index);
        PlayerPrefs.Save();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitGlobalSettings()
    {
        ApplyAllSavedSettings();
        SceneManager.sceneLoaded += OnSceneLoadedGlobal;
    }

    private static void OnSceneLoadedGlobal(Scene scene, LoadSceneMode mode)
    {
        ApplyAllSavedSettings();
    }

    /// <summary>
    /// Áp dụng toàn bộ cài đặt đã lưu trong PlayerPrefs ngay lập tức (Audio, Resolution, Fullscreen, FPS)
    /// </summary>
    public static void ApplyAllSavedSettings()
    {
        // 1. Âm thanh: Master Volume
        float masterVol = PlayerPrefs.GetFloat("MasterVol", 0.75f);
        AudioListener.volume = Mathf.Clamp01(masterVol);

        // 2. Âm thanh: BGM & SFX Volume
        float bgmVol = PlayerPrefs.GetFloat("BGMVol", 0.60f);
        float sfxVol = PlayerPrefs.GetFloat("SFXVol", 0.75f);

        // Tìm AudioSource BGMPlayer ở Scene_Menu nếu có
        GameObject bgmPlayer = GameObject.Find("BGMPlayer");
        if (bgmPlayer != null)
        {
            AudioSource src = bgmPlayer.GetComponent<AudioSource>();
            if (src != null)
            {
                src.volume = (bgmVol <= 0.0001f) ? 0f : bgmVol;
            }
        }

        // Đồng bộ với AudioManager nếu đang tồn tại (Lobby / In-Game)
        if (RogueKie.Audio.AudioManager.Instance != null)
        {
            RogueKie.Audio.AudioManager.Instance.SetVolume("MasterVolume", masterVol);
            RogueKie.Audio.AudioManager.Instance.SetVolume("BGMVolume", bgmVol);
            RogueKie.Audio.AudioManager.Instance.SetVolume("SFXVolume", sfxVol);
        }

        // 3. Đồ họa: Fullscreen
        bool isFullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        Screen.fullScreenMode = isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        // 4. Đồ họa: FPS Limit
        int fpsIndex = PlayerPrefs.GetInt("FPSLimitIndex", 0);
        switch (fpsIndex)
        {
            case 0: Application.targetFrameRate = 60; break;
            case 1: Application.targetFrameRate = 120; break;
            case 2: Application.targetFrameRate = 144; break;
            default: Application.targetFrameRate = 60; break;
        }

        // 5. Đồ họa: Resolution
        int resIndex = PlayerPrefs.GetInt("ResolutionIndex", 0);
        Resolution[] defaultResolutions = new Resolution[]
        {
            new Resolution { width = 1920, height = 1080 },
            new Resolution { width = 1600, height = 900 },
            new Resolution { width = 1280, height = 720 }
        };
        if (resIndex >= 0 && resIndex < defaultResolutions.Length)
        {
            Resolution res = defaultResolutions[resIndex];
            Screen.SetResolution(res.width, res.height, Screen.fullScreenMode);
        }
    }

    // --- CẤU HÌNH ÂM THANH ---

    public void SetMasterVolume(float volume)
    {
        if (_isInitializing) return;
        PlayerPrefs.SetFloat("MasterVol", volume);
        PlayerPrefs.Save();
        AudioListener.volume = Mathf.Clamp01(volume);
        if (RogueKie.Audio.AudioManager.Instance != null)
        {
            RogueKie.Audio.AudioManager.Instance.SetVolume("MasterVolume", volume);
        }
    }

    public void SetBGMVolume(float volume)
    {
        if (_isInitializing) return;
        PlayerPrefs.SetFloat("BGMVol", volume);
        PlayerPrefs.Save();
        if (bgmSource != null) bgmSource.volume = (volume <= 0.0001f) ? 0f : volume;
        if (RogueKie.Audio.AudioManager.Instance != null)
        {
            RogueKie.Audio.AudioManager.Instance.SetVolume("BGMVolume", volume);
        }
    }

    public void SetSFXVolume(float volume)
    {
        if (_isInitializing) return;
        PlayerPrefs.SetFloat("SFXVol", volume);
        PlayerPrefs.Save();
        if (RogueKie.Audio.AudioManager.Instance != null)
        {
            RogueKie.Audio.AudioManager.Instance.SetVolume("SFXVolume", volume);
        }
    }

    // --- LOAD CÀI ĐẶT KHI KHỞI ĐỘNG GAME ---

    private void LoadSettings()
    {
        _isInitializing = true;
        try
        {
            // Self-healing: if previous bug corrupted BGMVol to ~0, reset it to default 0.60f once
            if (!PlayerPrefs.HasKey("BGMVol_Fixed"))
            {
                PlayerPrefs.SetInt("BGMVol_Fixed", 1);
                if (PlayerPrefs.GetFloat("BGMVol", 0.60f) <= 0.00015f)
                {
                    PlayerPrefs.SetFloat("BGMVol", 0.60f);
                }
            }

            // Áp dụng cài đặt hệ thống toàn cục trước
            ApplyAllSavedSettings();

            // Đồng bộ giá trị vào các thành phần UI
            float masterVol = PlayerPrefs.GetFloat("MasterVol", 0.75f);
            if (masterSlider != null)
            {
                masterSlider.onValueChanged.RemoveAllListeners();
                masterSlider.value = masterVol;
                masterSlider.onValueChanged.AddListener(SetMasterVolume);
            }

            float bgmVol = PlayerPrefs.GetFloat("BGMVol", 0.60f);
            if (bgmSlider != null)
            {
                bgmSlider.onValueChanged.RemoveAllListeners();
                bgmSlider.value = bgmVol;
                bgmSlider.onValueChanged.AddListener(SetBGMVolume);
                if (bgmSource != null) bgmSource.volume = (bgmVol <= 0.0001f) ? 0f : bgmVol;
            }

            float sfxVol = PlayerPrefs.GetFloat("SFXVol", 0.75f);
            if (sfxSlider != null)
            {
                sfxSlider.onValueChanged.RemoveAllListeners();
                sfxSlider.value = sfxVol;
                sfxSlider.onValueChanged.AddListener(SetSFXVolume);
            }

            bool isFullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
            if (fullscreenToggle != null)
            {
                fullscreenToggle.onValueChanged.RemoveAllListeners();
                fullscreenToggle.isOn = isFullscreen;
                fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
            }

            int fpsIndex = PlayerPrefs.GetInt("FPSLimitIndex", 0);
            if (fpsDropdown != null)
            {
                if (fpsDropdown.options.Count == 0)
                {
                    SetupFPSDropdown();
                }
                else
                {
                    if (fpsIndex < 0 || fpsIndex >= fpsDropdown.options.Count)
                    {
                        fpsIndex = 0;
                    }
                    fpsDropdown.onValueChanged.RemoveAllListeners();
                    fpsDropdown.value = fpsIndex;
                    fpsDropdown.RefreshShownValue();
                    fpsDropdown.onValueChanged.AddListener(SetFPSLimit);
                }
            }

            int savedResIndex = PlayerPrefs.GetInt("ResolutionIndex", 0);
            if (resolutionDropdown != null)
            {
                if (resolutionDropdown.options.Count == 0)
                {
                    SetupResolutionDropdown();
                }
                else
                {
                    if (savedResIndex < 0 || savedResIndex >= resolutionDropdown.options.Count)
                    {
                        savedResIndex = 0;
                    }
                    resolutionDropdown.onValueChanged.RemoveAllListeners();
                    resolutionDropdown.value = savedResIndex;
                    resolutionDropdown.RefreshShownValue();
                    resolutionDropdown.onValueChanged.AddListener(SetResolution);
                }
            }
        }
        finally
        {
            _isInitializing = false;
        }
    }
}