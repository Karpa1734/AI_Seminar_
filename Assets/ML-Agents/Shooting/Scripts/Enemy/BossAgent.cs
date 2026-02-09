using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.UI;

public class BossAgent : Agent
{
    [Header("References")]
    public Transform playerTransform;
    private Rigidbody2D rb;
    private BulletSpawner spawner;
    [SerializeField] private GameTimerManager timerManager;

    [Header("Boss Stats")]
    public float maxHealth = 1000f;
    private float currentHealth;

    [Header("Energy Settings (スタミナ管理)")]
    public float maxEnergy = 100f;
    private float currentEnergy;
    public float energyRegenRate = 15f;    // 1秒間の回復量
    public float energyCostPerBullet = 2f; // 弾1発あたりのコスト
    [SerializeField] private Slider energySlider;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float minX = -4.5f; public float maxX = 4.5f;
    public float minY = 1f; public float maxY = 8f;
    private Vector3 lastPositionAtCheck;
    private float checkTimer = 0f;

    [Header("UI")]
    [SerializeField] private Slider bossHpSlider;
    [SerializeField] private float barSmoothSpeed = 10f;
    private float targetHpRatio = 1f;
    public float CurrentEnergyRatio => currentEnergy / maxEnergy;
    public float CurrentHealthRatio => currentHealth / maxHealth;
    private float fireCooldown = 0f;
    public float minFireInterval = 0.15f;
    public int LastChosenTactic { get; private set; }
    public Vector3 LastRemoteOffset { get; private set; }
    public int DebugMaxWayLimit { get; private set; }
    public float DebugSpeedMultiplier { get; private set; }
    public override void Initialize()
    {
        rb = GetComponent<Rigidbody2D>();
        spawner = GetComponent<BulletSpawner>();
        if (timerManager == null) timerManager = FindFirstObjectByType<GameTimerManager>();
    }

    public override void OnEpisodeBegin()
    {
        if (GlobalSkillManager.Instance != null && playerTransform != null)
        {
            var p = playerTransform.GetComponent<PlayerAgent>();

            // --- 総合評価（Performance Score）の計算 ---
            // A. 生存スコア (0.0 ～ 0.5)
            float survivalScore = p.CurrentHealthRatio * 0.5f;

            // B. 攻撃スコア (0.0 ～ 0.3)
            // エピソード中に 50発以上当てていれば満点とする
            float offenseScore = Mathf.Clamp01(p.HitsLandedOnBoss / 50f) * 0.3f;

            // C. 回避/スリルスコア (0.0 ～ 0.2)
            float evasionScore = p.GetNormalizedRisk() * 0.2f;

            // 合算 (0.0 ～ 1.0)
            float performance = survivalScore + offenseScore + evasionScore;

            GlobalSkillManager.Instance.UpdateSkill(performance);
        }
        // (中略) 2. ボス自身のステータスリセット
        currentEnergy = maxEnergy;
        // 1. 弾のプールを掃除（BulletPoolに全消去メソッドを作るのが理想）
        BulletPool.Instance?.DeactivateAll();

        // 2. ボス自身のステータスリセット
        currentHealth = maxHealth;
        currentEnergy = maxEnergy;
        targetHpRatio = 1f;
        fireCooldown = 0f;
        lastPositionAtCheck = transform.localPosition;

        // 3. 位置と物理のリセット
        transform.localPosition = new Vector3(0, 4f, 0f);
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // 4. UIのリセット
        if (bossHpSlider != null) bossHpSlider.value = 1f;
        if (energySlider != null) energySlider.value = 1f;
    }

    void Update()
    {
        // UIのスムーズな更新
        if (bossHpSlider != null)
            bossHpSlider.value = Mathf.Lerp(bossHpSlider.value, targetHpRatio, Time.deltaTime * barSmoothSpeed);

        // エネルギーの自動回復
        currentEnergy = Mathf.Min(currentEnergy + Time.deltaTime * energyRegenRate, maxEnergy);
        if (energySlider != null) energySlider.value = currentEnergy / maxEnergy;

        if (fireCooldown > 0) fireCooldown -= Time.deltaTime;

        ClampPosition();
        if (currentEnergy > maxEnergy * 0.5f)
        {
            AddReward(-0.0001f); // 塵も積もれば山となるペナルティ
        }

        if (GlobalSkillManager.Instance != null)
        {
            // [Keypad +] または [U] キーでスキルを 5% 上げる
            if (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.U))
            {
                float nextSkill = Mathf.Clamp(GlobalSkillManager.Instance.currentSkillLevel + 5f, 0f, 1f);
                GlobalSkillManager.Instance.UpdateSkill(nextSkill); //
                Debug.Log($"Debug: Skill Up -> {nextSkill * 100:F1}%");
            }

            // [Keypad -] または [J] キーでスキルを 5% 下げる
            if (Input.GetKeyDown(KeyCode.KeypadMinus) || Input.GetKeyDown(KeyCode.J))
            {
                float nextSkill = Mathf.Clamp(GlobalSkillManager.Instance.currentSkillLevel - 5f, 0f, 1f);
                GlobalSkillManager.Instance.UpdateSkill(nextSkill); //
                Debug.Log($"Debug: Skill Down -> {nextSkill * 100:F1}%");
            }
        }

