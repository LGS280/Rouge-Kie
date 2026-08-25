using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class CutsceneManager : MonoBehaviour
{
    [SerializeField] Sprite[] pages;
    [SerializeField] Image pageLeft;
    [SerializeField] Image pageRight;
    [SerializeField] float fadeDuration = 0.5f;
    [SerializeField] float delayBetweenClick = 0.3f;
    [SerializeField] string nextSceneName = "Scene_Menu";

    int currentIndex = 0; // ảnh tiếp theo sẽ hiện
    bool isLeft = true;   // lần click đầu hiện trái, rồi phải, xen kẽ
    bool isTransitioning = false;

    CanvasGroup leftGroup;
    CanvasGroup rightGroup;

    void Start()
    {
        PlayerPrefs.DeleteKey("CutsceneSeen");
        Debug.Log("CutsceneManager Start chạy");
        Debug.Log("pageLeft: " + pageLeft);
        Debug.Log("pageRight: " + pageRight);
        Debug.Log("pages length: " + pages.Length);

        if (PlayerPrefs.GetInt("CutsceneSeen", 0) == 1)
        {
            SceneManager.LoadScene(nextSceneName);
            return;
        }

        // Setup CanvasGroup cho từng ảnh
        leftGroup = GetOrAddCanvasGroup(pageLeft);
        rightGroup = GetOrAddCanvasGroup(pageRight);

        // Đảm bảo màu ảnh luôn trắng, không đục alpha ở đây nữa
        pageLeft.color = Color.white;
        pageRight.color = Color.white;

        // Ẩn cả 2 lúc đầu bằng CanvasGroup, không phải Image.color
        leftGroup.alpha = 0f;
        rightGroup.alpha = 0f;
    }

    CanvasGroup GetOrAddCanvasGroup(Image img)
    {
        var cg = img.GetComponent<CanvasGroup>();
        if (cg == null) cg = img.gameObject.AddComponent<CanvasGroup>();
        Debug.Log("CanvasGroup for " + img.name + ": " + cg + " alpha: " + cg.alpha);
        return cg;
    }
    public void OnScreenClicked()
    {
      
        if (isTransitioning) return;
        if (currentIndex >= pages.Length)
        {
            StartCoroutine(EndCutscene());
            return;
        }

        StartCoroutine(ShowNextImage());
    }

    IEnumerator ShowNextImage()
    {
        isTransitioning = true;
        Debug.Log("ShowNextImage - currentIndex: " + currentIndex + " isLeft: " + isLeft);

        if (isLeft)
        {
            if (currentIndex > 0)
                yield return StartCoroutine(FadeImage(leftGroup, 1f, 0f));

            yield return new WaitForSeconds(delayBetweenClick);

            pageLeft.sprite = pages[currentIndex];
            Debug.Log("Set pageLeft sprite: " + pages[currentIndex].name);
            yield return StartCoroutine(FadeImage(leftGroup, 0f, 1f));
        }
        else
        {
            if (currentIndex > 1)
                yield return StartCoroutine(FadeImage(rightGroup, 1f, 0f));

            yield return new WaitForSeconds(delayBetweenClick);

            pageRight.sprite = pages[currentIndex];
            Debug.Log("Set pageRight sprite: " + pages[currentIndex].name);
            yield return StartCoroutine(FadeImage(rightGroup, 0f, 1f));
        }

        currentIndex++;
        isLeft = !isLeft;
        isTransitioning = false;
    }

    IEnumerator FadeImage(CanvasGroup cg, float from, float to)
    {
        Debug.Log("FadeImage start - from: " + from + " to: " + to + " cg: " + cg);
        float t = 0f;
        cg.alpha = from;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        cg.alpha = to;
        Debug.Log("FadeImage end - alpha: " + cg.alpha);
    }

    IEnumerator EndCutscene()
    {
        isTransitioning = true;
        yield return StartCoroutine(FadeImage(leftGroup, 1f, 0f));
        yield return StartCoroutine(FadeImage(rightGroup, 1f, 0f));
        PlayerPrefs.SetInt("CutsceneSeen", 1);
        PlayerPrefs.Save();
        SceneManager.LoadScene(nextSceneName);
    }
}