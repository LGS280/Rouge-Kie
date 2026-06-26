using UnityEngine;
using UnityEngine.Audio;

namespace RogueKie.Audio
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

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
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
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
            if (mainMixer == null) return;

            float clampedVolume = Mathf.Clamp(linearVolume, 0.0001f, 1f);
            float dbVolume = Mathf.Log10(clampedVolume) * 20f;
            mainMixer.SetFloat(parameterName, dbVolume);
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
        /// Phát âm thanh 3D tại một tọa độ xác định trong không gian (Ví dụ: tiếng súng, tiếng nổ)
        /// </summary>
        public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null) return;

            // Tạo ra một AudioSource tạm thời tại vị trí phát, tự động xóa sau khi phát xong clip
            // Âm thanh sẽ tự động nhỏ dần khi người chơi đi ra xa nguồn phát
            AudioSource.PlayClipAtPoint(clip, position, volume);
        }

        // Phục vụ nhanh cho hiệu ứng tương tác nút bấm
        public void PlayHoverSound() => PlaySFX(buttonHoverClip);
        public void PlayClickSound() => PlaySFX(buttonClickClip);
    }
}