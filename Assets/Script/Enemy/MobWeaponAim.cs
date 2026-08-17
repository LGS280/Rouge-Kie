using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    public void InitializeMobWeapon()
    {
        Transform spawnParent = (handTransform != null) ? handTransform : transform;

        currentWeaponInfo = spawnParent.GetComponentInChildren<MobWeaponInfo>(true);
        if (currentWeaponInfo != null)
        {
            currentWeaponObject = currentWeaponInfo.gameObject;
            currentWeaponObject.transform.localPosition = Vector3.zero;
            currentWeaponObject.transform.localRotation = Quaternion.identity;
            currentWeaponInfo.ApplyMobWeaponConfig();
        }
        else if (spawnParent.childCount > 0)
        {

            currentWeaponObject = spawnParent.GetChild(0).gameObject;
            currentWeaponInfo = currentWeaponObject.GetComponent<MobWeaponInfo>();
            if (currentWeaponInfo == null)
            {
                currentWeaponInfo = currentWeaponObject.AddComponent<MobWeaponInfo>();
            }
            if (currentWeaponInfo != null)
            {
                currentWeaponInfo.ApplyMobWeaponConfig();
            }
        }
    }

    private void Update()
    {
        if (handTransform == null) handTransform = transform;

        MobHealth mobHealth = GetComponentInParent<MobHealth>();
        if (mobHealth != null && mobHealth.isDead)
        {
            enabled = false;
            return;
        }

        if (transform != handTransform)
        {
            transform.localRotation = Quaternion.identity;
        }
        else if (transform.parent != null)
        {
            transform.parent.localRotation = Quaternion.identity;
        }

        if (mobAI != null && mobAI.IsCombatActivated())
        {
            Transform target = (mobAI.targetPlayer != null) ? mobAI.targetPlayer : FindNearestPlayerFallback();

            if (target != null)
            {
                float distanceToPlayer = Vector2.Distance(handTransform.position, target.position);
                float maxDetect = mobAI.detectRange;

                if (distanceToPlayer <= maxDetect)
                {

                    Vector2 aimDirection = target.position - handTransform.position;
                    float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;

                    handTransform.rotation = Quaternion.Euler(0, 0, angle);

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
        }

        if (mobSpriteRenderer != null && mobSpriteRenderer.flipX)
        {

            handTransform.rotation = Quaternion.Euler(0, 0, 180f);
            handTransform.localScale = new Vector3(initialScale.x, -Mathf.Abs(initialScale.y), initialScale.z);
        }
        else
        {

            handTransform.rotation = Quaternion.Euler(0, 0, 0);
            handTransform.localScale = new Vector3(initialScale.x, Mathf.Abs(initialScale.y), initialScale.z);
        }
    }

    public void Fire(Vector2 targetPos, int damageOverride)
    {
        if (currentWeaponInfo != null)
        {
            currentWeaponInfo.Shoot(targetPos, damageOverride);
        }
    }

    public void DestroyWeaponOnDeath()
    {
        if (currentWeaponObject != null)
        {
            Destroy(currentWeaponObject);
        }
    }

    private Transform FindNearestPlayerFallback()
    {
        float shortest = Mathf.Infinity;
        Transform nearest = null;

        GameObject localPlayerObj = GameObject.FindGameObjectWithTag("Player");
        if (localPlayerObj != null)
        {
            RookieHealth health = localPlayerObj.GetComponent<RookieHealth>();
            if (health == null || !health.isDead)
            {
                shortest = Vector2.Distance(transform.position, localPlayerObj.transform.position);
                nearest = localPlayerObj.transform;
            }
        }

        RemotePlayerController[] remotes = Object.FindObjectsByType<RemotePlayerController>(FindObjectsSortMode.None);
        foreach (var rpc in remotes)
        {
            if (rpc != null && !rpc.isDead)
            {
                float d = Vector2.Distance(transform.position, rpc.transform.position);
                if (d < shortest)
                {
                    shortest = d;
                    nearest = rpc.transform;
                }
            }
        }

        return nearest;
    }
}
