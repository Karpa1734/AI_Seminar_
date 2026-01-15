using Unity.AppUI.UI;
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
    private int frameCounter = 0;
    private BulletData currentData;
    private float lifeTimer = 0f;
    private int subSpawnFrameCounter = 0;

    private SpriteRenderer spriteRenderer;

    [Header("Collision Components")]
    [SerializeField] private CircleCollider2D circleCollider;
    [SerializeField] private CapsuleCollider2D capsuleCollider; // ★追加
    // internal
    private Vector3 velocity = Vector3.zero;
    private Vector3 lastVelocity = Vector3.zero;
    private Vector3 _calculatedAcceleration;
    private BulletData originData;
    private int nextStepIndex = 0;
    private int framesSinceSpawn = 0; // float timeSinceSpawn から変更
    private int currentSortingOrder; // ★追加：現在の描画順を保持
    private bool isPreparing = false;
    private int prepFrameCount = 0;
    private Vector3 originalLocalPos;
    private BulletSpawner mySpawner;
    // メンバ変数に追加
    private BulletSpawner.TeamSide myTeam;
    // Expose for Agent observation
    public Vector3 Velocity => velocity;
    public Vector3 Acceleration => _calculatedAcceleration;
    // 外部（PlayerHealth）からダメージ量を読み取るためのプロパティ
    public int DamageValue => (originData != null) ? originData.damage : 1;// EnemyBullet.cs 内の推奨修正
    private float subSpawnTimer = 0f;
    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (circleCollider == null) circleCollider = GetComponent<CircleCollider2D>();
        if (capsuleCollider == null) capsuleCollider = GetComponent<CapsuleCollider2D>();
        if (spriteRenderer != null) defaultMaterial = spriteRenderer.material;
    }

    public void Setup(BulletData data, Vector3 pos, float initSpeed, float spAcc, float spMax, float initAngle, float anAcc, float anMax, int orderInLayer, BulletSpawner.TeamSide team, BulletSpawner spawner)
    {
        this.mySpawner = spawner;
        this.myTeam = team; // 自分のチームを記憶
        originData = data;
        nextStepIndex = 0;
        framesSinceSpawn = 0; // Delayを含まないカウント開始
        currentSortingOrder = orderInLayer; // 描画順を保存

        if (data != null && spriteRenderer != null)
        {
            deathEffectPrefab = data.deathEffectPrefab;
            spriteRenderer.sprite = data.bulletSprite;
            spriteRenderer.sortingLayerName = "Middle";
            spriteRenderer.sortingOrder = currentSortingOrder;
            transform.localScale = data.localScale;

            // 加算合成の適用
            spriteRenderer.material = (data.isAdditive && additiveMaterial != null) ? additiveMaterial : defaultMaterial;

            // --- 当たり判定のパターン分け ---
            if (data.colliderShape == ColliderShape.Circle)
            {
                // 円形コライダーの設定
                if (circleCollider != null)
                {
                    circleCollider.enabled = true;
                    circleCollider.radius = data.colliderRadius;
                    circleCollider.offset = data.colliderOffset;
                }
                // カプセル型は無効化
                if (capsuleCollider != null) capsuleCollider.enabled = false;
            }
            else if (data.colliderShape == ColliderShape.Capsule)
            {
                // カプセル型コライダーの設定（槍などの細長い弾用）
                if (capsuleCollider != null)
                {
                    capsuleCollider.enabled = true;
                    // Width = 半径の2倍, Height = 設定された高さ
                    capsuleCollider.size = new Vector2(data.colliderRadius * 2f, data.capsuleHeight);
                    capsuleCollider.offset = data.colliderOffset;
                    // 槍のスプライト向き（上向き想定）に合わせて Vertical(縦) に固定
                    capsuleCollider.direction = CapsuleDirection2D.Vertical;
                }
                // 円形は無効化
                if (circleCollider != null) circleCollider.enabled = false;
            }
        }

        // 出現演出（Startup Effect）の初期化
        if (data != null && data.startupEffect.durationFrames > 0)
        {
            isPreparing = true;
            prepFrameCount = 0;
            ApplyStartupEffect(0f); // 初期状態を適用
        }
        else
        {
            isPreparing = false;
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
        subSpawnFrameCounter = 0; // カウンターをリセット
        lifeTimer = 0f;           // 寿命タイマーをリセット

        Debug.Log($"[Bullet Setup] Shape: {data.colliderShape}, Speed: {currentSpeed}, Order: {currentSortingOrder}");

        UpdateVelocityAndRotation();
    }
    void Update()
    {


        framesSinceSpawn++;

        if (isPreparing)
        {
            UpdatePreparation();
            UpdateDelayVisuals();
            return;
        }

        // --- 1. 寿命（Lifespan）の処理 ---
        if (originData != null && originData.bulletLifespan > 0)
        {
            lifeTimer += Time.deltaTime;
            if (lifeTimer >= originData.bulletLifespan)
            {
                Deactivate(); // 8秒などで消滅
                return;
            }
        }

        // 既存の多段変化チェック...
        if (originData != null && nextStepIndex < originData.changeSteps.Count) { /* ... */ }

        // 移動処理
        currentSpeed += speedAcc * Time.deltaTime;
        if (maxSpeed > 0f && currentSpeed > maxSpeed) currentSpeed = maxSpeed;
        currentAngle += angleAcc * Time.deltaTime;

        UpdateVelocityAndRotation();
        transform.position += velocity * Time.deltaTime;

        // --- 2. 子弾（Sub-Bullet）の生成処理 ---
        if (originData != null && originData.spawnSubBullets && originData.subShotData != null)
        {
            // フレームカウントではなく、経過時間で判定する
            subSpawnTimer += Time.deltaTime;

            float intervalSeconds = originData.spawnIntervalFrames / 60f;

            if (subSpawnTimer >= intervalSeconds)
            {
                subSpawnTimer = 0f;
                SpawnSubBullet();
            }
        }

        CheckOutOfBounds();
    }


    private void SpawnSubBullet()
    {
        if (mySpawner == null) return;

        mySpawner.ExecuteShot(
            originData.subShotData,
            0,
            0,
            this.transform.position,
            this.myTeam
        );
    }

    // 従来の溜め演出（色変化など）を維持するためのメソッド（例）
    private void UpdateDelayVisuals()
    {
        // スパナーやデータで定義された「溜め色」への変化などを継続
        if (originData != null && spriteRenderer != null)
        {
            // 準備（アニメーション）中も、設定されたDelayColorを反映させる
            // (既存のロジックに合わせて調整してください)
        }
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

    private void UpdatePreparation()
    {
        prepFrameCount++;
        float progress = (float)prepFrameCount / originData.startupEffect.durationFrames;

        ApplyStartupEffect(progress);

        if (prepFrameCount >= originData.startupEffect.durationFrames)
        {
            isPreparing = false;
            // 準備完了後に回転などをリセットしたい場合はここで調整
        }
    }

    private void ApplyStartupEffect(float t)
    {
        var effect = originData.startupEffect;
        float value = Mathf.Lerp(effect.startValue, effect.endValue, t);

        switch (effect.type)
        {
            case BulletStartupType.RotateX:
                transform.localRotation = Quaternion.Euler(value, 0, currentAngle - 90f);
                break;
            case BulletStartupType.RotateY:
                transform.localRotation = Quaternion.Euler(0, value, currentAngle - 90f);
                break;
            case BulletStartupType.RotateZ:
                transform.localRotation = Quaternion.Euler(0, 0, value);
                break;
            case BulletStartupType.MoveX:
                // 発射位置からの相対距離で動かす例
                transform.position += transform.right * (value * Time.deltaTime);
                break;
            case BulletStartupType.Scale:
                transform.localScale = originData.localScale * value;
                break;
        }
    }
    // --- エフェクト生成メソッドを修正 ---
    private void SpawnDeathEffect()
    {
        if (deathEffectPrefab != null && originData != null)
        {
            GameObject effect = Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);

            // --- 大きさの同期 ---
            effect.transform.localScale = originData.localScale * 2.5f;

            // --- レイヤーと描画順の同期 ★追加 ---
            // パーティクルシステムやスプライトなど、すべての Renderer を対象にする
            Renderer[] renderers = effect.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
            {
                r.sortingLayerName = "Middle"; // 弾と同じレイヤー
                r.sortingOrder = currentSortingOrder; // 弾と同じ順序
            }
        }
    }

}