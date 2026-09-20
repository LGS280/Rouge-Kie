using System;

[Serializable]
public class CharacterConfig
{
    public int characterId;
    public int id;
    public string name;
    public string description;
    public int baseHealth;
    public int baseMana;
    public int baseArmor;
    public string prefabName;
    public string skillSet;
    public int unlockPrice;
    public string currencyType;

    public int GetId() => characterId > 0 ? characterId : id;
}

[Serializable]
public class CharacterArrayWrapper
{
    public CharacterConfig[] data;
}
