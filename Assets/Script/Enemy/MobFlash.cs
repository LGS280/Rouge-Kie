using System.Collections.Generic;
using UnityEngine;

public class MobFlash : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Material originalMaterial;
    public Material flashMaterial;
    public float flashDuration = 0.1f;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalMaterial = spriteRenderer.material;
        }

        flashMaterial = new Material(Shader.Find("GUI/Text Shader"));

    }

    public void TriggerFlash()
    {

        if (spriteRenderer != null && gameObject.activeInHierarchy)
        {
            StartCoroutine(FlashRoutine());
        }
    }

    private System.Collections.IEnumerator FlashRoutine()
    {

        spriteRenderer.material = flashMaterial;
        spriteRenderer.color = Color.white;

        yield return new WaitForSeconds(0.1f);

        spriteRenderer.material = originalMaterial;
        spriteRenderer.color = Color.white;
    }
}
