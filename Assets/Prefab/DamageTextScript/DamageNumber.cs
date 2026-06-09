using UnityEngine;
using TMPro;

public class DamageNumber : MonoBehaviour
{
    [SerializeField] float floatSpeed = 1.5f;
    [SerializeField] float lifetime = 0.8f;

    TextMeshProUGUI tmp;
    float timer;
    Color startColor;

    void Awake()
    {
        tmp = GetComponent<TextMeshProUGUI>();
        startColor = tmp.color;
    }

    public void Setup(int damage, bool isCrit = false)
    {
        if (isCrit)
        {
            tmp.text = damage.ToString() + "!";
            tmp.fontSize = 7;           // to hơn
            tmp.color = Color.yellow;
            startColor = Color.yellow;
        }
        else
        {
            tmp.text = damage.ToString();
            tmp.fontSize = 5;
            tmp.color = Color.white;
            startColor = Color.white;
        }
    }

    void Update()
    {
        timer += Time.deltaTime;

        // Bay lên
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;

        // Fade out
        float alpha = Mathf.Lerp(1f, 0f, timer / lifetime);
        tmp.color = new Color(startColor.r, startColor.g, startColor.b, alpha);

        if (timer >= lifetime)
            Destroy(gameObject);
    }
}