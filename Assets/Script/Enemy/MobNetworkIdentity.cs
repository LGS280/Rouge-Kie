using UnityEngine;

public class MobNetworkIdentity : MonoBehaviour
{
    // ID duy nhất do Server ASP.NET cấp phát khi spawn quái
    public string networkId;

    // Đánh dấu xem quái này thuộc về phòng chơi nào
    public string roomId;
}