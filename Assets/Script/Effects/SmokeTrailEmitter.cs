using UnityEngine;

public class SmokeTrailEmitter : MonoBehaviour
{
    [Header("CẤU HÌNH XẢ KHÓI TỰ NHIÊN (ORGANIC SMOKE)")]
    public Sprite smokeSprite;
    public int initialBurstCount = 6;     // 6-7 cục khói nhả ra ở nòng súng khi vừa bắn
    public float minSpawnInterval = 0.012f;// Khoảng thời gian xả khói ngẫu nhiên
    public float maxSpawnInterval = 0.025f;
    public float smokeLifetime = 1.35f;   // Tăng thời gian đọng lại của khói (1.35 giây giúp vệt khói kéo dài lâu hơn)
    public Vector3 spawnOffset = new Vector3(-0.15f, 0f, 0f);// Tọa độ lệch tương đối so với vật thể cha lệch về sau 0.15 X

    private float nextSpawnTime = 0f;

    void Start() // chạy duy nhất 1 lần khi run game
    {
        EnsureSmokeSprite();

        // 1. NGAY LÚC BẮN: Nổ chùm 6-7 cục khói bất quy tắc ngay đít súng
        Vector3 originPos = transform.TransformPoint(spawnOffset);
        for (int i = 0; i < initialBurstCount; i++) 
        {
            Vector2 randomDir = Random.insideUnitCircle * Random.Range(0.05f, 0.25f); // lấy 1 vecotr ngấu nhiên trong bán kính từ 0.05 đến 0.25 để làm độ lệch vị trí
            Vector3 burstPos = originPos + (Vector3)randomDir; // vị trí spawn ra khói sau khi cộng thêm độ lệch
            Vector2 burstVel = randomDir.normalized * Random.Range(0.2f, 0.6f); // hướng và vận tốc bay của khói 
            float pScale = Random.Range(0.45f, 0.8f); // kích thước khói

            SpawnParticleAt(burstPos, burstVel, pScale); // gọi hàm tạo khói
        }
    }

    void Update() // chạy mỗi frame
    {
        if (Time.time >= nextSpawnTime) // check xem khói đến thời điểm tạo ra đợt khói mới chưa
        {
            nextSpawnTime = Time.time + Random.Range(minSpawnInterval, maxSpawnInterval); // đặt lịch cho lần tạo ra khói tiếp theo ngẫu nhiên trong khoảng minSpawnInterval, maxSpawnInterval

            // 2. KHI ĐẠN BAY: Xả ngẫu nhiên từ 1 đến 3 hạt khói tại các vị trí nhiễu loạn khác nhau
            Vector3 worldSpawnPos = transform.TransformPoint(spawnOffset); // lấy vị trí của đuôi đạn
            int count = Random.Range(1, 4); // 1, 2 hoặc 3 hạt mỗi nhịp

            for (int i = 0; i < count; i++)
            {
                // Khử hoàn toàn sự song song bằng cách chọn vị trí ngẫu nhiên trong vùng elip tự do
                float perpOffset = Random.Range(-0.16f, 0.16f); // độ lệch vuông góc với hướng đạn
                float alongOffset = Random.Range(-0.08f, 0.08f); // độc lệch dọc theo hướng đạn

                Vector3 localDelta = (Vector3)(Vector2.Perpendicular(transform.right) * perpOffset + (Vector2)transform.right * alongOffset);
                Vector3 particlePos = worldSpawnPos + localDelta;

                // Vận tốc cuộn xoáy ngẫu nhiên theo nhiều góc
                float driftAngle = Random.Range(-60f, 60f);
                Vector2 driftDir = Quaternion.Euler(0, 0, driftAngle) * (-transform.right * 0.3f + (Vector3)Vector2.Perpendicular(transform.right) * Mathf.Sign(perpOffset) * 0.4f);// tính toán hướng dạt ra của khói: đẩy lùi ra sau và xoay thêm 1 góc driftAngle
                Vector2 driftVel = driftDir * Random.Range(0.25f, 0.6f);

                float pScale = Random.Range(0.45f, 0.75f);
                SpawnParticleAt(particlePos, driftVel, pScale);
            }
        }
    }

    private void EnsureSmokeSprite() // hỗ trợ tự động tìm và tải spitre khói
    {
        if (smokeSprite == null)
        {
            smokeSprite = Resources.Load<Sprite>("Effect/Smoke");
            if (smokeSprite == null)
            {
#if UNITY_EDITOR
                smokeSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Effect/Smoke.png");
#endif
            }
        }
    }

    private void SpawnParticleAt(Vector3 pos, Vector2 velocity, float scale)
    {
        if (smokeSprite == null) return;

        GameObject smokeObj = new GameObject("SmokeParticle");
        smokeObj.transform.position = pos;
        smokeObj.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f)); // Xoay góc ngẫu nhiên quanh trục Z từ $0^\circ$ đến $360^\circ$ để các hạt nhìn khác nhau.
        smokeObj.transform.localScale = Vector3.one * scale;

        SpriteRenderer sr = smokeObj.AddComponent<SpriteRenderer>();
        sr.sprite = smokeSprite;
        sr.sortingOrder = 9; // Nằm sát dưới đạn

        float targetEndScale = scale * Random.Range(2.0f, 2.8f);
        float rotSpeed = Random.Range(-70f, 70f);

        smokeObj.AddComponent<SmokeParticleBehavior>().Init(smokeLifetime, scale, targetEndScale, velocity, rotSpeed);
    }
}

public class SmokeParticleBehavior : MonoBehaviour
{
    private float lifetime;
    private float startScale;
    private float endScale;
    private Vector2 velocity;
    private float rotationSpeed;
    private float elapsed = 0f;
    private SpriteRenderer sr;

    public void Init(float life, float sScale, float eScale, Vector2 vel, float rotSpd)
    {
        lifetime = life;
        startScale = sScale;
        endScale = eScale;
        velocity = vel;
        rotationSpeed = rotSpd;
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float progress = elapsed / lifetime;

        if (progress >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        // Tự quay tròn nhẹ tạo độ cuộn sinh động
        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);

        // Khói từ từ dạt ra và giảm tốc độ dần
        transform.position += (Vector3)(velocity * (1f - progress * 0.7f) * Time.deltaTime);

        // Nở to dần theo thời gian
        float currentScale = Mathf.Lerp(startScale, endScale, progress);
        transform.localScale = Vector3.one * currentScale;

        // Giữ độ rõ của khói lâu hơn (trong 40% thời gian đầu giữ rõ, sau đó mới từ từ mờ hẳn)
        if (sr != null)
        {
            Color c = sr.color;
            float alphaProgress = Mathf.Clamp01((progress - 0.4f) / 0.6f);
            c.a = Mathf.Lerp(0.85f, 0f, alphaProgress * alphaProgress);
            sr.color = c;
        }
    }
}
