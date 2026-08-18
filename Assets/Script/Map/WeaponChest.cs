using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponChest : MonoBehaviour
{
    [Header("thành phần của rương")]
    public GameObject body;
    public GameObject top;
    public GameObject inside;

    [Header("cấu hình vật phẩm trong rương")]
    public GameObject[] weaponPrefabs;

    private bool isOpened = false;
    private bool isPlayerInRange = false;
    private TextMesh promptText;

    private void Start()
    {

        if (body == null)
        {
            Transform bodyTrans = transform.Find("Body");
            if (bodyTrans != null) body = bodyTrans.gameObject;
        }

        if (top == null)
        {
            Transform topTrans = transform.Find("Top");
            if (topTrans != null) top = topTrans.gameObject;
        }

        if (inside == null)
        {
            Transform insideTrans = transform.Find("Inside");
            if (insideTrans != null) inside = insideTrans.gameObject;
        }

        EnforceSortingOrders();

        CreatePromptText();
    }

    private void EnforceSortingOrders()
    {
        if (body != null)
        {
            SpriteRenderer sr = body.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = 5;
        }

        if (top != null)
        {
            SpriteRenderer sr = top.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = 6;
        }

        if (inside != null)
        {
            SpriteRenderer sr = inside.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = 6;
        }
    }

    private void CreatePromptText()
    {
        GameObject textObj = new GameObject("PromptText");
        textObj.transform.SetParent(transform);
        textObj.transform.localPosition = new Vector3(0f, 0.95f, 0f);

        promptText = textObj.AddComponent<TextMesh>();
        promptText.text = "";
        promptText.fontSize = 32;
        promptText.characterSize = 0.07f;
        promptText.anchor = TextAnchor.MiddleCenter;
        promptText.alignment = TextAlignment.Center;
        promptText.color = Color.green;

        MeshRenderer mr = textObj.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingOrder = 10;
        }
    }

    private void Update()
    {
        if (isOpened) return;

        if (isPlayerInRange)
        {

            if (promptText != null)
            {
                if (InputDeviceHelper.IsGamepadActive())
                {
                    promptText.text = "Press B";
                }
                else
                {
                    promptText.text = "Press E";
                }
                promptText.color = Color.green;
            }

            bool hasPressedOpenKey = false;

            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                hasPressedOpenKey = true;
            }

            else if (Gamepad.current != null && Gamepad.current.bButton.wasPressedThisFrame)
            {
                hasPressedOpenKey = true;
            }

            if (hasPressedOpenKey)
            {
                OpenChest();
            }
        }
    }

    public void OpenChest()
    {
        if (isOpened) return;
        isOpened = true;

        if (body != null) body.SetActive(true);
        if (inside != null) inside.SetActive(true);

        if (promptText != null)
        {
            Destroy(promptText.gameObject);
        }

        SpawnWeaponLoot();

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = false;
        }

        if (top != null)
        {
            StartCoroutine(AnimateTopChestOpen(top));
        }
    }

    private IEnumerator AnimateTopChestOpen(GameObject topObj)
    {
        SpriteRenderer sr = topObj.GetComponent<SpriteRenderer>();
        Vector3 startPos = topObj.transform.localPosition;
        Vector3 targetPos = startPos + new Vector3(0f, 0.4f, 0f);

        float duration = 0.55f;
        float elapsed = 0f;

        Color startColor = (sr != null) ? sr.color : Color.white;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            topObj.transform.localPosition = Vector3.Lerp(startPos, targetPos, t);

            if (sr != null)
            {
                Color c = startColor;
                c.a = Mathf.Lerp(1f, 0f, t);
                sr.color = c;
            }

            yield return null;
        }

        topObj.SetActive(false);
    }

    private void SpawnWeaponLoot()
    {

#if UNITY_EDITOR
        if (weaponPrefabs == null || weaponPrefabs.Length == 0)
        {
            string folderPath = "Assets/Prefab/Weapons";
            if (System.IO.Directory.Exists(folderPath))
            {
                string[] files = System.IO.Directory.GetFiles(folderPath, "*.prefab");
                System.Collections.Generic.List<GameObject> list = new System.Collections.Generic.List<GameObject>();
                foreach (string file in files)
                {
                    if (file.Contains("GroundWeapon")) continue;
                    GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(file);
                    if (prefab != null) list.Add(prefab);
                }
                weaponPrefabs = list.ToArray();
            }
        }
#endif

        if (weaponPrefabs == null || weaponPrefabs.Length == 0)
        {

            return;
        }

        GameObject randomWeaponPrefab = SelectWeaponByRarity(weaponPrefabs);

        Vector3 spawnPos = transform.position + new Vector3(0.8f, 0f, 0f);
        GroundWeapon.Create(randomWeaponPrefab, spawnPos);

    }

    private GameObject SelectWeaponByRarity(GameObject[] prefabs)
    {
        float totalWeight = 0f;
        float[] weights = new float[prefabs.Length];

        for (int i = 0; i < prefabs.Length; i++)
        {
            float weight = 40f;
            GameObject p = prefabs[i];

            if (p != null)
            {
                string pName = p.name.Replace("(Clone)", "").Trim();
                if (GameConfigManager.Instance != null && GameConfigManager.Instance.WeaponDbByName.TryGetValue(pName, out WeaponConfig config))
                {
                    if (!string.IsNullOrEmpty(config.rarity))
                    {
                        switch (config.rarity.Trim().ToLower())
                        {
                            case "common":
                                weight = 50f;
                                break;
                            case "rare":
                                weight = 30f;
                                break;
                            case "epic":
                                weight = 15f;
                                break;
                            case "legendary":
                                weight = 5f;
                                break;
                        }
                    }
                }
            }

            weights[i] = weight;
            totalWeight += weight;
        }

        float randomRoll = Random.Range(0f, totalWeight);
        float currentSum = 0f;

        for (int i = 0; i < prefabs.Length; i++)
        {
            currentSum += weights[i];
            if (randomRoll <= currentSum)
            {
                return prefabs[i];
            }
        }

        return prefabs[Random.Range(0, prefabs.Length)];
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isOpened) return;

        if (collision.CompareTag("Player"))
        {
            isPlayerInRange = true;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (isOpened) return;

        if (collision.CompareTag("Player"))
        {
            isPlayerInRange = false;

            if (promptText != null)
            {
                promptText.text = "";
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        string folderPath = "Assets/Prefab/Weapons";
        if (System.IO.Directory.Exists(folderPath))
        {
            string[] files = System.IO.Directory.GetFiles(folderPath, "*.prefab");
            System.Collections.Generic.List<GameObject> list = new System.Collections.Generic.List<GameObject>();
            foreach (string file in files)
            {
                if (file.Contains("GroundWeapon")) continue;
                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(file);
                if (prefab != null)
                {
                    list.Add(prefab);
                }
            }
            weaponPrefabs = list.ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
