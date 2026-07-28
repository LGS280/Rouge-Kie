using System;

[Serializable]
public class WeaponConfig
{
    public int id;
    public string weaponName;
    public float fireRate;
    public int manaCost;
    public int bulletsPerShot;
    public float spreadAngle;
    public int bulletId;

    public string prefabName;
    public string shootSound;
    public float shootVolume;
    public float handPositionX;
    public float handPositionY;
    public float handPositionZ;
    public float recoilDistance;
    public float recoilDuration;
    public float returnDuration;

    // 2 Cột mới bổ sung từ Backend Database API
    public string weaponType; // "Pistol", "Heavy Gun", "Sniper", "Rifle", "Laser Gun", "SMG", "Shotgun", "Sword"
    public string rarity;     // "Common", "Rare", "Epic", "Legendary"
}

[Serializable]
public class WeaponArrayWrapper
{
    public WeaponConfig[] data;
}