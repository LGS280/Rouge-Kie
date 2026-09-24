using System;

[Serializable]
public class BuffConfig
{
    public int id;
    public string buffName;
    public string description;
    public string buffType;
    public float value;
    public string rarity;
}

[Serializable]
public class BuffArrayWrapper
{
    public BuffConfig[] data;
}
