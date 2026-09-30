using UnityEngine;

public class HUDManager : MonoBehaviour
{
    [SerializeField] PlayerHUD[] playerHUDs;
    [SerializeField] RookieHealth[] players;

    void Start()
    {
        for (int i = 0; i < playerHUDs.Length; i++)
        {
            if (playerHUDs[i] == null) continue;

            RookieHealth targetPlayer = null;
            if (i == 0)
            {
                // Luôn ưu tiên tìm nhân vật người chơi thực tế đang active trong Scene
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) targetPlayer = p.GetComponent<RookieHealth>();
            }

            // Fallback nếu không tìm thấy tag Player thì mới dùng danh sách kéo thả Inspector
            if (targetPlayer == null && players != null && i < players.Length)
            {
                targetPlayer = players[i];
            }

            if (targetPlayer != null)
                playerHUDs[i].SetTarget(targetPlayer);
        }
    }
}