        // 0.5秒ごとに移動評価と難易度調整（DDA）を実行
        checkTimer += Time.deltaTime;
        if (checkTimer >= 0.5f)
        {
            UpdateRunningSkill();
            EvaluateMovement();
            AnalyzePlayerPerformance();
            checkTimer = 0f;
        }
    }
    private void UpdateRunningSkill()
    {
        if (GlobalSkillManager.Instance == null || playerTransform == null) return;
        var p = playerTransform.GetComponent<PlayerAgent>();

        // --- 1. 評価基準の厳格化 ---

        // A. 生存評価 (0.0 ～ 0.4): 20秒耐えてようやく満点（以前は5秒）
        // これにより、頻繁に被弾する初心者はここが常に低い値（0.1～0.2）に留まります
        float survival = Mathf.Min(p.SurvivalTimeSinceLastHit / 20f, 1f) * 0.4f;

        // B. 攻撃評価 (0.0 ～ 0.3): 0.5秒間に 5発 以上当てて満点（以前は1発）
        // 適当に撃っているだけでは満点が取れず、正確なエイムを要求します
        float offense = Mathf.Clamp01(p.RecentHitsLanded / 5f) * 0.3f;

        // C. 回避評価 (0.0 ～ 0.3): リスク 0.4（高密度）で満点（以前は0.15）
        // 弾幕の薄い安全圏にいる初心者は、ここが 0 に近くなります
        float evasion = Mathf.Min(p.GetNormalizedRisk() / 0.4f, 1f) * 0.3f;

        // 理論上の最大値は 1.0。初心者は 0.2 ～ 0.3 程度に収束する設計です。
        float instantPerformance = survival + offense + evasion;

        // --- 2. 追従速度の維持 ---
        float currentSkill = GlobalSkillManager.Instance.currentSkillLevel;
        float lerpFactor = 0.35f;

        float updatedSkill = Mathf.Lerp(currentSkill, instantPerformance, lerpFactor);

        GlobalSkillManager.Instance.UpdateSkill(Mathf.Clamp01(updatedSkill));

        // カウンターのリセット
        p.ResetRecentHits();
    }
    // 観測 (Vector Observation) - Space Size: 13 に設定すること
    public override void CollectObservations(VectorSensor sensor)
    {
        // 1-2. 自分の座標 (正規化)
        sensor.AddObservation(new Vector2(transform.localPosition.x / maxX, transform.localPosition.y / maxY));
        // 3-4. 自分の速度
        sensor.AddObservation(rb.linearVelocity / moveSpeed);
        // 5. 自分のHP割合（またはタイマー）
        sensor.AddObservation(timerManager != null ? timerManager.CurrentTimeRatio : currentHealth / maxHealth);
        // 6. 自分のエネルギー割合
        sensor.AddObservation(currentEnergy / maxEnergy);

        if (playerTransform != null)
        {
            var p = playerTransform.GetComponent<PlayerAgent>();
            // 7-8. プレイヤーとの相対座標
            sensor.AddObservation((playerTransform.position - transform.position) / 15f);
            // 9-10. 活性度 / ニアミス
            sensor.AddObservation(p.MovementActivity);
            sensor.AddObservation(p.GrazeCount / 100f);
            // 11-12. 生存時間 / HP割合
            sensor.AddObservation(p.SurvivalTimeSinceLastHit / 30f);
            sensor.AddObservation(p.CurrentHealthRatio);
            // 13. リスク（論文指標）
            sensor.AddObservation(p.GetNormalizedRisk());

            // 15. プレイヤーの現在の滞在熱（Heat）
            sensor.AddObservation(playerTransform.GetComponent<PlayerAgent>().CurrentHeat);
        }
        else
        {
            sensor.AddObservation(new float[7]); // プレイヤー不在時の埋め合わせ
        }
    }

    // Unityエディタの Behavior Parameters 設定
    // Discrete Branches: 2 (Branch 0: 射撃[2], Branch 1: パターン選択[4])

    // BossAgent.cs の OnActionReceived 内

    public override void OnActionReceived(ActionBuffers actions)
    {
        var CA = actions.ContinuousActions;
        var DA = actions.DiscreteActions;
        LastChosenTactic = actions.DiscreteActions[1] % 4; // AIが選んだ技を保存
        LastRemoteOffset = new Vector3(actions.ContinuousActions[4] * 4f, actions.ContinuousActions[5] * 4f, 0); // 設置位置を保存
        // 1. 移動 (既存)
        rb.linearVelocity = new Vector2(CA[0], CA[1]) * moveSpeed;

        // 2. 空間制御
        Vector3 remoteOffset = new Vector3(CA[4] * 3f, CA[5] * 3f, 0);

        // 3. 技量の取得
        float playerSkill = (GlobalSkillManager.Instance != null) ? GlobalSkillManager.Instance.currentSkillLevel : 0.5f;

        // 4. 射撃判定と動的インターバル
        if (DA[0] == 1 && fireCooldown <= 0 && currentEnergy >= 10f)
        {
            int chosenTactic = DA[1] % 4;
            spawner.ExecuteTactic(chosenTactic, transform.position, remoteOffset, playerSkill);

            // --- エネルギーと報酬の計算 ---
            currentEnergy -= 0.8f * (1.0f + playerSkill * 0.01f);

            // ★ 発射間隔（Cooldown）を練度によって短縮
            // 0%時: 0.25秒間隔 (秒間4発)
            // 100%時: 0.08秒間隔 (秒間12.5発)
            fireCooldown = Mathf.Lerp(0.25f, 0.08f, playerSkill);

            AddReward(0.02f * (1.0f + playerSkill));
        }
        ClampPosition();
    }
    private void EvaluateStrategy(int tacticIndex)
    {
        var p = playerTransform.GetComponent<PlayerAgent>();

        // 例：プレイヤーが端にいるときに「端からの奇襲(Tactic 2)」を選んだら加点
        if (tacticIndex == 2 && Mathf.Abs(playerTransform.position.x) > 3.0f)
        {
            AddReward(0.2f); // 状況適合ボーナス
        }

        // 例：プレイヤーが「滞在熱(Heat)」が高い（一箇所に留まっている）ときに
        // 「高速中心弾(Tactic 3)」で動かそうとしたら加点
        if (tacticIndex == 3 && p.CurrentHeat > 0.7f)
        {
            AddReward(0.15f);
        }
    }
    private void EvaluateMovement()
    {
        float dist = Vector3.Distance(transform.localPosition, lastPositionAtCheck);
        if (dist > 1.5f) AddReward(0.05f);     // 積極移動ボーナス
        else if (dist < 0.2f) AddReward(-0.02f); // 停滞ペナルティ
        lastPositionAtCheck = transform.localPosition;
    }

    private void AnalyzePlayerPerformance()
    {
        var p = playerTransform.GetComponent<PlayerAgent>();
        float timeRatio = timerManager != null ? timerManager.CurrentTimeRatio : 1f;
        float risk = p.GetNormalizedRisk();
        float skillLevel = p.MovementActivity;

        // --- 時間帯によって目指すべきリスク（難易度）を変える ---
        float targetRisk = (timeRatio > 0.8f) ? 0.15f : 0.4f; // 序盤は低く、中盤以降は高く設定
        float riskDiff = Mathf.Abs(risk - targetRisk);

        // 1. リスク維持報酬（序盤は 0.15 付近を維持できれば加点）
        if (riskDiff < 0.1f && p.HitCountInPeriod == 0)
        {
            AddReward(0.1f);
        }

        // 2. 序盤の「技量測定」ボーナス
        // 少ない弾数（低リスク）なのに、プレイヤーがしっかり動いている＝技量が高いと判定
        if (timeRatio > 0.8f && risk > 0.1f && skillLevel > 3.0f)
        {
            AddReward(0.05f); // 測定成功ボーナス
        }
        // ★ 修正：プレイヤーが避けていようがいまいが、
        // 画面内に高い密度（Risk）を作っていること自体を評価する
        if (risk > 0.3f)
        {
            AddReward(0.01f * risk); // 密度が高いほどボスに加点
        }
        // 3. 被弾ペナルティ（序盤の「殺しすぎ」を重罪にする）
        if (p.HitCountInPeriod > 0)
        {
            // 序盤の被弾は、ボスの「加減」ができていない証拠としてペナルティを倍増
            float severity = (timeRatio > 0.8f) ? 2.0f : 1.0f;
            AddReward(-severity * p.HitCountInPeriod);
            p.ResetHitCount();
            return;
        }
    }
    private void ClampPosition()
    {
        transform.localPosition = new Vector3(
            Mathf.Clamp(transform.localPosition.x, minX, maxX),
            Mathf.Clamp(transform.localPosition.y, minY, maxY), 0);
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Enemy_Bullet")) // 自機の弾
        {
            var eb = col.GetComponent<EnemyBullet>();
            if (eb != null)
            {
                if (playerTransform != null)
                {
                    var p = playerTransform.GetComponent<PlayerAgent>();
                    p.RegisterHit(); // 攻撃成功を記録
                    p.AddReward(0.05f);
                }
                AddReward(0.01f);
                eb.Deactivate();
            }
        }
    }
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var CA = actionsOut.ContinuousActions;
        var DA = actionsOut.DiscreteActions;
        CA[0] = Input.GetAxisRaw("Horizontal");
        CA[1] = Input.GetAxisRaw("Vertical");
        CA[2] = 0.5f; CA[3] = 0f; CA[4] = 0.5f;
        DA[0] = Input.GetKey(KeyCode.Space) ? 1 : 0;
        DA[1] = 1; DA[2] = 2;
    }
}