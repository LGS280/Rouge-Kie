using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Component ngắm bắn 60 FPS mượt như Player dành riêng cho Quái (Mob / Enemy).
/// Được gắn trên GameObject Hand_Position của Quái.
/// Tuyệt đối KHÔNG BAO GIỜ xoay nghiêng thân người con Quái Gốc!
/// </summary>
public class MobWeaponAim : MonoBehaviour
{
    [Header("Kho Vũ Khí Quái (Tự động quét kho Assets/Prefab/Mobs_Weapons)")]
    public GameObject currentWeaponObject;
    public MobWeaponInfo currentWeaponInfo;

    private MobAI mobAI;
    private SpriteRenderer mobSpriteRenderer;
    private Transform handTransform;
    private Vector3 initialScale = Vector3.one;

    private void Awake()
    {
        // 🎯 TỰ ĐỘNG KHÓA VỊ TRÍ HAND_POSITION: Nếu gán nhầm trên Root Quái, tự nhảy tới Hand_Position
        if (transform.name == "Hand_Position")
        {
            handTransform = transform;
            if (transform.parent != null)
            {
                mobAI = transform.parent.GetComponent<MobAI>();
                mobSpriteRenderer = transform.parent.GetComponent<SpriteRenderer>();
            }
        }
        else
        {
            mobAI = GetComponent<MobAI>();
            mobSpriteRenderer = GetComponent<SpriteRenderer>();
            handTransform = transform.Find("Hand_Position");
            if (handTransform == null) handTransform = transform;
        }

        initialScale = handTransform.localScale;
    }

    private void Start()
    {
        InitializeMobWeapon();
    }

    /// <summary>
    /// Khởi tạo vũ khí ngẫu nhiên từ thư mục Assets/Prefab/Mobs_Weapons (Tự động nhận diện khi thêm prefab mới)
    /// </summary>
    public void InitializeMobWeapon()
    {
        Transform spawnParent = (handTransform != null) ? handTransform : transform;

        // 🛡️ DỌN DẸP TUYỆT ĐỐI: Xóa tất cả GameObject con cũ dưới Hand_Position để đảm bảo CHỈ CÓ NGUYÊN 1 CÂY VŨ KHÍ
        foreach (Transform child in spawnParent)
        {
            if (child != null)
            {
                Destroy(child.gameObject);
            }
        }
        currentWeaponObject = null;
        currentWeaponInfo = null;

        GameObject weaponPrefabToSpawn = null;
        List<GameObject> validWeapons = new List<GameObject>();

#if UNITY_EDITOR
        // 1. Quét tự động thư mục Assets/Prefab/Mobs_Weapons trong Editor khi bạn thêm vũ khí mới
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefab/Mobs_Weapons" });
        foreach (var guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (!path.ToLower().Contains("/bullets/"))
            {
                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    validWeapons.Add(prefab);
                }
            }
        }
#endif

        // 2. Dự phòng nạp từ Resources/Prefab/Mobs_Weapons cho môi trường Build
        if (validWeapons.Count == 0)
        {
            GameObject[] loadedList = Resources.LoadAll<GameObject>("Prefab/Mobs_Weapons");
            if (loadedList != null && loadedList.Length > 0)
            {
                foreach (var w in loadedList)
                {
                    if (w != null && !w.name.ToLower().Contains("bullet")) validWeapons.Add(w);
                }
            }
        }

        // Bốc ngẫu nhiên 1 vũ khí từ danh sách tự động tìm thấy
        if (validWeapons.Count > 0)
        {
            weaponPrefabToSpawn = validWeapons[Random.Range(0, validWeapons.Count)];
        }

