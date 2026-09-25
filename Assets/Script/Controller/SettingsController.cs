using UnityEngine;
using UnityEngine.UI;
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

    // --- CẤU HÌNH ÂM THANH (Sẽ kết nối với Audio Mixer sau) ---

    // Thay thế các hàm SetVolume cũ trong SettingsController.cs bằng code này:

    public void SetMasterVolume(float volume)
    {
        if (_isInitializing) return;
        PlayerPrefs.SetFloat("MasterVol", volume);
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
        if (bgmSource != null) bgmSource.volume = volume;
        if (RogueKie.Audio.AudioManager.Instance != null)
        {
            RogueKie.Audio.AudioManager.Instance.SetVolume("BGMVolume", volume);
        }
    }

    public void SetSFXVolume(float volume)
    {
        if (_isInitializing) return;
        PlayerPrefs.SetFloat("SFXVol", volume);
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

            // Tắt event trước khi set value để tránh trigger
            float masterVol = PlayerPrefs.GetFloat("MasterVol", 0.75f);
            if (masterSlider != null)
            {
                masterSlider.onValueChanged.RemoveAllListeners();
                masterSlider.value = masterVol;
                masterSlider.onValueChanged.AddListener(SetMasterVolume);
            }
            AudioListener.volume = Mathf.Clamp01(masterVol);
            if (RogueKie.Audio.AudioManager.Instance != null)
            {
                RogueKie.Audio.AudioManager.Instance.SetVolume("MasterVolume", masterVol);
            }

            float bgmVol = PlayerPrefs.GetFloat("BGMVol", 0.60f);
            if (bgmSlider != null)
            {
                bgmSlider.onValueChanged.RemoveAllListeners();
                bgmSlider.value = bgmVol;
                bgmSlider.onValueChanged.AddListener(SetBGMVolume);
                if (bgmSource != null) bgmSource.volume = bgmVol;
            }
            if (RogueKie.Audio.AudioManager.Instance != null)
            {
                RogueKie.Audio.AudioManager.Instance.SetVolume("BGMVolume", bgmVol);
            }

            float sfxVol = PlayerPrefs.GetFloat("SFXVol", 0.75f);
            if (sfxSlider != null)
            {
                sfxSlider.onValueChanged.RemoveAllListeners();
                sfxSlider.value = sfxVol;
                sfxSlider.onValueChanged.AddListener(SetSFXVolume);
            }
            if (RogueKie.Audio.AudioManager.Instance != null)
            {
                RogueKie.Audio.AudioManager.Instance.SetVolume("SFXVolume", sfxVol);
            }

            // Phần còn lại giữ nguyên
            bool isFullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
            if (fullscreenToggle != null)
            {
                fullscreenToggle.onValueChanged.RemoveAllListeners();
                fullscreenToggle.isOn = isFullscreen;
                fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
            }
            Screen.fullScreenMode = isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

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
            SetFPSLimit(fpsIndex);
        }
        finally
        {
            _isInitializing = false;
        }
    }
}