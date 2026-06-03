using UnityEngine;
using System.Collections;

public class RoomDoor : MonoBehaviour
{
    [Header("C?u hình Kh?i D??i (Thân C?a)")]
    public Sprite openSpriteLower;
    public Sprite closedSpriteLower;

    [Header("C?u hình Kh?i Trên (Mái C?a)")]
    public Sprite openSpriteUpper;
    public Sprite closedSpriteUpper;

    [Header("Object Con Gi? Mái C?a")]
    public SpriteRenderer upperSpriteRenderer;

    private SpriteRenderer lowerSpriteRenderer;
    private BoxCollider2D physicsCollider;
    private BoxCollider2D triggerCollider; // L?u cái collider làm trigger
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
                triggerCollider = col; // Tìm ra cái Trigger
        }

        OpenDoor();
    }

    // Hàm này giúp DungeonGenerator t? ??ng c?u hình l?i Trigger theo t?ng h??ng c?a
    public void SetupTriggerCollider(Vector2 size, Vector2 offset)
    {
        // N?u ch?a k?p l?y ? Awake thì tìm l?i cho ch?c
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
            // ??i 0.25 giây xem player có th?c s? ?i vào phòng không r?i m?i khóa
            StartCoroutine(CheckBeforeClose(collision.transform));
        }
    }

    private IEnumerator CheckBeforeClose(Transform playerTransform)
    {
        yield return new WaitForSeconds(0.25f);

        if (playerTransform != null)
        {
            // Tính kho?ng cách gi?a Player và ô c?a
            float distance = Vector2.Distance(transform.position, playerTransform.position);

            // N?u ?i xa quá 0.8 ô ngh?a là ?ã vào h?n trong phòng -> Khóa toàn b?!
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