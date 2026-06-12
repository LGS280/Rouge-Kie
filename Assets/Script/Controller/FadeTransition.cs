using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FadeTransition : MonoBehaviour
{
    private CanvasGroup canvasGroup;
    public float fadeDuration = 1.0f; // Thời gian mờ dần (1 giây)

    void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        // Khi bắt đầu mở Game, màn hình đen sẽ tự mờ dần để lộ Menu ra
        StartCoroutine(Fade(1, 0, null));
    }

    public void TransitionToScene(string sceneName)
    {
        // Khi chuyển sang màn chơi, màn hình sẽ tối dần rồi mới Load Scene
        StartCoroutine(Fade(0, 1, () => {
            SceneManager.LoadScene(sceneName);
        }));
    }

    private IEnumerator Fade(float startAlpha, float endAlpha, System.Action onComplete)
    {
        float elapsedTime = 0f;
        canvasGroup.alpha = startAlpha;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsedTime / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = endAlpha;
        // Chặn tương tác chuột khi màn hình đang đen hoàn toàn
        canvasGroup.blocksRaycasts = (endAlpha == 1);

        onComplete?.Invoke();
    }
}