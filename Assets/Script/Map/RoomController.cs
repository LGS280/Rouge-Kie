using System.Collections.Generic;
using UnityEngine;

public class RoomController : MonoBehaviour
{
    [Header("Multiplayer Settings (Auto-generated)")]
    // TỰ ĐỘNG HÓA: ID này sẽ tự sinh bằng GetInstanceID().ToString() lúc Runtime, không cần điền tay nữa!
    public string roomUniqueId { get; set; }

    [Header("Room Status")]
    public bool roomCleared = false;
    public bool roomStarted = false;
    private Transform currentPlayer;

    // Cache lại Collider của phòng để chia sẻ cho các MobAI lấy biên di chuyển
    public Collider2D RoomCollider { get; private set; }

    private readonly List<RoomDoor> doors = new List<RoomDoor>();
    private readonly List<MobHealth> mobs = new List<MobHealth>();

    private void Awake()
    {
        RoomCollider = GetComponent<Collider2D>();

        // TỰ ĐỘNG SINH ID: Lấy mã InstanceID độc nhất của Object này trong Scene hiện tại
        if (string.IsNullOrEmpty(roomUniqueId))
        {
            roomUniqueId = gameObject.GetInstanceID().ToString();
        }
    }

    private void Start()
    {
        // Thu thập quái và cửa ngay từ đầu bằng Toán học (Bounds) thay vì chờ Vật lý (OverlapPoint)
        CollectMobsInsideRoom();
        CollectDoorsNearRoom();
    }

    public void AddDoor(RoomDoor door)
    {
        if (door != null && !doors.Contains(door))
        {
            doors.Add(door);
            door.SetOwnerRoom(this);
        }
    }

    public void AddMob(MobHealth mob)
    {
        if (mob == null || mobs.Contains(mob))
            return;

        mobs.Add(mob);
        mob.OnDeath += OnMobDeath;

        // Tự động gán tham chiếu phòng cho MobAI mới nạp
        MobAI mobAI = mob.GetComponent<MobAI>();
        if (mobAI != null)
        {
            mobAI.myRoom = this;
        }

        Debug.Log($"{gameObject.name} AddMob: {mob.name}");
    }

    private void Update()
    {
        if (roomStarted && !roomCleared)
        {
            CheckRoomCleared();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            currentPlayer = collision.transform;
            TryStartRoomCombat();
        }
    }

    public void TryStartRoomCombat()
    {
        Debug.Log($"{gameObject.name} TryStartRoomCombat - AliveMobs: {GetAliveMobCount()}");

        if (roomCleared || roomStarted)
            return;

        if (GetAliveMobCount() <= 0)
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.SendRoomClearedEvent(roomUniqueId);
            }
            else
            {
                ClearRoom();
            }
            return;
        }

        // Gửi ID tự động lên Server
        if (NetworkManager.Instance != null && currentPlayer != null)
        {
            // SỬA THEO YÊU CẦU: Lấy tọa độ mép cửa phía trong phòng (safeSpot) thay vì giữa phòng,
            // tránh trường hợp giữa phòng có vật cản.
            Vector3 roomCenter = transform.position;
            Vector3 dirToCenter = (roomCenter - currentPlayer.position).normalized;
            Vector3 safeTeleportPos = currentPlayer.position + dirToCenter * 2.0f; // Đẩy vào trong 2 unit từ mép cửa

            NetworkManager.Instance.SendRoomCombatTrigger(roomUniqueId, safeTeleportPos);
        }
        else
        {
            ExecuteStartCombatLocal();
        }
    }

    public void ExecuteStartCombatLocal()
    {
        if (roomStarted || roomCleared) return;

        roomStarted = true;
        PushPlayerInsideRoom();
        CloseDoors();
        ActivateAllMobsInRoom();
    }

    public void ExecuteClearRoomLocal()
    {
        roomCleared = true;
        roomStarted = false;
        OpenDoors();
    }

    private void PushPlayerInsideRoom()
    {
        if (currentPlayer == null) return;

        // Bỏ qua nếu đang chơi Multi, vì MultiplayerSyncManager đã tự dịch chuyển (tránh đẩy 2 lần)
        if (NetworkManager.Instance != null) return;

        Vector3 roomCenter = transform.position;
        Vector3 dir = (roomCenter - currentPlayer.position).normalized;

        currentPlayer.position += dir * 1.5f;
    }

    private void ActivateAllMobsInRoom()
    {
        foreach (MobHealth mob in mobs)
        {
            if (mob != null)
            {
                MobAI mobAI = mob.GetComponent<MobAI>();
                if (mobAI != null)
                {
                    mobAI.ActivateMob();
                }
            }
        }
    }

    private void OnMobDeath(MobHealth deadMob)
    {
        Debug.Log($"{gameObject.name} Mob Died: {deadMob.name}");
        CheckRoomCleared();
    }

    private void CheckRoomCleared()
    {
        if (GetAliveMobCount() <= 0)
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.SendRoomClearedEvent(roomUniqueId);
            }
            else
            {
                ClearRoom();
            }
        }
    }

    private void ClearRoom()
    {
        ExecuteClearRoomLocal();
    }

    private int GetAliveMobCount()
    {
        int count = 0;
        foreach (MobHealth mob in mobs)
        {
            if (mob != null && !mob.isDead)
                count++;
        }
        return count;
    }

    private void CloseDoors()
    {
        Debug.Log($"{gameObject.name} CLOSE DOORS - DoorCount: {doors.Count}");
        foreach (RoomDoor door in doors)
        {
            if (door != null)
            {
                door.CloseDoor();
            }
        }
    }

    private void OpenDoors()
    {
        Debug.Log($"{gameObject.name} OPEN DOORS");
        foreach (RoomDoor door in doors)
        {
            if (door != null)
                door.OpenDoor();
        }
    }

    private void CollectMobsInsideRoom()
    {
        if (RoomCollider == null)
            return;

        // Tạo Bounds 2D an toàn (mở rộng trục Z ra vô cực để không bị trượt quái nếu lệch Z)
        // Mở rộng XY thêm 0.5f để bao cả dung sai
        Bounds bounds = RoomCollider.bounds;
        bounds.Expand(new Vector3(0.5f, 0.5f, 100f));

        MobHealth[] allMobs = Object.FindObjectsByType<MobHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (MobHealth mob in allMobs)
        {
            if (mob == null || mob.isDead || mobs.Contains(mob))
                continue;

            // Sử dụng Bounds.Contains thay vì OverlapPoint để không phụ thuộc vào Physics2D
            if (bounds.Contains(mob.transform.position))
            {
                AddMob(mob);
            }
        }
    }

    private void CollectDoorsNearRoom()
    {
        if (RoomCollider == null)
            return;

        Bounds roomBounds = RoomCollider.bounds;
        roomBounds.Expand(8f);

        RoomDoor[] allDoors = Object.FindObjectsByType<RoomDoor>(FindObjectsSortMode.None);

        foreach (RoomDoor door in allDoors)
        {
            if (door == null || doors.Contains(door))
                continue;

            Collider2D[] doorColliders = door.GetComponents<Collider2D>();
            bool isNearRoom = false;

            foreach (Collider2D doorCol in doorColliders)
            {
                if (doorCol != null && roomBounds.Intersects(doorCol.bounds))
                {
                    isNearRoom = true;
                    break;
                }
            }

            if (isNearRoom)
            {
                AddDoor(door);
            }
        }
    }
}