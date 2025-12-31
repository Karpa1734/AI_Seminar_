using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using System.Linq;

public class DodgerAgent : Agent
{
    [Header("Target Settings")]
    public Transform enemyTransform;

    [Header("Movement Settings")]
    public float normalSpeed = 5f;
    public float slowSpeed = 2f;
    private Rigidbody2D rb;

    [Header("Observation Settings")]
    public int observeBulletCount = 10;
    public float maxDetectionRadius = 15f;

    [Header("Status Settings")]
    public float stunDuration = 2.0f;
    public float invincibilityDuration = 5.0f;
    private float stunTimer = 0f;
    private float invincibilityTimer = 0f;
    private SpriteRenderer spriteRenderer;

    [Header("Life Settings")]
    public int maxHealth = 3;
    private int currentHealth;

    [Header("Reward Weights")]
    public float survivalReward = 0.002f;
    public float grazeReward = 0.005f;
    public float hitPenalty = -0.5f;
    public float gameOverPenalty = -1.0f;
    public float clearBonus = 2.0f;
    public float wallPenaltyScale = 0.01f;
    public float efficiencyReward = -0.001f;

    [Header("Positioning Reward Weights")]
    public float oppositeSideRewardScale = 0.01f;
    public float centerYMaintenanceScale = 0.005f;
    public float invincibilityEscapeBonus = 0.05f;
    public float cautionDistance = 8.0f;
    public float proximityPenaltyScale = 0.05f;

    [Header("Stage Bounds")]
    public float minX = -9f;
    public float maxX = 9f;
    public float minY = -5f;
    public float maxY = 5f;

    // --- 追加：攻撃管理用の変数 ---
    private BulletSpawner spawner;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        spawner = GetComponent<BulletSpawner>(); // スパウナーを取得

