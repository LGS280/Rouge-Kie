using System;

[Serializable]
public class LevelConfig
{
    public int id;
    public int stageId;
    public int floorNumber;
    public float difficultyMultiplier;
}

[Serializable]
public class LevelArrayWrapper
{
    public LevelConfig[] data;
}
