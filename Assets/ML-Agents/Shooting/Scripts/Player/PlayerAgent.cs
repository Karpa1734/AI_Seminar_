using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.UI;

public class PlayerAgent : Agent
{
    [Header("Movement Settings")]
    public float normalSpeed = 5f;
    public float slowSpeed = 2.5f; // ★追加：低速時の速度（通常時の半分程度が一般的です）
    private Rigidbody2D rb;
    private BulletSpawner spawner;

    [Header("Life Settings")]
    public float maxHealth = 100f;
    private float currentHealth;
    // ボスが観測するために公開するプロパティ
    public float CurrentHealthRatio => Mathf.Clamp01(currentHealth / maxHealth);

    [Header("Stage Bounds (Vertical STG)")]
    public float minX = -4.5f;
    public float maxX = 4.5f;
    public float minY = -8f;
    public float maxY = 8f;

    [Header("UI References")]
    [SerializeField] private Slider hpSlider;
    private float targetHpRatio = 1f;

    [Header("Performance Tracking (能力測定用)")]
    private int grazeCount = 0;
    private float lastHitTime = 0f;
    private float totalMovement = 0f;
    private Vector3 lastPosition;
    [Header("Heatmap Settings")]
    private float[,] heatmap = new float[16, 16]; // 5x5のグリッド
    private float currentHeat = 0f;
    [Header("Targeting (追加)")]
    public Transform bossTransform; // インスペクターでボスをアタッチしてください
    public float CurrentHeat => currentHeat;
    [Header("Risk Sensor Settings")]
    public float riskDetectionRadius = 2.0f; // どの程度の範囲を「近く」とするか
    private int currentRiskCount = 0;
    public int CurrentRiskCount => currentRiskCount;
    [Header("Skill Metrics")]
    private int hitCountInPeriod = 0; // 一定期間内の被弾数
    public int HitCountInPeriod => hitCountInPeriod;
    // ボスが観測するために正規化したリスク値を返す
    public float GetNormalizedRisk() => Mathf.Clamp01(currentRiskCount / 20f);
    // ボスAIが参照する能力値
    public int GrazeCount => grazeCount;
    public float SurvivalTimeSinceLastHit => Time.time - lastHitTime;
    public float MovementActivity => totalMovement;

    private int shootFrameCounter = 0;
    public int HitsLandedOnBoss { get; private set; } // 敵に当てた弾数
    public void ResetHitCount() { hitCountInPeriod = 0; }
    public int RecentHitsLanded { get; private set; }
    public override void Initialize()
    {
        rb = GetComponent<Rigidbody2D>();
        spawner = GetComponent<BulletSpawner>();
    }

    public override void OnEpisodeBegin()
    {
        // 1. 弾のプールを掃除（どちらか片方のエージェントで呼べばOKですが、念のため）
        BulletPool.Instance?.DeactivateAll();
        System.Array.Clear(heatmap, 0, heatmap.Length);
        currentHeat = 0f;
        // 2. 自分のステータスリセット
        currentHealth = maxHealth;
        targetHpRatio = 1f;
        grazeCount = 0;
        lastHitTime = Time.time;
        totalMovement = 0f;
        shootFrameCounter = 0;
        hitCountInPeriod = 0;
        // 3. 位置と物理のリセット
        transform.localPosition = new Vector3(0, -6f, 0f); // 自機の初期位置（画面下部）
        if (rb != null) rb.linearVelocity = Vector2.zero;
        HitsLandedOnBoss = 0; // 毎エピソードリセット
        base.OnEpisodeBegin();
        // 4. UIのリセット
        if (hpSlider != null) hpSlider.value = 1f;
    }
    // エピソード終了時に呼ばれる
    public void FinalizeSkillEvaluation()
    {
        // 被弾が少なく、移動量が多く、グレイズが多いほど高評価
        float healthBonus = CurrentHealthRatio;
        float movementBonus = Mathf.Min(MovementActivity / 10f, 1f);
        float grazeBonus = Mathf.Min(grazeCount / 50f, 1f);

        // 簡易的な評価式: P = (H + M + G) / 3
        float performance = (healthBonus + movementBonus + grazeBonus) / 3f;

        GlobalSkillManager.Instance.UpdateSkill(performance);
    }
    void FixedUpdate()
    {
        UpdateHeatmap(); // 物理更新に合わせてヒートマップを更新
        // 物理演算のタイミングで周囲の弾をカウント
        CalculateRisk();
    }
    void Update()
    {
        if (hpSlider != null)
            hpSlider.value = Mathf.Lerp(hpSlider.value, targetHpRatio, Time.deltaTime * 8f);

        float dist = Vector3.Distance(transform.localPosition, lastPosition);
        totalMovement = Mathf.Lerp(totalMovement, dist / Time.deltaTime, Time.deltaTime * 2f);
        lastPosition = transform.localPosition;

        // ★Update内でも制限をかけることで、Heuristicモードでの突き抜けを防止
        ClampPosition();
    }
    private void CalculateRisk()
    {
        // プレイヤーの周囲にある「Enemy_Bullet」レイヤーのコライダーを取得
        int layerMask = LayerMask.GetMask("Player2_Bullet"); // ボスがPlayer2の場合
                                                             // ※環境に合わせて適切なレイヤーまたはタグを指定してください

        Collider2D[] bulletsAround = Physics2D.OverlapCircleAll(transform.position, riskDetectionRadius);

        int count = 0;
        foreach (var col in bulletsAround)
        {
            if (col.CompareTag("Enemy_Bullet")) count++;
        }
        currentRiskCount = count;
    }

