using UnityEngine;

public class DamageNumberSpawner : MonoBehaviour
{
    public static DamageNumberSpawner Instance;

    [SerializeField] GameObject damageNumberPrefab;

    void Awake()
    {
        Instance = this;
    }

    public void Spawn(Vector3 position, int damage, bool isCrit = false)
    {
        // Offset ngẫu nhiên một chút để không chồng lên nhau
        Vector3 offset = new Vector3(Random.Range(-0.3f, 0.3f), 0.5f, 0f);
        GameObject obj = Instantiate(damageNumberPrefab, position + offset, Quaternion.identity);
        obj.GetComponent<DamageNumber>().Setup(damage, isCrit);
    }
}