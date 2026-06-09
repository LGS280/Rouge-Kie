using UnityEngine;

public class DamageNumberSpawner : MonoBehaviour
{
    public static DamageNumberSpawner Instance;
    [SerializeField] GameObject damageNumberPrefab;

    void Awake()
    {
        Instance = this;
        if (damageNumberPrefab == null)
            damageNumberPrefab = Resources.Load<GameObject>("DamageText 1");
    }

    public void Spawn(Vector3 position, int damage, bool isCrit = false)
    {
        Debug.Log("Spawn called: damage=" + damage + " isCrit=" + isCrit);
        Vector3 offset = new Vector3(Random.Range(-0.4f, 0.4f), 1f, 0f);
        GameObject obj = Instantiate(damageNumberPrefab, position + offset, Quaternion.identity);
        DamageNumber dn = obj.GetComponent<DamageNumber>();
        Debug.Log("DamageNumber component: " + dn);
        if (dn != null) dn.Setup(damage, isCrit);
    }
}