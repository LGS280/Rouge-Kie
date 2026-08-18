using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum LootType
{
    Coin,
    Mana
}

public class LootItem : MonoBehaviour
{
    [Header("cấu hình nhặt đồ")]
    public LootType lootType = LootType.Coin;
    public int value = 1;

    [Header("năm chăm hút coin và mana")]
    public float detectRange = 4.0f;
    public float baseAttractSpeed = 3.0f;
    public float acceleration = 5.0f;
    public float attractDelay = 0.5f;
    public float pickupDelay = 0.5f;

    private Transform playerTransform;
    private Rigidbody2D rb;
    private float spawnTime;
    private float currentSpeed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        spawnTime = Time.time;
        currentSpeed = baseAttractSpeed;
    }

    private void Update()
    {

        if (Time.time - spawnTime < attractDelay) return;

        FindLocalPlayer();

        if (playerTransform != null)
        {
            float distance = Vector3.Distance(transform.position, playerTransform.position);

            if (distance <= detectRange)
            {

                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.bodyType = RigidbodyType2D.Kinematic;
                }

                currentSpeed += acceleration * Time.deltaTime;

                transform.position = Vector3.MoveTowards(transform.position, playerTransform.position, currentSpeed * Time.deltaTime);
            }
        }
    }

    private void FindLocalPlayer()
    {
        if (playerTransform == null)
        {
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        TryCollect(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        TryCollect(collision);
    }

    private void TryCollect(Collider2D collision)
    {

        if (Time.time - spawnTime < pickupDelay) return;

        if (collision.CompareTag("Player"))
        {
            RookieHealth playerHealth = collision.GetComponent<RookieHealth>();
            if (playerHealth == null)
            {
                playerHealth = collision.GetComponentInParent<RookieHealth>();
            }

            if (playerHealth != null && !playerHealth.isDead)
            {
                CollectLoot(playerHealth);
            }
        }
    }

    private void CollectLoot(RookieHealth playerHealth)
    {
        if (lootType == LootType.Coin)
        {

            if (RunStatsTracker.Instance != null)
            {
                RunStatsTracker.Instance.AddCurrency(value);
            }
        }
        else if (lootType == LootType.Mana)
        {

            playerHealth.RestoreMana(value);
        }

        Destroy(gameObject);
    }
}
