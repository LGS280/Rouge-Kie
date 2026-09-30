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

            RookieHealth targetPlayer = (players != null && i < players.Length) ? players[i] : null;
            if (targetPlayer == null)
            {
                // Tự động tìm nhân vật người chơi trong Scene nếu Inspector bị Missing/None
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) targetPlayer = p.GetComponent<RookieHealth>();
            }

            if (targetPlayer != null)
                playerHUDs[i].SetTarget(targetPlayer);
        }
    }
}