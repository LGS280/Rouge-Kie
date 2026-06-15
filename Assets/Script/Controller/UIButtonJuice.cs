using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonJuice : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    ISelectHandler, IDeselectHandler // Chỉ giữ lại hiệu ứng Scale để tương thích với Layout Group
{
    private Vector3 originalScale;

    [Header("Settings")]
    public float hoverScaleMultiplier = 1.08f; // Phóng to 8% khi chọn

    void Start()
    {
        originalScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
        {
            return;
        }
        ResetJuice();
    }

    public void OnSelect(BaseEventData eventData)
    {
        ApplyHoverEffect();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        ResetJuice();
    }

    private void ApplyHoverEffect()
    {
        transform.localScale = originalScale * hoverScaleMultiplier;
    }

    private void ResetJuice()
    {
        transform.localScale = originalScale;
    }
}