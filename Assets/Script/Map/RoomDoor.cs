using UnityEngine;
using System.Collections;

public class RoomDoor : MonoBehaviour
{
    [Header("C?u h?nh Kh?i D??i (Th?n C?a)")]
    public Sprite openSpriteLower;
    public Sprite closedSpriteLower;

    [Header("C?u h?nh Kh?i Tr?n (M?i C?a)")]
    public Sprite openSpriteUpper;
    public Sprite closedSpriteUpper;

    [Header("Object Con Gi? M?i C?a")]
    public SpriteRenderer upperSpriteRenderer;

    private SpriteRenderer lowerSpriteRenderer;
    private BoxCollider2D physicsCollider;
    private BoxCollider2D triggerCollider; // L?u c?i collider l?m trigger
    private bool isClosed = false;

    private void Awake()
    {
        lowerSpriteRenderer = GetComponent<SpriteRenderer>();

        BoxCollider2D[] colliders = GetComponents<BoxCollider2D>();
        foreach (var col in colliders)
        {
            if (!col.isTrigger)
                physicsCollider = col;
            else
                triggerCollider = col; // T?m ra c?i Trigger
        }

        OpenDoor();
    }

    // H?m n?y gi?p DungeonGenerator t? ??ng c?u h?nh l?i Trigger theo t?ng h??ng c?a
    public void SetupTriggerCollider(Vector2 size, Vector2 offset)
    {
        // N?u ch?a k?p l?y ? Awake th? t?m l?i cho ch?c
        if (triggerCollider == null)
        {
            BoxCollider2D[] colliders = GetComponents<BoxCollider2D>();
            foreach (var col in colliders) { if (col.isTrigger) triggerCollider = col; }
        }

        if (triggerCollider != null)
        {
            triggerCollider.size = size;
            triggerCollider.offset = offset;
        }
    }

    [ContextMenu("Close Door")]
    public void CloseDoor()
    {
        if (isClosed) return;
        isClosed = true;

        if (lowerSpriteRenderer != null) lowerSpriteRenderer.sprite = closedSpriteLower;
        if (upperSpriteRenderer != null) upperSpriteRenderer.sprite = closedSpriteUpper;

        if (physicsCollider != null) physicsCollider.enabled = true;
    }

    [ContextMenu("Open Door")]
    public void OpenDoor()
    {
        isClosed = false;

        if (lowerSpriteRenderer != null) lowerSpriteRenderer.sprite = openSpriteLower;
        if (upperSpriteRenderer != null) upperSpriteRenderer.sprite = openSpriteUpper;

        if (physicsCollider != null) physicsCollider.enabled = false;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && !isClosed)
        {
            // ??i 0.25 gi?y xem player c? th?c s? ?i v?o ph?ng kh?ng r?i m?i kh?a
            StartCoroutine(CheckBeforeClose(collision.transform));
        }
    }

    private IEnumerator CheckBeforeClose(Transform playerTransform)
    {
        yield return new WaitForSeconds(0.25f);

        if (playerTransform != null)
        {
            // T?nh kho?ng c?ch gi?a Player v? ? c?a
            float distance = Vector2.Distance(transform.position, playerTransform.position);

            // N?u ?i xa qu? 0.8 ? ngh?a l? ?? v?o h?n trong ph?ng -> Kh?a to?n b?!
            if (distance > 0.8f)
            {
                CloseAllDoorsInRoom();
            }
        }
    }

    private void CloseAllDoorsInRoom()
    {
        RoomDoor[] allDoors = Object.FindObjectsByType<RoomDoor>(FindObjectsSortMode.None);
        foreach (RoomDoor door in allDoors)
        {
            if (door != null) door.CloseDoor();
        }
    }
}