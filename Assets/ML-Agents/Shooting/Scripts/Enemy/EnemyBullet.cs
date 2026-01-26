using UnityEngine;

public class EnemyBullet : MonoBehaviour
{
    [Header("Screen Bounds")]
    public float minX = -5.5f; public float maxX = 5.5f;
    public float minY = -9.5f; public float maxY = 9.5f;

    [Header("Collision Settings")]
    [SerializeField] private LayerMask playerLayerMask; // Inspectorで"Player"レイヤーを設定

    [Header("Materials")]
    [SerializeField] private Material additiveMaterial;
    private Material defaultMaterial;

    private SpriteRenderer spriteRenderer;
    private CircleCollider2D circleCollider;
    private CapsuleCollider2D capsuleCollider;

    private float currentSpeed;
    private float currentAngle;
    private BulletData originData;
    private float lifeTimer = 0f;
    private Vector3 velocity;

    private BulletSpawner.TeamSide myTeam;
    private BulletSpawner mySpawner;
    private float angularVelocity; // フィールドに追加
    public Vector3 Velocity => velocity;
    public int DamageValue => (originData != null) ? originData.damage : 1;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        circleCollider = GetComponent<CircleCollider2D>();
        capsuleCollider = GetComponent<CapsuleCollider2D>();
        if (spriteRenderer != null) defaultMaterial = spriteRenderer.material;
    }

    public void Setup(BulletData data, Vector3 pos, float speed, float spAcc, float spMax, float angle, float anAcc, float anMax, int order, BulletSpawner.TeamSide team, BulletSpawner spawner)
    {
        this.originData = data;
        this.myTeam = team;
        this.mySpawner = spawner;
        this.transform.position = pos;
        this.currentSpeed = speed;
        this.currentAngle = angle;
        this.lifeTimer = 0f;
        this.currentAngle = angle;
        this.angularVelocity = anAcc; // 引数から受け取る
        if (data != null && spriteRenderer != null)
        {
            spriteRenderer.sprite = data.bulletSprite;
            spriteRenderer.sortingOrder = order;
            transform.localScale = data.localScale;
            spriteRenderer.material = (data.isAdditive && additiveMaterial != null) ? additiveMaterial : defaultMaterial;
            ConfigureCollider(data);
        }

        UpdateVelocityAndRotation();
    }

    private void ConfigureCollider(BulletData data)
    {
        if (data.colliderShape == ColliderShape.Circle)
        {
            if (circleCollider != null)
            {
                circleCollider.enabled = true;
                circleCollider.radius = data.colliderRadius;
                circleCollider.offset = data.colliderOffset;
            }
            if (capsuleCollider != null) capsuleCollider.enabled = false;
        }
        else
        {
            if (capsuleCollider != null)
            {
                capsuleCollider.enabled = true;
                capsuleCollider.size = new Vector2(data.colliderRadius * 2f, data.capsuleHeight);
                capsuleCollider.direction = CapsuleDirection2D.Vertical;
            }
            if (circleCollider != null) circleCollider.enabled = false;
        }
    }

    void Update()
    {
        if (originData != null && originData.bulletLifespan > 0)
        {
            lifeTimer += Time.deltaTime;
            if (lifeTimer >= originData.bulletLifespan) { Deactivate(); return; }
        }
        //currentAngle += angularVelocity * Time.deltaTime;
        UpdateVelocityAndRotation();

        // --- 軌跡補完判定 (トンネル現象対策) ---
        // 前回のフレームからの移動距離分を CircleCast でなぞる
        float moveDistance = velocity.magnitude * Time.deltaTime;
        if (moveDistance > 0 && originData != null)
        {
            RaycastHit2D hit = Physics2D.CircleCast(
                transform.position,
                originData.colliderRadius * transform.localScale.x, // 弾の半径
                velocity.normalized,
                moveDistance,
                playerLayerMask
            );

            if (hit.collider != null)
            {
                OnHitPlayer(hit.collider);
                return; // 当たったので移動処理はスキップ
            }
        }

        transform.position += velocity * Time.deltaTime;
        CheckOutOfBounds();
    }

    private void OnHitPlayer(Collider2D playerCol)
    {
        var player = playerCol.GetComponent<PlayerAgent>();
        if (player != null)
        {
            // 前回決めた PlayerAgent の被弾メソッドを呼ぶ
            player.RegisterBulletHit(this);
            Deactivate();
        }
    }

    private void UpdateVelocityAndRotation()
    {
        float rad = currentAngle * Mathf.Deg2Rad;
        velocity = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * currentSpeed;
        transform.rotation = Quaternion.Euler(0, 0, currentAngle - 90f);
    }

    private void CheckOutOfBounds()
    {
        Vector3 p = transform.position;
        if (p.x < minX || p.x > maxX || p.y < minY || p.y > maxY) Deactivate();
    }

    public void Deactivate()
    {
        if (BulletPool.Instance != null) BulletPool.Instance.ReturnToPool(gameObject);
    }
}