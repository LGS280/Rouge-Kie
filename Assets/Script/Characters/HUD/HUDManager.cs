using UnityEngine;

public class HUDManager : MonoBehaviour
{
    [SerializeField] PlayerHUD[] playerHUDs;
    [SerializeField] RookieHealth[] players;

    void Start()
    {
        for (int i = 0; i < playerHUDs.Length; i++)
        {
            if (i < players.Length && players[i] != null)
                playerHUDs[i].SetTarget(players[i]);
        }
    }
}