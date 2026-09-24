using UnityEngine;

/// <summary>
/// Giao diện chuẩn cho tất cả Boss / Mini-Boss trong game:
/// Cho phép RoomController, BossHealthBarUI, MultiplayerSyncManager tương tác đồng nhất
/// mà không phụ thuộc vào lớp cụ thể (MelogBossAI, BraeadBossAI hay bất kỳ Boss nào trong tương lai).
/// </summary>
public interface IBossAI
{
    void SetRoom(RoomController room);
    void ActivateMob();
    bool IsCombatActivated();
    string GetBossDisplayName();
    void UpdateNetworkPosition(Vector2 newPos);
    void ExecuteNetworkAttack(Vector2 targetPos);
}
