using UnityEngine;

public class EnemyBullet : MonoBehaviour
{
    [Header("Screen Bounds")]
    public float minX = -10f;
    public float maxX = 10f;
    public float minY = -6f;
    public float maxY = 6f;
    // --- スクリプト上部に追加 ---
    private GameObject deathEffectPrefab; // インスペクターでプレハブを指定

    [Header("Materials")]
    [SerializeField] private Material additiveMaterial; // 加算用マテリアル
    private Material defaultMaterial;

    // Parameters set by spawner
    private float currentSpeed;
    private float speedAcc;
    private float maxSpeed;
    private float currentAngle;
    private float angleAcc;
    private float maxAngle;
    private bool hasAngleLimit = false;
    private bool shouldRotate = true; // 画像を進行方向に向けるか

    private SpriteRenderer spriteRenderer;
    private CircleCollider2D circleCollider;

    // internal
    private Vector3 velocity = Vector3.zero;
    private Vector3 lastVelocity = Vector3.zero;
    private Vector3 _calculatedAcceleration;
    private BulletData originData;
    private int nextStepIndex = 0;
    private int framesSinceSpawn = 0; // float timeSinceSpawn から変更

    // Expose for Agent observation
    public Vector3 Velocity => velocity;
    public Vector3 Acceleration => _calculatedAcceleration;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        circleCollider = GetComponent<CircleCollider2D>();
        if (spriteRenderer != null) defaultMaterial = spriteRenderer.material;
    }

    public void Setup(BulletData data, Vector3 pos, float initSpeed, float spAcc, float spMax, float initAngle, float anAcc, float anMax, int orderInLayer)
    {
        originData = data;
        nextStepIndex = 0;
        framesSinceSpawn = 0; // Delayを含まないカウント開始

        if (data != null && spriteRenderer != null)
        {
            deathEffectPrefab = data.deathEffectPrefab;
            spriteRenderer.sprite = data.bulletSprite;
            spriteRenderer.sortingLayerName = "Middle";
            spriteRenderer.sortingOrder = orderInLayer;
            transform.localScale = data.localScale;

            // 加算合成の適用
            spriteRenderer.material = (data.isAdditive && additiveMaterial != null) ? additiveMaterial : defaultMaterial;

            if (circleCollider != null)
            {
                circleCollider.radius = data.colliderRadius;
                circleCollider.offset = data.colliderOffset;
            }
        }

        // 移動パラメータ初期化
        transform.position = pos;
        currentSpeed = initSpeed;
        speedAcc = spAcc;
        maxSpeed = spMax;
        currentAngle = initAngle;
        angleAcc = anAcc;
        maxAngle = anMax;
        hasAngleLimit = (anMax != 0);
        Debug.Log($"[Bullet Setup] Speed: {this.currentSpeed}, Cap: {this.maxSpeed}");

        UpdateVelocityAndRotation();
    }

    void Update()
    {
        // 多段変化チェック
        if (originData != null && nextStepIndex < originData.changeSteps.Count)
        {
            if (framesSinceSpawn >= originData.changeSteps[nextStepIndex].triggerFrame)
            {
                ApplyNextStep(originData.changeSteps[nextStepIndex]);
                nextStepIndex++;
            }
        }

        // 移動更新
        currentSpeed += speedAcc * Time.deltaTime;
        if (maxSpeed > 0f && currentSpeed > maxSpeed) currentSpeed = maxSpeed;

        // 角度更新ロジック (省略せず既存のものを維持)
        currentAngle += angleAcc * Time.deltaTime;

        UpdateVelocityAndRotation();
        transform.position += velocity * Time.deltaTime;

        framesSinceSpawn++; // フレーム加算
        CheckOutOfBounds();
    }

    private void ApplyNextStep(BulletChangeStep step)
    {
        // 多段変化の適用
        if (step.newSprite != null) spriteRenderer.sprite = step.newSprite;
        if (step.newColliderRadius > 0) circleCollider.radius = step.newColliderRadius;
        if (step.newScale != Vector3.zero) transform.localScale = step.newScale;

        if (step.changeTrajectory)
        {
            currentSpeed = step.newSpeed;
            speedAcc = step.newSpeedAcc;
            if (step.isAbsoluteAngle) currentAngle = step.newAngleOffset;
            else currentAngle += step.newAngleOffset;
        }
    }
    private void UpdateVelocityAndRotation()
    {
        // 速度ベクトルの計算
        float rad = currentAngle * Mathf.Deg2Rad;
        velocity = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * currentSpeed;

        // ★画像の向きを更新
        if (shouldRotate)
        {
            // Unityの0度は右(X+)なので、上を向いたスプライトを合わせるために-90度する
            transform.rotation = Quaternion.Euler(0, 0, currentAngle - 90f);
        }
    }
    private void CheckOutOfBounds()
    {
        Vector3 p = transform.position;
        if (p.x < minX || p.x > maxX || p.y < minY || p.y > maxY)
        {
            // × BulletPool.Instance.ReturnAllBullets(); // これだと全部消える
            BulletPool.Instance.ReturnToPool(gameObject); // ◎ 自分だけを戻す
        }
    }

    // --- 既存の Deactivate メソッドを修正 ---
    public void Deactivate()
    {
        // プールに戻る前にエフェクトを発生させる
        SpawnDeathEffect();
        BulletPool.Instance.ReturnToPool(gameObject);
    }


    // --- エフェクト生成用のヘルパーメソッドを追加 ---
    private void SpawnDeathEffect()
    {
        if (deathEffectPrefab != null && originData != null)
        {
            // エフェクトを生成
            GameObject effect = Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);

            // --- 大きさ（分類）の同期 ---
            // 弾のデータにある localScale をエフェクトにも適用
            effect.transform.localScale = originData.localScale;
        }
    }
}