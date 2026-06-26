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

    private void Awake()
    {
        if (resolutionDropdown != null)
            SetupResolutionDropdown();
        else
            Debug.LogError("resolutionDropdown chưa được gán trong Inspector!");
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

    public void SetFPSLimit(int index)
    {
        switch (index)
        {
            case 0: Application.targetFrameRate = 30; break;
            case 1: Application.targetFrameRate = 60; break;
            case 2: Application.targetFrameRate = 120; break;
            case 3: Application.targetFrameRate = -1; break; // Không giới hạn
        }
        PlayerPrefs.SetInt("FPSLimitIndex", index);
    }

    // --- CẤU HÌNH ÂM THANH (Sẽ kết nối với Audio Mixer sau) ---

    // Thay thế các hàm SetVolume cũ trong SettingsController.cs bằng code này:

    public void SetMasterVolume(float volume)
    {
        PlayerPrefs.SetFloat("MasterVol", volume);
        if (RogueKie.Audio.AudioManager.Instance != null)
        {
            RogueKie.Audio.AudioManager.Instance.SetVolume("MasterVolume", volume);
        }
    }

    public void SetBGMVolume(float volume)
    {
        PlayerPrefs.SetFloat("BGMVol", volume);
        if (RogueKie.Audio.AudioManager.Instance != null)
        {
            RogueKie.Audio.AudioManager.Instance.SetVolume("BGMVolume", volume);
        }
    }

    public void SetSFXVolume(float volume)
    {
        PlayerPrefs.SetFloat("SFXVol", volume);
        if (RogueKie.Audio.AudioManager.Instance != null)
        {
            RogueKie.Audio.AudioManager.Instance.SetVolume("SFXVolume", volume);
        }
    }

    // --- LOAD CÀI ĐẶT KHI KHỞI ĐỘNG GAME ---

    private void LoadSettings()
    {
        // Tắt event trước khi set value để tránh trigger
        if (bgmSlider != null)
        {
            bgmSlider.onValueChanged.RemoveAllListeners();
            bgmSlider.value = PlayerPrefs.GetFloat("BGMVol", 1f);
            bgmSlider.onValueChanged.AddListener(SetBGMVolume); // gắn lại sau
            if (bgmSource != null) bgmSource.volume = bgmSlider.value;
        }

        if (masterSlider != null)
        {
            masterSlider.onValueChanged.RemoveAllListeners();
            masterSlider.value = PlayerPrefs.GetFloat("MasterVol", 1f);
            masterSlider.onValueChanged.AddListener(SetMasterVolume);
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveAllListeners();
            sfxSlider.value = PlayerPrefs.GetFloat("SFXVol", 1f);
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        }

        // Phần còn lại giữ nguyên
        bool isFullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = isFullscreen;
            fullscreenToggle.onValueChanged.RemoveAllListeners();
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }
        Screen.fullScreenMode = isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        int fpsIndex = PlayerPrefs.GetInt("FPSLimitIndex", 1);
        if (fpsDropdown != null)
        {
            fpsDropdown.value = fpsIndex;
            fpsDropdown.onValueChanged.RemoveAllListeners();
            fpsDropdown.onValueChanged.AddListener(SetFPSLimit);
        }
        SetFPSLimit(fpsIndex);
    }
}