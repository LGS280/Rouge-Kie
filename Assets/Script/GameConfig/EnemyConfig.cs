using System;

[Serializable]
public class EnemyConfig
{
    public int id;
    public string enemyName;
    public int baseHealth;
    public float moveSpeed;
    public float attackSpeed;
    public string prefabName;
}

[Serializable]
public class EnemyArrayWrapper
{
    public EnemyConfig[] data;
}
