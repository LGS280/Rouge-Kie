using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MelogWeaponAim : MonoBehaviour
{
    [Header("Khớp tay 1 & 2 của Melog")]
    public Transform leftHandTransform;
    public Transform rightHandTransform;

    [Header("Vũ khí 1 & 2")]
    public MobWeaponInfo leftWeaponInfo;
    public MobWeaponInfo rightWeaponInfo;

    private MelogBossAI melogAI;
    private MobHealth mobHealth;

    private void Awake()
    {
        melogAI = GetComponent<MelogBossAI>();
        mobHealth = GetComponent<MobHealth>();

        InitializeDualHandsAndWeapons();
    }

    public void InitializeDualHandsAndWeapons()
    {
        if (leftHandTransform == null)
        {
            leftHandTransform = transform.Find("Left_Hand_Position");
            if (leftHandTransform == null) leftHandTransform = transform.Find("Hand_Position");
        }

        if (rightHandTransform == null)
        {
            rightHandTransform = transform.Find("Right_Hand_Position");
            if (rightHandTransform == null) rightHandTransform = transform.Find("Second_Hand_Position");
        }

        if (leftHandTransform != null && leftWeaponInfo == null)
        {
            leftWeaponInfo = leftHandTransform.GetComponentInChildren<MobWeaponInfo>();
        }

        if (rightHandTransform != null && rightWeaponInfo == null)
        {
            rightWeaponInfo = rightHandTransform.GetComponentInChildren<MobWeaponInfo>();
        }
    }

    private void Update()
    {
        if (mobHealth != null && mobHealth.isDead)
        {
            enabled = false;
            return;
        }

        Transform target = (melogAI != null && melogAI.targetPlayer != null) ? melogAI.targetPlayer : FindNearestPlayerFallback();

        if (target != null && (melogAI == null || melogAI.IsCombatActivated()))
        {
            float dist = Vector2.Distance(transform.position, target.position);
            float maxDetect = (melogAI != null) ? melogAI.detectRange : 7.0f;

            if (dist <= maxDetect)
            {
                AimHandTowardsTarget(leftHandTransform, target.position);
                AimHandTowardsTarget(rightHandTransform, target.position);
            }
            else
            {
                ResetHandsToRest();
            }
        }
        else
        {
            ResetHandsToRest();
        }
    }

    private void ResetHandsToRest()
    {
        if (leftHandTransform != null)
        {
            leftHandTransform.localRotation = Quaternion.identity;
            leftHandTransform.localScale = Vector3.one;
        }
        if (rightHandTransform != null)
        {
            rightHandTransform.localRotation = Quaternion.identity;
            rightHandTransform.localScale = Vector3.one;
        }
    }

    private void AimHandTowardsTarget(Transform hand, Vector3 targetPos)
    {
        if (hand == null) return;

        Vector2 aimDirection = targetPos - hand.position;
        float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;

        bool isBodyFlippedLeft = Mathf.Abs(transform.eulerAngles.y - 180f) < 10f;

        if (isBodyFlippedLeft)
        {
            float localAngle = 180f - angle;
            hand.localRotation = Quaternion.Euler(0, 0, localAngle);

            float scaleY = (aimDirection.x > 0) ? -1f : 1f;
            hand.localScale = new Vector3(1f, scaleY, 1f);
        }
        else
        {
            hand.localRotation = Quaternion.Euler(0, 0, angle);

            float scaleY = (aimDirection.x < 0) ? -1f : 1f;
            hand.localScale = new Vector3(1f, scaleY, 1f);
        }
    }

    public void FireBothGuns(Vector2 targetPosition, int damage)
    {
        if (leftWeaponInfo != null)
        {
            leftWeaponInfo.Shoot(targetPosition, damage);
        }

        if (rightWeaponInfo != null)
        {
            rightWeaponInfo.Shoot(targetPosition, damage);
        }
    }

    public void DestroyWeaponsOnDeath()
    {
        if (leftHandTransform != null)
        {
            leftHandTransform.gameObject.SetActive(false);
            foreach (Transform child in leftHandTransform) Destroy(child.gameObject);
        }
        if (rightHandTransform != null)
        {
            rightHandTransform.gameObject.SetActive(false);
            foreach (Transform child in rightHandTransform) Destroy(child.gameObject);
        }
        enabled = false;
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
