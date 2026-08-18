using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RewardChest : MonoBehaviour
{
    [Header("thành phần của rương")]
    public GameObject body;
    public GameObject top;
    public GameObject inside;

    [Header("prefab coin và mana")]
    public GameObject coinPrefab;
    public GameObject manaPrefab;

    [Header("số lương mana và coin rơi ra")]
    public int minGold = 3;
    public int maxGold = 4;
    public int minMana = 3;
    public int maxMana = 4;
    [Range(0f, 1f)]
    public float weaponDropChance = 0.15f;

    private bool isOpened = false;

    private void Start()
    {

        if (body == null)
        {
            Transform bodyTrans = transform.Find("Body");
            if (bodyTrans != null) body = bodyTrans.gameObject;
        }

        if (top == null)
        {
            Transform topTrans = transform.Find("Top");
            if (topTrans != null) top = topTrans.gameObject;
        }

        if (inside == null)
        {
            Transform insideTrans = transform.Find("Inside");
            if (insideTrans != null) inside = insideTrans.gameObject;
        }

        EnforceSortingOrders();
    }

    private void EnforceSortingOrders()
    {
        if (body != null)
        {
            SpriteRenderer sr = body.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = 5;
        }

        if (top != null)
        {
            SpriteRenderer sr = top.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = 6;
        }

        if (inside != null)
        {
            SpriteRenderer sr = inside.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = 6;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isOpened) return;

        if (collision.CompareTag("Player"))
        {

            PlayerController player = collision.GetComponent<PlayerController>();
            if (player != null)
            {
                OpenChest();
            }
        }
    }

    public void OpenChest()
    {
        if (isOpened) return;

        isOpened = true;

        if (body != null) body.SetActive(true);
        if (inside != null) inside.SetActive(true);

        if (top != null)
        {
            StartCoroutine(AnimateTopChestOpen(top));
        }

        SpawnRewards();
    }

    private IEnumerator AnimateTopChestOpen(GameObject topObj)
    {
        SpriteRenderer sr = topObj.GetComponent<SpriteRenderer>();
        Vector3 startPos = topObj.transform.localPosition;
        Vector3 targetPos = startPos + new Vector3(0f, 0.4f, 0f);

        float duration = 0.55f;
        float elapsed = 0f;

        Color startColor = (sr != null) ? sr.color : Color.white;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            topObj.transform.localPosition = Vector3.Lerp(startPos, targetPos, t);

            if (sr != null)
            {
                Color c = startColor;
                c.a = Mathf.Lerp(1f, 0f, t);
                sr.color = c;
            }

            yield return null;
        }

        topObj.SetActive(false);
    }

    private void SpawnRewards()
    {
        float difficultyMultiplier = 1f;

        int goldToSpawn = Mathf.RoundToInt(Random.Range(minGold, maxGold + 1) * difficultyMultiplier);
        int manaToSpawn = Mathf.RoundToInt(Random.Range(minMana, maxMana + 1) * difficultyMultiplier);

        int totalItems = goldToSpawn + manaToSpawn;
        int index = 0;

        for (int i = 0; i < goldToSpawn; i++)
        {
            SpawnLootItem(coinPrefab, index, totalItems);
            index++;
        }

        for (int i = 0; i < manaToSpawn; i++)
        {
            SpawnLootItem(manaPrefab, index, totalItems);
            index++;
        }

        if (Random.value <= weaponDropChance)
        {
            SpawnWeaponReward();
        }
    }

    private void SpawnLootItem(GameObject prefab, int index, int totalItems)
    {
        if (prefab == null) return;

        Vector3 spawnPos = transform.position;
        GameObject lootObj = Instantiate(prefab, spawnPos, Quaternion.identity);

        Rigidbody2D rb = lootObj.GetComponent<Rigidbody2D>();
        if (rb != null)
        {

            rb.bodyType = RigidbodyType2D.Dynamic;

            rb.linearDamping = 6.0f;
            rb.linearVelocity = Vector2.zero;

            float angle = index * (360f / Mathf.Max(1, totalItems)) + Random.Range(-15f, 15f);
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

            float force = Random.Range(3.5f, 5.0f);
            rb.AddForce(direction * force, ForceMode2D.Impulse);
        }
    }

    private void SpawnWeaponReward()
    {

    }
}
