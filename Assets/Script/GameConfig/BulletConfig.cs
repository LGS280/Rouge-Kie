using System;

[Serializable]
public class BulletConfig
{
    public int id;
    public string bulletName;
    public int damage;
    public float critRate;
    public float flightSpeed;
    public int piercingCount;
    public string prefabName;
}

[Serializable]
public class BulletArrayWrapper
{
    public BulletConfig[] data;
}