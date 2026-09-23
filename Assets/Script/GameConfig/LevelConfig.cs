using System;

[Serializable]
public class LevelConfig
{
    public int id;
    public int stageId;
    public int floorNumber;
    public float difficultyMultiplier;

    // Cấu hình số lượng phòng và Co-op Dynamic Scaling nạp trực tiếp từ Database
    public int baseRoomCount = 7;
    public int coopExtraRooms = 2;
    public float coopMobHPMultiplier = 0.4f;
    public float coopBossHPMultiplier = 0.6f;
    public int coopExtraMobsPerRoom = 1;

    public int chestRoomCount = 1;
    public int coopExtraChestRooms = 0;
}

[Serializable]
public class LevelArrayWrapper
{
    public LevelConfig[] data;
}
