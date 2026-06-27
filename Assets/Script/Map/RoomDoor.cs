using UnityEngine;
using System.Collections;

public class RoomDoor : MonoBehaviour
{
    public Sprite openSpriteLower;
    public Sprite closedSpriteLower;
    public Sprite openSpriteUpper;
    public Sprite closedSpriteUpper;
    public SpriteRenderer upperSpriteRenderer;

    private SpriteRenderer lowerSpriteRenderer;
    private BoxCollider2D physicsCollider;
    private BoxCollider2D triggerCollider;
    private bool isClosed = false;

    private RoomController ownerRoom;

    private void Awake()
    {
        lowerSpriteRenderer = GetComponent<SpriteRenderer>();

        BoxCollider2D[] colliders = GetComponents<BoxCollider2D>();
        foreach (var col in colliders)
        {
            if (!col.isTrigger)
                physicsCollider = col;
            else
                triggerCollider = col;
        }

        OpenDoor();
    }

    public void SetOwnerRoom(RoomController room)
    {
        ownerRoom = room;
    }

    public void SetupTriggerCollider(Vector2 size, Vector2 offset)
    {
        if (triggerCollider == null)
        {
            BoxCollider2D[] colliders = GetComponents<BoxCollider2D>();
            foreach (var col in colliders)
            {
                if (col.isTrigger)
                    triggerCollider = col;
            }
        }

        if (triggerCollider != null)
        {
            triggerCollider.size = size;
            triggerCollider.offset = offset;
        }
    }

    public void CloseDoor()
    {
        if (isClosed) return;
        isClosed = true;

        if (lowerSpriteRenderer != null) lowerSpriteRenderer.sprite = closedSpriteLower;
        if (upperSpriteRenderer != null) upperSpriteRenderer.sprite = closedSpriteUpper;

        // C?a ?óng: ch?n Player + ch?n ??n
        if (physicsCollider != null)
        {
            physicsCollider.enabled = true;
            physicsCollider.isTrigger = false;
        }
    }

    public void OpenDoor()
    {
        isClosed = false;

        if (lowerSpriteRenderer != null) lowerSpriteRenderer.sprite = openSpriteLower;
        if (upperSpriteRenderer != null) upperSpriteRenderer.sprite = openSpriteUpper;

        // C?a m?: v?n cho ??n detect, nh?ng không ch?n Player v?t lý
        if (physicsCollider != null)
        {
            physicsCollider.enabled = true;
            physicsCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && !isClosed)
        {
            StartCoroutine(CheckBeforeClose(collision.transform));
        }
    }

    private IEnumerator CheckBeforeClose(Transform playerTransform)
    {
        yield return new WaitForSeconds(0.25f);

        if (playerTransform == null || ownerRoom == null)
            yield break;

        float distance = Vector2.Distance(transform.position, playerTransform.position);

        if (distance > 0.8f)
        {
            ownerRoom.TryStartRoomCombat();
        }
    }
}