        // CharacterDataから移動速度を同期
        if (spawner != null && spawner.characterProfile != null)
        {
            normalSpeed = spawner.characterProfile.normalSpeed;
            slowSpeed = spawner.characterProfile.slowSpeed;
        }
    }

    public override void OnEpisodeBegin()
    {
        currentHealth = maxHealth;

        // 1. BulletSpawnerがまだ取得できていない場合の再取得
        if (spawner == null) spawner = GetComponent<BulletSpawner>();

        // 2. 初期位置の決定
        // ステージ端からの距離（例：中央から左右に6ユニット離れた場所）
        float offsetFromCenter = 6.0f;
        float startX = 0f;

        // チーム設定に基づいて左右を振り分け
        if (spawner != null)
        {
            // Player1なら左(-6)、Player2なら右(+6)
            startX = (spawner.myTeam == BulletSpawner.TeamSide.Player1) ? -offsetFromCenter : offsetFromCenter;
        }

        // 3. 座標の適用
        // ※親子関係がある場合は transform.localPosition、ない場合は transform.position を使います
        transform.localPosition = new Vector3(startX, 0f, 0f);

        // 物理挙動のリセット
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        stunTimer = 0f;
        invincibilityTimer = 0f;

        // エピソード開始時に画面内の弾を掃除
        if (BulletPool.Instance != null)
        {
            BulletPool.Instance.ReturnAllBullets();
        }
    }

    private void ClearAllActiveBullets()
    {
        if (BulletPool.Instance != null)
        {
            BulletPool.Instance.ReturnAllBullets();
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 1. 基本ステータス
        sensor.AddObservation(transform.localPosition.x / maxX);
        sensor.AddObservation(transform.localPosition.y / maxY);
        sensor.AddObservation(stunTimer > 0);
        sensor.AddObservation(invincibilityTimer > 0);
        sensor.AddObservation((float)currentHealth / maxHealth);

        // 2. 攻撃のリキャスト状況（0:撃てる, 1:リキャスト中）
        for (int i = 0; i < 4; i++)
        {
            sensor.AddObservation(spawner.GetRecastProgress(i));
        }

        // 3. 敵の情報
        if (enemyTransform != null)
        {
            sensor.AddObservation(enemyTransform.localPosition.x / maxX);
            Vector2 relativeEnemyPos = (Vector2)(enemyTransform.position - transform.position);
            sensor.AddObservation(relativeEnemyPos / maxDetectionRadius);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(Vector2.zero);
        }

        // 4. 弾の観測（10発分、常に60個のデータを送るように固定）
        string targetLayer = (spawner.myTeam == BulletSpawner.TeamSide.Player1) ? "Player2_Bullet" : "Player1_Bullet";
        var bullets = GameObject.FindGameObjectsWithTag("Enemy_Bullet")
            .Where(b => b.layer == LayerMask.NameToLayer(targetLayer))
            .OrderBy(b => Vector2.Distance(b.transform.position, transform.position))
            .Take(observeBulletCount)
            .ToList();

        foreach (var b in bullets)
        {
            Vector2 relativePos = (Vector2)(b.transform.position - transform.position);
            sensor.AddObservation(relativePos / maxDetectionRadius); // +2

            var eb = b.GetComponent<EnemyBullet>();
            if (eb != null)
            {
                // Vector3のままだと3個送られるため、(Vector2)でキャストして2個に固定する
                sensor.AddObservation((Vector2)eb.Velocity / 10f);     // +2
                sensor.AddObservation((Vector2)eb.Acceleration / 5f); // +2
            }
            else
            {
                sensor.AddObservation(Vector2.zero); // +2
                sensor.AddObservation(Vector2.zero); // +2
            }
        }

        // 足りない分を埋める（ここも常に合計6個になるようにする）
        int missingBullets = observeBulletCount - bullets.Count;
        for (int i = 0; i < missingBullets; i++)
        {
            sensor.AddObservation(Vector2.zero); // 相対位置分 (+2)
            sensor.AddObservation(Vector2.zero); // 速度分 (+2)
            sensor.AddObservation(Vector2.zero); // 加速度分 (+2)
        }

    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (StepCount >= MaxStep - 1 && MaxStep > 0)
        {
            SetReward(clearBonus);
            EndEpisode();
            return;
        }

        // --- ステータス更新 ---
        bool canMove = true;
        if (stunTimer > 0)
        {
            stunTimer -= Time.deltaTime;
            canMove = false;
            if (rb != null) rb.linearVelocity = Vector2.zero;
            if (spriteRenderer != null) spriteRenderer.color = Color.red;
            if (stunTimer <= 0) invincibilityTimer = invincibilityDuration;
        }
        else if (invincibilityTimer > 0)
        {
            invincibilityTimer -= Time.deltaTime;
            if (spriteRenderer != null) { Color c = Color.cyan; c.a = 0.5f; spriteRenderer.color = c; }
        }
        else
        {
            if (spriteRenderer != null) spriteRenderer.color = Color.white;
        }

        // --- 行動：移動 (Continuous Actions) ---
        if (canMove)
        {
            var CA = actions.ContinuousActions;
            float moveX = Mathf.Clamp(CA[0], -1f, 1f);
            float moveY = Mathf.Clamp(CA[1], -1f, 1f);
            bool isSlow = CA.Length >= 3 && CA[2] > 0.5f;
            float speed = isSlow ? slowSpeed : normalSpeed;
            Vector2 moveInput = new Vector2(moveX, moveY);
            if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();
            if (rb != null) rb.linearVelocity = moveInput * speed;

            Vector3 pos = transform.localPosition;
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
            transform.localPosition = pos;
        }

        // --- 行動：攻撃 (Discrete Actions Branch 0) ---
        // 0:なし, 1:Z, 2:X, 3:C, 4:V
        // DodgerAgent.cs の OnActionReceived 内の攻撃処理部分
        if (canMove)
        {
            int attackAction = actions.DiscreteActions[0]; // 0:なし, 1:Z, 2:X, 3:C, 4:V

            // 毎フレーム、現在どのアクション（ボタン）を選択しているかをスパウナーに伝える
            // これにより「長押しでの連射」と「指を離した時の中断」が同期します
            if (spawner != null)
            {
                spawner.UpdateInputState(attackAction);
            }
        }

        // --- 報酬 ---
        AddReward(survivalReward);
        if (canMove)
        {
            ApplyPositioningRewards(actions.ContinuousActions);
            GameObject closestBullet = GetClosestBullet();
            if (closestBullet != null)
            {
                float dist = Vector2.Distance(transform.position, closestBullet.transform.position);
                if (dist < 0.8f) AddReward(grazeReward * (0.8f - dist));
            }
        }
    }

    private void ApplyPositioningRewards(ActionSegment<float> actions)
    {
        if (enemyTransform != null)
        {
            float xDistToEnemy = Mathf.Abs(transform.localPosition.x - enemyTransform.localPosition.x);
            float distRatio = xDistToEnemy / (maxX * 2);
            AddReward(Mathf.Pow(distRatio, 2) * oppositeSideRewardScale);
            if (invincibilityTimer > 0) AddReward(distRatio * invincibilityEscapeBonus);

            float distToEnemy = Vector2.Distance(transform.position, enemyTransform.position);
            if (distToEnemy < cautionDistance)
            {
                float proximityIntensity = (cautionDistance - distToEnemy) / cautionDistance;
                AddReward(-Mathf.Pow(proximityIntensity, 2) * proximityPenaltyScale);
            }
        }
        float centerYDist = Mathf.Abs(transform.localPosition.y);
        AddReward(-Mathf.Pow(centerYDist / maxY, 2) * centerYMaintenanceScale);
        float wallDistX = Mathf.Abs(transform.localPosition.x) / maxX;
        float wallDistY = Mathf.Abs(transform.localPosition.y) / maxY;
        if (wallDistX > 0.8f || wallDistY > 0.8f) AddReward(-(wallDistX + wallDistY) * wallPenaltyScale);
        Vector2 moveInput = new Vector2(actions[0], actions[1]);
        AddReward(moveInput.sqrMagnitude * efficiencyReward);
    }

    private GameObject GetClosestBullet()
    {
        // 自分に当たるレイヤーの弾だけを判定対象にする
        string targetLayer = (spawner.myTeam == BulletSpawner.TeamSide.Player1) ? "Player2_Bullet" : "Player1_Bullet";
        var bullets = GameObject.FindGameObjectsWithTag("Enemy_Bullet")
            .Where(b => b.layer == LayerMask.NameToLayer(targetLayer));

        if (!bullets.Any()) return null;

        GameObject closest = null; float minDist = float.MaxValue;
        foreach (var b in bullets)
        {
            float d = Vector2.Distance(transform.position, b.transform.position);
            if (d < minDist) { minDist = d; closest = b; }
        }
        return closest;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // 1. 移動
        var CA = actionsOut.ContinuousActions;
        CA[0] = Input.GetAxisRaw("Horizontal");
        CA[1] = Input.GetAxisRaw("Vertical");
        CA[2] = Input.GetKey(KeyCode.LeftShift) ? 1f : 0f;

        // 2. 攻撃
        var DA = actionsOut.DiscreteActions;
        DA[0] = 0;
        if (Input.GetKey(KeyCode.Z)) DA[0] = 1;
        else if (Input.GetKey(KeyCode.X)) DA[0] = 2;
        else if (Input.GetKey(KeyCode.C)) DA[0] = 3;
        else if (Input.GetKey(KeyCode.V)) DA[0] = 4;
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (stunTimer > 0 || invincibilityTimer > 0) return;

        if (col.CompareTag("Enemy_Bullet"))
        {
            ClearAllActiveBullets();
            currentHealth--;
            AddReward(hitPenalty);

            if (currentHealth <= 0)
            {
                SetReward(gameOverPenalty);
                EndEpisode();
            }
            else
            {
                stunTimer = stunDuration;
            }
        }
    }
}