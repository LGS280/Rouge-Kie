using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsController : MonoBehaviour
{
    [Header("Audio UI")]
    public Slider masterSlider;
    public Slider bgmSlider;
    public Slider sfxSlider;

    [Header("Graphics UI")]
    public Toggle fullscreenToggle;
    public TMP_Dropdown resolutionDropdown;
    public TMP_Dropdown fpsDropdown;

    private Resolution[] resolutions;

    private void Start()
    {
        SetupResolutionDropdown();
        LoadSettings();
    }

    // --- CẤU HÌNH ĐỒ HỌA ---

    private void SetupResolutionDropdown()
    {
        if (resolutionDropdown == null) return;

        resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        System.Collections.Generic.List<string> options = new System.Collections.Generic.List<string>();
        int currentResolutionIndex = 0;

        for (int i = 0; i < resolutions.Length; i++)
        {
            string option = resolutions[i].width + " x " + resolutions[i].height;
            options.Add(option);

            if (resolutions[i].width == Screen.currentResolution.width &&
                resolutions[i].height == Screen.currentResolution.height)
            {
                currentResolutionIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);

        // Load lại độ phân giải đã lưu hoặc đặt mặc định
        int savedResIndex = PlayerPrefs.GetInt("ResolutionIndex", currentResolutionIndex);
        resolutionDropdown.value = savedResIndex;
        resolutionDropdown.RefreshShownValue();
    }

    public void SetResolution(int resolutionIndex)
    {
        Resolution resolution = resolutions[resolutionIndex];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
        PlayerPrefs.SetInt("ResolutionIndex", resolutionIndex);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
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

    public void SetMasterVolume(float volume)
    {
        PlayerPrefs.SetFloat("MasterVol", volume);
        // Code điều chỉnh AudioMixer sẽ bổ sung ở bước sau
    }

    public void SetBGMVolume(float volume)
    {
        PlayerPrefs.SetFloat("BGMVol", volume);
    }

    public void SetSFXVolume(float volume)
    {
        PlayerPrefs.SetFloat("SFXVol", volume);
    }

    // --- LOAD CÀI ĐẶT KHI KHỞI ĐỘNG GAME ---

    private void LoadSettings()
    {
        // Đồ họa
        bool isFullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        if (fullscreenToggle != null) fullscreenToggle.isOn = isFullscreen;
        Screen.fullScreen = isFullscreen;

        int fpsIndex = PlayerPrefs.GetInt("FPSLimitIndex", 1); // Mặc định 60 FPS
        if (fpsDropdown != null) fpsDropdown.value = fpsIndex;
        SetFPSLimit(fpsIndex);

        // Âm thanh
        if (masterSlider != null) masterSlider.value = PlayerPrefs.GetFloat("MasterVol", 0.8f);
        if (bgmSlider != null) bgmSlider.value = PlayerPrefs.GetFloat("BGMVol", 0.6f);
        if (sfxSlider != null) sfxSlider.value = PlayerPrefs.GetFloat("SFXVol", 0.8f);
    }
}