    // デバッグ用：エディタ上でリスク範囲を視覚化
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, riskDetectionRadius);
    }
    private void ClampPosition()
    {
        transform.localPosition = new Vector3(
            Mathf.Clamp(transform.localPosition.x, minX, maxX),
            Mathf.Clamp(transform.localPosition.y, minY, maxY), 0);
    }


    public override void CollectObservations(VectorSensor sensor)
    {
        // 1-2. 座標
        sensor.AddObservation(transform.localPosition.x / maxX);
        sensor.AddObservation(transform.localPosition.y / maxY);
        // 3. HP割合
        sensor.AddObservation(CurrentHealthRatio);
        // 4. 移動活性度
        sensor.AddObservation(totalMovement);
        // 5. 生存時間
        sensor.AddObservation(SurvivalTimeSinceLastHit / 30f);
        // 6. ボスの残りHP割合（タイマーの残り時間をボスのHPとして観測）
        var timer = Object.FindFirstObjectByType<GameTimerManager>();
        if (timer != null)
        {
            sensor.AddObservation(timer.CurrentTimeRatio);
        }
        else
        {
            sensor.AddObservation(1f);
        }
        // ★ 7. 【追加】ボスとの水平相対距離 (Space Size を 7 に変更すること)
        // これにより、AIは「今、ボスの真下からどれだけズレているか」を理解できます
        if (bossTransform != null)
        {
            float relX = (bossTransform.localPosition.x - transform.localPosition.x) / maxX;
            sensor.AddObservation(relX);
        }
        else
        {
            sensor.AddObservation(0f);
        }
        // Playerの合計 Space Size: 5
    }
    // BossAgent 側からこのメソッドを呼んでカウントを増やす
    public override void OnActionReceived(ActionBuffers actions)
    {
        var CA = actions.ContinuousActions;
        var DA = actions.DiscreteActions;

        // --- 1. 低速移動の判定 ---
        // DA[1] が 1 なら低速モードを採用
        bool isSlowMode = (DA.Length > 1 && DA[1] == 1);
        float currentSpeed = isSlowMode ? slowSpeed : normalSpeed;

        // --- 2. 移動方向の決定 ---
        float moveX, moveY;

        // 学習中かつ、5%の確率でランダム移動（ここを1つにまとめました）
        if (Academy.Instance.IsCommunicatorOn && Random.value < 0.05f)
        {
            moveX = Random.Range(-1f, 1f);
            moveY = Random.Range(-1f, 1f);
        }
        else
        {
            moveX = CA[0];
            moveY = CA[1];
        }

        // ★ 修正ポイント： normalSpeed ではなく currentSpeed を掛ける！
        rb.linearVelocity = new Vector2(moveX, moveY) * currentSpeed;

        // --- 3. 画面外制限 (Clamp) ---
        ClampPosition();

        // --- 4. 射撃ロジック ---
        if (DA[0] == 1)
        {
            shootFrameCounter++;
            if (shootFrameCounter >= 4)
            {
                if (spawner != null) spawner.FireSimpleVertical(10f);
                shootFrameCounter = 0;
            }
        }
        else
        {
            shootFrameCounter = 3;
        }
        // --- 4. 【追加】陣取り報酬（ポジショニング） ---
        if (bossTransform != null)
        {
            float diffX = Mathf.Abs(transform.localPosition.x - bossTransform.localPosition.x);

            // ボスの真下（距離1.0以内）を維持している場合、生存報酬(0.003)を超えるボーナス
            if (diffX < 1.0f)
            {
                AddReward(0.003f); // 積極的に真下を狙う動機付け
            }
            // 逆に端（距離3.0以上）に逃げている場合は微減点
            else if (diffX > 3.0f)
            {
                AddReward(-0.001f);
            }
        }
        // 生存報酬
        AddReward(0.003f);
    }
    public void RegisterBulletHit(EnemyBullet eb)
    {
        if (eb == null) return;

        // 1. 被弾カウントの更新（ボスAIへのフィードバック用）
        hitCountInPeriod++;

        // 2. プレイヤーへの報酬ペナルティ（回避を学ばせる）
        // 論文的アプローチに基づき、大きめのマイナスを与えます
        AddReward(-2.0f);

        // 3. ステータスの更新
        currentHealth = Mathf.Max(0, currentHealth - eb.DamageValue);
        targetHpRatio = CurrentHealthRatio;
        lastHitTime = Time.time;

        // 4. ヒートマップのペナルティ（オプション：被弾した場所を「悪い場所」として熱を上げる）
        // UpdateHeatmap() のインデックス計算と同様の処理で、その場所のHeatを最大にしても良いです

        // 5. 弾を非アクティブ化してプールに戻す
        eb.Deactivate();
    }
    private void UpdateHeatmap()
    {
        // 座標を 0.0 ～ 1.0 に正規化
        float xPercent = Mathf.InverseLerp(minX, maxX, transform.localPosition.x);
        float yPercent = Mathf.InverseLerp(minY, maxY, transform.localPosition.y);

        // グリッドのインデックスを計算
        int gridX = Mathf.Clamp(Mathf.FloorToInt(xPercent * 5), 0, 4);
        int gridY = Mathf.Clamp(Mathf.FloorToInt(yPercent * 5), 0, 4);

        // 現在のマスの熱を上げる
        heatmap[gridX, gridY] += Time.deltaTime * 0.5f; // 上昇速度

        // 全体の熱を徐々に冷ます（拡散・冷却）
        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                heatmap[i, j] = Mathf.Max(0, heatmap[i, j] - Time.deltaTime * 0.1f);
            }
        }

        currentHeat = Mathf.Clamp01(heatmap[gridX, gridY]);
    }
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var CA = actionsOut.ContinuousActions;
        var DA = actionsOut.DiscreteActions;

        CA[0] = Input.GetAxisRaw("Horizontal");
        CA[1] = Input.GetAxisRaw("Vertical");

        // Zキーで射撃
        DA[0] = Input.GetKey(KeyCode.Z) ? 1 : 0;

        // ★ 左シフトキーで低速移動
        DA[1] = Input.GetKey(KeyCode.LeftShift) ? 1 : 0;
    }
    public void RegisterHit()
    {
        RecentHitsLanded++;
        // (既存の合計ヒット数カウントなどの処理)
        HitsLandedOnBoss++;
    }

    // 評価が終わった後に BossAgent から呼び出してリセットする
    public void ResetRecentHits()
    {
        RecentHitsLanded = 0;
    }
    public void RegisterGraze()
    {
        grazeCount++;
        // グレイズ報酬を大幅に下げ、被弾リスクに見合わないようにする
        AddReward(0.001f);
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Enemy_Bullet"))
        {
            var eb = col.GetComponent<EnemyBullet>();
            if (eb != null)
            {
                hitCountInPeriod++;

                // プレイヤー自身へのペナルティは維持（回避を学ばせるため）
                AddReward(-2.0f);

                // HPは減らすが、0になっても EndEpisode() は呼ばない
                currentHealth = Mathf.Max(0, currentHealth - eb.DamageValue);
                targetHpRatio = CurrentHealthRatio;
                lastHitTime = Time.time;

                eb.Deactivate();
            }
        }
    }
}