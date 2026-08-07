using System.Collections;
using UnityEngine;

namespace RogueKie.Effects
{
    public class AutoDestroyEffect : MonoBehaviour
    {
        [Header("Animation Frames")]
        public Sprite[] animationFrames;      // Danh sách các frame ảnh vụ nổ (Exlpsion_Effect_1 -> 5)
        public float frameDuration = 0.1f;    // Thời gian mỗi frame (0.1s x 5 frames = 0.5s tổng thời gian nổ)

        [Header("Audio SFX")]
        public AudioClip soundClip;          // Âm thanh nổ (tùy chọn)
        public float soundVolume = 0.8f;

        private SpriteRenderer spriteRenderer;

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
        }

        void Start()
        {
            // Phát âm thanh nổ nếu có gán file âm thanh
            if (soundClip != null && RogueKie.Audio.AudioManager.Instance != null)
            {
                RogueKie.Audio.AudioManager.Instance.PlaySFXAtPosition(soundClip, transform.position, soundVolume);
            }

            // Bắt đầu chuỗi phát hoạt ảnh nổ
            if (animationFrames != null && animationFrames.Length > 0)
            {
                StartCoroutine(PlayAnimationRoutine());
            }
            else
            {
                // Nếu không có frame ảnh, tự xóa sau 0.5s làm fallback
                Destroy(gameObject, 0.5f);
            }
        }

        IEnumerator PlayAnimationRoutine()
        {
            for (int i = 0; i < animationFrames.Length; i++)
            {
                if (spriteRenderer != null && animationFrames[i] != null)
                {
                    spriteRenderer.sprite = animationFrames[i];
                }
                yield return new WaitForSeconds(frameDuration);
            }

            // Xóa hiệu ứng vụ nổ khỏi Scene ngay sau khi phát xong frame cuối
            Destroy(gameObject);
        }
    }
}
