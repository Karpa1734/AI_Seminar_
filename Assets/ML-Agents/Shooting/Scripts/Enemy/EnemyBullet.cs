using UnityEngine;

public class EnemyBullet : MonoBehaviour
{
    [Header("Screen Bounds")]
    public float minX = -10f;
    public float maxX = 10f;
    public float minY = -6f;
    public float maxY = 6f;

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

    // Expose for Agent observation
    public Vector3 Velocity => velocity;
    public Vector3 Acceleration => _calculatedAcceleration;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        circleCollider = GetComponent<CircleCollider2D>();
    }

    public void Setup(BulletData data, Vector3 pos, float initSpeed, float spAcc, float spMax, float initAngle, float anAcc, float anMax, int orderInLayer)
    {
        if (data != null && spriteRenderer != null)
        {
            spriteRenderer.sprite = data.bulletSprite;

            // Sorting Layer を「Middle」に設定し、計算された Order を適用
            spriteRenderer.sortingLayerName = "Middle";
            spriteRenderer.sortingOrder = orderInLayer;

            transform.localScale = data.localScale;
            if (circleCollider != null)
            {
                circleCollider.radius = data.colliderRadius;
                circleCollider.offset = data.colliderOffset;
            }
        }

        // 2. 移動パラメータの設定
        transform.position = pos;
        currentSpeed = initSpeed;
        speedAcc = spAcc;
        maxSpeed = spMax;
        currentAngle = initAngle;
        angleAcc = anAcc;
        maxAngle = anMax;

        hasAngleLimit = (anMax != 0);

        UpdateVelocityAndRotation(); // 初回の向きと速度を計算
    }

    void Update()
    {
        lastVelocity = velocity;

        // --- 速度の更新 ---
        currentSpeed += speedAcc * Time.deltaTime;
        if (maxSpeed > 0f && currentSpeed > maxSpeed) currentSpeed = maxSpeed;

        // --- 角度の更新（制限ロジック含む） ---
        if (hasAngleLimit && angleAcc != 0)
        {
            float deltaToTarget = Mathf.DeltaAngle(currentAngle, maxAngle);
            float frameRotation = angleAcc * Time.deltaTime;

            if (Mathf.Abs(deltaToTarget) <= Mathf.Abs(frameRotation))
            {
                currentAngle = maxAngle;
                angleAcc = 0;
            }
            else
            {
                currentAngle += frameRotation;
            }
        }
        else
        {
            currentAngle += angleAcc * Time.deltaTime;
        }

        // --- 速度ベクトルと回転の同時更新 ---
        UpdateVelocityAndRotation();

        // 移動の適用
        transform.position += velocity * Time.deltaTime;

        // AI用の加速度計算
        _calculatedAcceleration = (velocity - lastVelocity) / Time.deltaTime;

        CheckOutOfBounds();
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

    public void Deactivate()
    {
        // × BulletPool.Instance.ReturnAllBullets(); // これだと全部消える
        BulletPool.Instance.ReturnToPool(gameObject); // ◎ 自分だけを戻す
    }
}