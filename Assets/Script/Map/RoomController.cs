using System.Collections.Generic;
using UnityEngine;

public class RoomController : MonoBehaviour
{
    public bool roomCleared = false;
    public bool roomStarted = false;
    private Transform currentPlayer;

    private readonly List<RoomDoor> doors = new List<RoomDoor>();
    private readonly List<MobHealth> mobs = new List<MobHealth>();

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

    private void PushPlayerInsideRoom()
    {
        if (currentPlayer == null)
            return;

        Vector3 roomCenter = transform.position;
        Vector3 dir = (roomCenter - currentPlayer.position).normalized;

        currentPlayer.position += dir * 1.5f;
    }

    public void TryStartRoomCombat()
    {
        CollectMobsInsideRoom();
        CollectDoorsNearRoom();

        Debug.Log($"{gameObject.name} StartCombat - AliveMobs: {GetAliveMobCount()}");

        if (roomCleared || roomStarted)
            return;

        if (GetAliveMobCount() <= 0)
        {
            ClearRoom();
            return;
        }

        roomStarted = true;

        PushPlayerInsideRoom();
        CloseDoors();
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
            ClearRoom();
        }
    }

    private void ClearRoom()
    {
        roomCleared = true;
        roomStarted = false;
        OpenDoors();
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
                Debug.Log($"Closing door: {door.name}");
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
        Collider2D roomCollider = GetComponent<Collider2D>();

        if (roomCollider == null)
            return;

        MobHealth[] allMobs = Object.FindObjectsByType<MobHealth>(FindObjectsSortMode.None);

        foreach (MobHealth mob in allMobs)
        {
            if (mob == null || mob.isDead || mobs.Contains(mob))
                continue;

            if (roomCollider.OverlapPoint(mob.transform.position))
            {
                AddMob(mob);
            }
        }
    }

    private void CollectDoorsNearRoom()
    {
        Collider2D roomCollider = GetComponent<Collider2D>();

        if (roomCollider == null)
            return;

        Bounds roomBounds = roomCollider.bounds;
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

        Debug.Log($"{gameObject.name} CollectDoorsNearRoom - DoorCount: {doors.Count}");
    }
}