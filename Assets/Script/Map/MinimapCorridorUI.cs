using UnityEngine;
using UnityEngine.UI;

public class MinimapCorridorUI : MonoBehaviour
{
    [Header("UI Components")]
    public Image corridorImage;

    /// <summary>
    /// Cập nhật hiển thị đường hành lang nối giữa 2 phòng trên Minimap
    /// </summary>
    /// <param name="isVisible">Đã khám phá ít nhất 1 trong 2 phòng nối bởi hành lang này</param>
    /// <param name="isVisited">Cả 2 phòng nối ở 2 đầu hành lang đều đã đi qua</param>
    public void SetState(bool isVisible, bool isVisited)
    {
        if (!isVisible)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (corridorImage != null)
        {
            if (isVisited)
            {
                // Cả 2 phòng đã đi qua -> Màu xám sáng trắng rõ nét
                corridorImage.color = new Color(0.85f, 0.85f, 0.9f, 0.9f);
            }
            else
            {
                // Mới mở 1 phòng kề cạnh -> Màu tối dịu nhẹ
                corridorImage.color = new Color(0.35f, 0.4f, 0.5f, 0.65f);
            }
        }
    }
}
