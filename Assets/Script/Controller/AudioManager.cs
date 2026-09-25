using UnityEngine;
using UnityEngine.Audio;

namespace RogueKie.Audio
{
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager _instance;
        public static AudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<AudioManager>();
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Audio Mixer Reference")]
        [SerializeField] private AudioMixer mainMixer;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("UI SFX Clips")]
        [SerializeField] private AudioClip buttonHoverClip;
        [SerializeField] private AudioClip buttonClickClip;

        [Header("Default Music")]
        [SerializeField] private AudioClip menuBgmClip;

        private void Awake()
        {
            // Thiết lập DontDestroyOnLoad Singleton
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                SetupMixerGroups();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void SetupMixerGroups()
        {
            if (mainMixer == null) return;

            if (bgmSource != null && bgmSource.outputAudioMixerGroup == null)
            {
                AudioMixerGroup[] bgmGroups = mainMixer.FindMatchingGroups("BGM");
                if (bgmGroups != null && bgmGroups.Length > 0)
                {
                    bgmSource.outputAudioMixerGroup = bgmGroups[0];
                }
            }

            if (sfxSource != null && sfxSource.outputAudioMixerGroup == null)
            {
                AudioMixerGroup[] sfxGroups = mainMixer.FindMatchingGroups("SFX");
                if (sfxGroups != null && sfxGroups.Length > 0)
                {
                    sfxSource.outputAudioMixerGroup = sfxGroups[0];
                }
            }
        }

        private void Start()
        {
            // Khởi tạo mức âm lượng từ PlayerPrefs (giá trị tuyến tính mặc định là 0.75f)
            InitializeVolume("MasterVolume", "MasterVol", 0.75f);
            InitializeVolume("BGMVolume", "BGMVol", 0.60f);
            InitializeVolume("SFXVolume", "SFXVol", 0.75f);

            // Tự động phát nhạc nền Main Menu khi khởi động
            if (menuBgmClip != null)
            {
                PlayBGM(menuBgmClip);
            }
        }

        private void InitializeVolume(string mixerParam, string prefsKey, float defaultValue)
        {
            float savedVol = PlayerPrefs.GetFloat(prefsKey, defaultValue);
            SetVolume(mixerParam, savedVol);
        }

        // Chuyển đổi từ thang đo Tuyến tính [0.0001, 1] của Slider sang thang đo Logarit Decibel [-80, 20] của Mixer
        public void SetVolume(string parameterName, float linearVolume)
        {
            float clampedVolume = Mathf.Clamp(linearVolume, 0.0001f, 1f);

            if (mainMixer != null)
            {
                float dbVolume = Mathf.Log10(clampedVolume) * 20f;
                mainMixer.SetFloat(parameterName, dbVolume);
            }

            // Cơ chế bảo hiểm kép: can thiệp trực tiếp AudioListener và AudioSource
            if (parameterName == "MasterVolume")
            {
                AudioListener.volume = Mathf.Clamp01(linearVolume);
            }
            else if (parameterName == "BGMVolume" && bgmSource != null)
            {
                bgmSource.volume = clampedVolume;
            }
            else if (parameterName == "SFXVolume" && sfxSource != null)
            {
                sfxSource.volume = clampedVolume;
            }
        }

        // --- HÀM PHÁT BGM (NHẠC NỀN) ---
        public void PlayBGM(AudioClip clip)
        {
            if (bgmSource == null || clip == null) return;
            if (bgmSource.clip == clip && bgmSource.isPlaying) return;

            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.Play();
        }

        // --- HÀM PHÁT SFX (HIỆU ỨNG ÂM THANH) ---
        public void PlaySFX(AudioClip clip)
        {
            if (sfxSource == null || clip == null) return;
            sfxSource.PlayOneShot(clip);
        }

        /// <summary>
        /// Phát âm thanh 3D tại một tọa độ xác định trong không gian (Ví dụ: tiếng súng, tiếng nổ) có định tuyến qua Mixer SFX
        /// </summary>
        public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null) return;

            GameObject tempAudioObj = new GameObject("TempSFXAudio_" + clip.name);
            tempAudioObj.transform.position = position;

            AudioSource tempSource = tempAudioObj.AddComponent<AudioSource>();
            tempSource.clip = clip;
            tempSource.volume = Mathf.Clamp01(volume);
            tempSource.spatialBlend = 1f; // 3D sound
            tempSource.rolloffMode = AudioRolloffMode.Linear;
            tempSource.minDistance = 2f;
            tempSource.maxDistance = 25f;

            // Định tuyến qua Mixer SFX
            if (sfxSource != null && sfxSource.outputAudioMixerGroup != null)
            {
                tempSource.outputAudioMixerGroup = sfxSource.outputAudioMixerGroup;
            }
            else if (mainMixer != null)
            {
                AudioMixerGroup[] sfxGroups = mainMixer.FindMatchingGroups("SFX");
                if (sfxGroups != null && sfxGroups.Length > 0)
                {
                    tempSource.outputAudioMixerGroup = sfxGroups[0];
                }
            }

            tempSource.Play();
            Destroy(tempAudioObj, clip.length);
        }

        // Phục vụ nhanh cho hiệu ứng tương tác nút bấm
        public void PlayHoverSound() => PlaySFX(buttonHoverClip);
        public void PlayClickSound() => PlaySFX(buttonClickClip);
    }
}