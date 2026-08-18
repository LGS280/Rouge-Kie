using UnityEngine;

public class GroundWeapon : MonoBehaviour
{
    [Header("prefab Vũ khí tương ứng")]
    public GameObject weaponPrefab;

    private bool isPlayerInside = false;
    private Transform nameTagTrans;

    public static GameObject Create(GameObject weaponPrefab, Vector3 position)
    {
        if (weaponPrefab == null) return null;

        GameObject go = new GameObject("GroundWeapon");
        go.transform.position = position;

        Vector3 targetScale = new Vector3(2.5f, 2.5f, 1f);
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            targetScale = player.transform.lossyScale;
            targetScale.z = 1f;
        }
        go.transform.localScale = targetScale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1.2f, 1.2f);

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        GroundWeapon gw = go.AddComponent<GroundWeapon>();
        gw.weaponPrefab = weaponPrefab;

        return go;
    }

    private void Start()
    {

        if (weaponPrefab != null)
        {
            SpriteRenderer prefabSR = weaponPrefab.GetComponent<SpriteRenderer>();
            SpriteRenderer mySR = GetComponent<SpriteRenderer>();
            if (prefabSR != null && mySR != null)
            {
                mySR.sprite = prefabSR.sprite;
                mySR.sortingOrder = 8;
            }

            CreateNameTag();
        }
        else
        {

            GameObject textObj = new GameObject("NameTag");
            textObj.transform.SetParent(transform);
            nameTagTrans = textObj.transform;

            Vector3 parentScale = transform.localScale;
            textObj.transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f);
            textObj.transform.localPosition = new Vector3(0f, 0.22f, 0f);

            TextMesh textMesh = textObj.AddComponent<TextMesh>();
            textMesh.text = "Vũ Khí Vô Danh";
            textMesh.fontSize = 32;
            textMesh.characterSize = 0.07f;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = Color.white;

            MeshRenderer mr = textObj.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 9;
        }
    }

    private void CreateNameTag()
    {
        if (weaponPrefab == null) return;

        GameObject textObj = new GameObject("NameTag");
        textObj.transform.SetParent(transform);
        nameTagTrans = textObj.transform;

        Vector3 parentScale = transform.localScale;
        textObj.transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f);
        textObj.transform.localPosition = new Vector3(0f, 0.42f, 0f);

        TextMesh textMesh = textObj.AddComponent<TextMesh>();
        textMesh.text = "";
        textMesh.fontSize = 32;
        textMesh.characterSize = 0.07f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.green;

        MeshRenderer mr = textObj.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingOrder = 9;
        }
    }

    private void Update()
    {
        if (isPlayerInside)
        {
            UpdatePromptText();
        }
    }

    private void UpdatePromptText()
    {
        TextMesh tm = GetComponentInChildren<TextMesh>();
        if (tm != null)
        {
            tm.color = Color.green;
            string sName = (weaponPrefab != null) ? weaponPrefab.name : "Vũ Khí";
            string cleanName = sName.Replace("(Clone)", "").Replace("_", " ");

            string keyName = InputDeviceHelper.GetInteractKeyDisplayString();
            if (!string.IsNullOrEmpty(keyName))
            {
                keyName = keyName.Replace("HOLD ", "").Replace("HOLD", "").Replace("[", "").Replace("]", "").Trim();
            }
            if (string.IsNullOrEmpty(keyName)) keyName = "E";

            if (InputDeviceHelper.IsGamepadActive())
            {
                tm.text = cleanName + "\n(Press B)";
            }
            else
            {
                tm.text = cleanName + "\n(Press " + keyName + ")";
            }

            if (nameTagTrans != null)
            {
                nameTagTrans.localPosition = new Vector3(0f, 0.42f, 0f);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            WeaponManager wm = collision.GetComponent<WeaponManager>();
            if (wm != null)
            {
                if (!wm.nearbyWeapons.Contains(this))
                {
                    wm.nearbyWeapons.Add(this);
                    isPlayerInside = true;
                    UpdatePromptText();
                }
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            WeaponManager wm = collision.GetComponent<WeaponManager>();
            if (wm != null)
            {
                wm.nearbyWeapons.Remove(this);
                isPlayerInside = false;

                TextMesh tm = GetComponentInChildren<TextMesh>();
                if (tm != null)
                {
                    tm.text = "";
                }
            }
        }
    }

    private void OnDestroy()
    {

        WeaponManager wm = Object.FindFirstObjectByType<WeaponManager>();
        if (wm != null && wm.nearbyWeapons.Contains(this))
        {
            wm.nearbyWeapons.Remove(this);
        }
    }
}
