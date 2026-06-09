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
        // Tạo nhanh một Material dùng "GUI/Text Shader" để chớp trắng tinh mà không cần tạo file Shader mới
        flashMaterial = new Material(Shader.Find("GUI/Text Shader"));

    }

    public void TriggerFlash()
    {
        //if (spriteRenderer != null && flashMaterial != null)
        //{
        //    StopAllCoroutines();
        //    StartCoroutine(FlashRoutine());
        //}

        // Kiểm tra xem quái có đang hiển thị và hoạt động không trước khi chạy Coroutine
        if (spriteRenderer != null && gameObject.activeInHierarchy)
        {
            StartCoroutine(FlashRoutine());
        }
    }

    private System.Collections.IEnumerator FlashRoutine()
    {
        //spriteRenderer.material = flashMaterial;
        //yield return new WaitForSeconds(flashDuration);
        //spriteRenderer.material = originalMaterial;

        // Chuyển sang Material chớp trắng
        spriteRenderer.material = flashMaterial;
        spriteRenderer.color = Color.white; // Đảm bảo giữ màu trắng tinh của shader

        yield return new WaitForSeconds(0.1f); // Thời gian chớp (0.1 giây)

        // Trả lại Material và màu sắc ban đầu của quái
        spriteRenderer.material = originalMaterial;
        spriteRenderer.color = Color.white;
    }
}