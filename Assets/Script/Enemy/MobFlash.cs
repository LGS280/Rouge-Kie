using System.Collections.Generic;
using UnityEngine;

public class MobFlash : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Material originalMaterial;
    public Material flashMaterial; // Tạo một Material sử dụng Shader Flash White
    public float flashDuration = 0.1f;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalMaterial = spriteRenderer.material;
        }
    }

    public void TriggerFlash()
    {
        if (spriteRenderer != null && flashMaterial != null)
        {
            StopAllCoroutines();
            StartCoroutine(FlashRoutine());
        }
    }

    private System.Collections.IEnumerator FlashRoutine()
    {
        spriteRenderer.material = flashMaterial;
        yield return new WaitForSeconds(flashDuration);
        spriteRenderer.material = originalMaterial;
    }
}