        if (weaponPrefabToSpawn != null)
        {
            currentWeaponObject = Instantiate(weaponPrefabToSpawn, spawnParent);
            currentWeaponObject.transform.localPosition = Vector3.zero;
            currentWeaponObject.transform.localRotation = Quaternion.identity;

            currentWeaponInfo = currentWeaponObject.GetComponent<MobWeaponInfo>();
            if (currentWeaponInfo == null)
            {
                currentWeaponInfo = currentWeaponObject.AddComponent<MobWeaponInfo>();
            }

            // 🎯 Áp dụng tọa độ tay cầm từ DB (customHandPosition) để súng xoay mượt đúng khớp tay
            if (currentWeaponInfo != null)
            {
                currentWeaponInfo.ApplyMobWeaponConfig();
            }
        }
    }

    private void Update()
    {
        if (handTransform == null) handTransform = transform;

        // 🔒 BẢO VỆ TUYỆT ĐỐI: Khóa thân người con Quái Root luôn đứng thẳng (Quaternion.identity), 100% KHÔNG BAO GIỜ nghiêng 45 độ!
        if (transform != handTransform)
        {
            transform.localRotation = Quaternion.identity;
        }
        else if (transform.parent != null)
        {
            transform.parent.localRotation = Quaternion.identity;
        }

        Transform target = (mobAI != null) ? mobAI.targetPlayer : null;

        // 🎯 KIỂM TRA ĐỦ 2 ĐIỀU KIỆN ĐỂ AIM:
        // 1. Cửa phòng đã đóng và combat kích hoạt (IsCombatActivated == true)
        // 2. Player bước vào bán kính tầm quét (detectRange) HOẶC Quái đang ở trạng thái Chase/Attack
        if (mobAI != null && mobAI.IsCombatActivated() && target != null)
        {
            bool isChasingOrAttacking = mobAI.currentState == MobAI.EnemyState.Chase || mobAI.currentState == MobAI.EnemyState.Attack;
            float distanceToPlayer = Vector2.Distance(handTransform.position, target.position);

            if (isChasingOrAttacking || distanceToPlayer <= mobAI.detectRange)
            {
                // 🎯 CHỈ XOAY ĐÚNG VŨ KHÍ TRÊN HAND_POSITION!
                Vector2 aimDirection = target.position - handTransform.position;
                float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;

                handTransform.rotation = Quaternion.Euler(0, 0, angle);

                // Smart Scale Y Flip trực tiếp trên Hand_Position (Giữ nguyên tỉ lệ gốc)
                if (angle > 90f || angle < -90f)
                {
                    handTransform.localScale = new Vector3(initialScale.x, -Mathf.Abs(initialScale.y), initialScale.z);
                    if (mobSpriteRenderer != null) mobSpriteRenderer.flipX = true;
                }
                else
                {
                    handTransform.localScale = new Vector3(initialScale.x, Mathf.Abs(initialScale.y), initialScale.z);
                    if (mobSpriteRenderer != null) mobSpriteRenderer.flipX = false;
                }
                return;
            }
        }

        // 🎯 KHI CHƯA AIM PLAYER (ĐANG ĐI DẠO WANDER HOẶC IDLE):
        // Vũ khí tự động xoay và lật mặt xuôi theo hướng di chuyển/nhìn của Quái!
        if (mobSpriteRenderer != null && mobSpriteRenderer.flipX)
        {
            // Quái đang nhìn sang TRAÍ: Súng xuôi theo hướng trái
            handTransform.rotation = Quaternion.Euler(0, 0, 180f);
            handTransform.localScale = new Vector3(initialScale.x, -Mathf.Abs(initialScale.y), initialScale.z);
        }
        else
        {
            // Quái đang nhìn sang PHẢI: Súng xuôi theo hướng phải
            handTransform.rotation = Quaternion.Euler(0, 0, 0);
            handTransform.localScale = new Vector3(initialScale.x, Mathf.Abs(initialScale.y), initialScale.z);
        }
    }

    /// <summary>
    /// Bắn đạn / Đâm thương khi Quái tấn công
    /// </summary>
    public void Fire(Vector2 targetPos, int damageOverride)
    {
        if (currentWeaponInfo != null)
        {
            currentWeaponInfo.Shoot(targetPos, damageOverride);
        }
    }

    /// <summary>
    /// Xóa súng ngay lập tức khi Quái chết để súng không bị treo lơ lửng
    /// </summary>
    public void DestroyWeaponOnDeath()
    {
        if (currentWeaponObject != null)
        {
            Destroy(currentWeaponObject);
        }
    }
}