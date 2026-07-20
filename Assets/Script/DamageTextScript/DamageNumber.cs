using UnityEngine;
using TMPro;

public class DamageNumber : MonoBehaviour
{
    [SerializeField] float floatSpeed = 2f;
    [SerializeField] float lifetime = 0.8f;

    TextMeshPro tmp;
    float timer;
    Color startColor;

    void Awake()
    {
        tmp = GetComponent<TextMeshPro>();

        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingLayerName = "Default";
            mr.sortingOrder = 100;
        }

        transform.rotation = Camera.main.transform.rotation;
        // Bỏ startColor ở đây
    }

    public void Setup(int damage, bool isCrit = false)
    {
        Debug.Log("Setup called: damage=" + damage + " isCrit=" + isCrit); // ra ngoài if
        if (isCrit)
        {
            tmp.text = damage.ToString() + "!";
            tmp.fontSize = 5;
            tmp.color = Color.yellow;
        }
        else
        {
            tmp.text = damage.ToString();
            tmp.fontSize = 3;
            tmp.color = Color.red;
        }
        startColor = tmp.color;
    }

    public void SetColor(Color c)
    {
        if (tmp == null) tmp = GetComponent<TextMeshPro>();
        tmp.color = c;
        startColor = c;
    }

    public void SetText(string text)
    {
        if (tmp == null) tmp = GetComponent<TextMeshPro>();
        tmp.text = text;
    }

    void Update()
    {
        timer += Time.deltaTime;

        // Bay lên + lắc nhẹ ngang
        transform.position += new Vector3(0, floatSpeed * Time.deltaTime, 0);

        float alpha = Mathf.Lerp(1f, 0f, timer / lifetime);
        tmp.color = new Color(startColor.r, startColor.g, startColor.b, alpha);

        if (timer >= lifetime)
            Destroy(gameObject);
    }
}