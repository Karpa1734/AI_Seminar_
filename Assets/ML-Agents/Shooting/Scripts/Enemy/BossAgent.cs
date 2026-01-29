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

        // A. 生存スコア：8秒間耐えれば満点（0.5）
        float survival = Mathf.Min(p.SurvivalTimeSinceLastHit / 8f, 1f) * 0.5f;

        // B. 攻撃スコア：わずか 3発 当てるだけで満点（0.3）
        float offense = Mathf.Clamp01(p.HitsLandedOnBoss / 3f) * 0.3f;

        // C. 回避スコア：Risk 0.3（中程度の密度）で満点（0.3）
        float evasion = Mathf.Min(p.GetNormalizedRisk() / 0.3f, 1f) * 0.3f;

        // 最大合計 1.1 になるので、0.7～0.8 に到達するのが非常に簡単になります
        float currentPerformance = survival + offense + evasion;
        GlobalSkillManager.Instance.UpdateSkill(currentPerformance);
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

    public override void OnActionReceived(ActionBuffers actions)
    {
        var CA = actions.ContinuousActions;
        var DA = actions.DiscreteActions;

        // --- 1. 移動と座標計算 ---
        rb.linearVelocity = new Vector2(CA[0], CA[1]) * moveSpeed;
        float spawnRadius = (CA[2] + 1f) * 1.5f;
        float spawnAngleOffset = CA[3] * 180f;
        Vector3 spawnPos = transform.position + Quaternion.Euler(0, 0, spawnAngleOffset) * Vector3.right * spawnRadius;

        // --- 2. プレイヤーの技量を取得し、S字カーブを適用 ---
        float rawSkill = (GlobalSkillManager.Instance != null) ? GlobalSkillManager.Instance.currentSkillLevel : 0.5f;
        float playerSkill;

        if (rawSkill < 0.3f)
        {
            playerSkill = Mathf.Lerp(0f, 0.05f, rawSkill / 0.3f);
        }
        else if (rawSkill < 0.7f)
        {
            float t = (rawSkill - 0.3f) / 0.4f;
            // 30%～70%の間で 0.05 から 0.85 まで上昇させる（0.95だと高すぎたため抑制）
            playerSkill = Mathf.Lerp(0.05f, 0.85f, Mathf.SmoothStep(0f, 1f, t));
        }
        else
        {
            // 70% ～ 100% の間は 0.85 から 1.0 へ緩やかに
            playerSkill = Mathf.Lerp(0.85f, 1.0f, (rawSkill - 0.7f) / 0.3f);
        }

        // --- 3. 弾幕パラメータの決定（速度の抑制） ---
        // 倍率の上限を 1.1f -> 1.05f に抑え、速すぎを防止
        DebugSpeedMultiplier = Mathf.Lerp(0.7f, 1.0f, playerSkill);
        // 速度係数を 1.5f に下げ、ベース速度をマイルドに
        float baseSpeed = ((CA[4] + 1f) * 1.2f + 1.5f) * DebugSpeedMultiplier;
        float speedGap = (CA[7] + 1f) * 1.0f; // 層ごとの速度差も 1.5 -> 1.0 へ

        // AIによる「歪み」の振幅も抑制
        float sAmp = (CA.Length > 8) ? (CA[8] + 1f) * 1.2f : 0f;
        float sFreq = (CA.Length > 10) ? (CA[10] + 1f) * 5.0f : 0.5f;
        float aAmp = (CA.Length > 9) ? (CA[9] + 1f) * 4.0f : 0f;
        float aFreq = (CA.Length > 11) ? (CA[11] + 1f) * 5.0f : 0.5f;

        // --- 4. 制限の調整（密度の抑制） ---
        int patternMode = DA[1];
        float spread = (patternMode == 2) ? 360f : (CA[5] + 1f) * 60f;

        // ★ 密度の基準を spread/5 から spread/6 に抑える (全方位最大 60発)
        float absMaxWay = spread / 12.0f;
        int minWay, maxWay;
        if (patternMode == 2)
        {
            minWay = Mathf.RoundToInt(Mathf.Lerp(6f, 16f, playerSkill)); // 36 -> 32
            maxWay = Mathf.RoundToInt(Mathf.Lerp(12f, absMaxWay, playerSkill));
        }
        else
        {
            minWay = Mathf.RoundToInt(Mathf.Lerp(3f, 5f, playerSkill)); // 12 -> 10
            maxWay = Mathf.RoundToInt(Mathf.Lerp(5f, absMaxWay, playerSkill));
        }
        DebugMaxWayLimit = maxWay;
        int wayCount = Mathf.Clamp(DA[2] + minWay, minWay, maxWay);

        // ★ 最大レイヤー数を 3層 に制限 (4層は密度が高すぎたため)
        int layerCount = (playerSkill > 0.8f) ? 3 : (playerSkill > 0.5f ? 2 : 1);

        // --- 5. 射撃コストと頻度（インターバル解消の核心） ---
        float costMultiplier = Mathf.Lerp(3.0f, 0.1f, playerSkill);
        float totalCost = energyCostPerBullet * wayCount * layerCount * costMultiplier;
        float cooldownBias = Mathf.Lerp(3.0f, 0.1f, playerSkill);

        // ★ 修正：wayCount による加算を平方根(Mathf.Sqrt)にし、大量発射後のフリーズを防止
        float wayCooldownPenalty = Mathf.Sqrt(wayCount) * 0.04f;

        // --- 6. 射撃実行 ---
        if (DA[0] == 1 && fireCooldown <= 0 && currentEnergy >= totalCost)
        {
            Vector2 dirToPlayer = (playerTransform.position - spawnPos).normalized;
            float baseAngle = Mathf.Atan2(dirToPlayer.y, dirToPlayer.x) * Mathf.Rad2Deg;
            float startAngle = (wayCount > 1) ? baseAngle - (spread / 2f) : baseAngle;
            float step = (wayCount > 1 && spread < 360f) ? spread / (wayCount - 1) : 360f / wayCount;

            for (int l = 0; l < layerCount; l++)
            {
                float layerBaseSpeed = Mathf.Max(2.0f, baseSpeed - (l * speedGap));
                for (int i = 0; i < wayCount; i++)
                {
                    float speedOffset = Mathf.Sin(i * sFreq + l) * sAmp;
                    float angleOffset = Mathf.Cos(i * aFreq + l) * aAmp;
                    spawner.FireRaw(spawnPos, Mathf.Max(2.0f, layerBaseSpeed + speedOffset), startAngle + (step * i) + angleOffset, CA[6] * 30f);
                }
            }

            float moveReward = (patternMode == 2) ? 2.0f : 0.05f;
            currentEnergy -= totalCost / 5;
            // ★ クールダウンの計算式を更新
            fireCooldown = minFireInterval * (cooldownBias + wayCooldownPenalty);
            AddReward(moveReward);
        }
        ClampPosition();
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