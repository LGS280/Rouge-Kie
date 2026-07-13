using UnityEngine;
using UnityEngine.UI;

public class MinimapRoomUI : MonoBehaviour
{
    [Header("UI Components")]
    public Image roomBackground;    // Ảnh nền phòng (Room.png)
    public Image roomIcon;          // Ảnh icon loại phòng (Boss.png, Chest.png, Home.png)
    public GameObject playerIndicator; // Điểm chỉ hướng hoặc hình tròn đỏ thể hiện vị trí người chơi

    /// <summary>
    /// Khởi tạo các sprite cho phòng trên Minimap
    /// </summary>
    public void Setup(Sprite bgSprite, Sprite iconSprite)
    {
        if (roomBackground != null)
        {
            roomBackground.sprite = bgSprite;
        }

        if (roomIcon != null)
        {
            roomIcon.sprite = iconSprite;
            roomIcon.gameObject.SetActive(iconSprite != null);
        }

        if (playerIndicator != null)
        {
            playerIndicator.SetActive(false);
        }
    }

    /// <summary>
    /// Cập nhật hiển thị phòng dựa trên trạng thái khám phá (kiểu The Binding of Isaac)
    /// </summary>
    /// <param name="isCurrent">Người chơi hiện đang đứng ở phòng này</param>
    /// <param name="isVisited">Người chơi đã từng đi qua phòng này</param>
    /// <param name="isCleared">Phòng này đã dọn sạch quái</param>
    /// <param name="isAdjacentToVisited">Phòng kề cận phòng đã đi qua (đã mở đường nhưng chưa vào)</param>
    public void SetState(bool isCurrent, bool isVisited, bool isCleared, bool isAdjacentToVisited)
    {
        if (isCurrent)
        {
            // Người chơi đang đứng tại phòng này -> Sáng trắng hoàn toàn
            gameObject.SetActive(true);
            SetAlpha(1.0f);
            SetBackgroundColor(Color.white);
            
            bool hasSpecialIcon = false;
            if (roomIcon != null)
            {
                hasSpecialIcon = (roomIcon.sprite != null);
                roomIcon.gameObject.SetActive(hasSpecialIcon);
                roomIcon.color = Color.white; // Phục hồi màu icon gốc
            }

            if (playerIndicator != null)
            {
                // Chỉ hiển thị chấm đỏ người chơi khi phòng này KHÔNG có icon đặc biệt (tránh bị đè đè lên Home/Boss/Chest)
                playerIndicator.SetActive(!hasSpecialIcon);
            }
        }
        else if (isVisited)
        {
            // Đã đi qua và dọn dẹp hoặc chỉ mới đi qua -> Sáng vừa (xám nhạt), ẩn chấm đỏ
            gameObject.SetActive(true);
            SetAlpha(1.0f);
            
            if (isCleared)
            {
                SetBackgroundColor(new Color(0.75f, 0.75f, 0.75f, 1.0f)); // Dọn sạch quái: Xám sáng
            }
            else
            {
                SetBackgroundColor(new Color(0.5f, 0.5f, 0.5f, 1.0f)); // Chưa dọn quái: Xám trung bình
            }

            if (roomIcon != null)
            {
                roomIcon.gameObject.SetActive(roomIcon.sprite != null);
                roomIcon.color = Color.white; // Phục hồi màu icon gốc
            }
            if (playerIndicator != null) playerIndicator.SetActive(false);
        }
        else if (isAdjacentToVisited)
        {
            // Chưa đi qua nhưng kề cạnh phòng đã đi qua -> Màu tối hẳn, hiện icon phòng để người chơi biết trước
            gameObject.SetActive(true);
            SetAlpha(0.6f);
            SetBackgroundColor(new Color(0.2f, 0.2f, 0.2f, 1.0f)); // Rất tối
            
            if (roomIcon != null)
            {
                roomIcon.gameObject.SetActive(roomIcon.sprite != null);
                roomIcon.color = new Color(0.4f, 0.4f, 0.4f, 1.0f); // Icon cũng tối màu
            }
            
            if (playerIndicator != null) playerIndicator.SetActive(false);
        }
        else
        {
            // Các phòng còn lại chưa khám phá tới -> Ẩn hoàn toàn
            gameObject.SetActive(false);
        }
    }

    private void SetBackgroundColor(Color color)
    {
        if (roomBackground != null)
        {
            roomBackground.color = color;
        }
    }

    private void SetAlpha(float alpha)
    {
        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.alpha = alpha;
        }
    }
}
