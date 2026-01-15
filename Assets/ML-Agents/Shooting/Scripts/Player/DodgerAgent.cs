using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.UI;
using System.Linq;

public class DodgerAgent : Agent
{
    [Header("Target Settings")]
    public Transform enemyTransform;
    private DodgerAgent opponentAgent;

    [Header("UI References")]
    [SerializeField] private Slider hpSlider;
    [SerializeField] private Slider orangeSlider; // ★追加：背後に置くオレンジ色のスライダー
    [SerializeField] private Image hpFillImage;
    [SerializeField] private float barSmoothSpeed = 8f;   // メインHPの速度（少し速め）
    [SerializeField] private float orangeSmoothSpeed = 1f; // ★追加：オレンジゲージの速度（ゆっくり）
    [SerializeField] private float orangeDelayTime = 0.6f;  // ★追加：減少が始まるまでの待機時間
    [SerializeField] private Color normalHpColor = Color.green;
    [SerializeField] private Color dangerHpColor = Color.red;

    [Header("Visual Settings")]
    public SpriteRenderer visualSprite;

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

    [Header("Life Settings")]
    public float maxHealth = 100f;
    private float currentHealth;
    private float targetHpRatio = 1f; // ★目標とするHP割合(0.0~1.0)

    [Header("Reward Weights")]
    public float survivalReward = 0.002f;
    public float hitPenalty = -0.5f;
    public float hitOpponentReward = 0.8f;
    public float gameOverPenalty = -1.0f;
    public float clearBonus = 2.0f;

    [Header("Stage Bounds")]
    public float minX = -9f;
    public float maxX = 9f;
    public float minY = -5f;
    public float maxY = 5f;

    [Header("Random Fire Settings")]
    [SerializeField] private bool useRandomFireInTraining = false; // 学習中にランダム射撃を行うか
    private float randomFireTimer = 0f;

    private static bool isSideSwapped = false;
    private static int lastUpdateFrame = -1; // 同じフレーム内での重複判定を防止

    [Header("Team Appearance")]
    [SerializeField] private Color p1Color = new Color(0.2f, 0.5f, 1f); // 青系
    [SerializeField] private Color p2Color = new Color(1f, 0.3f, 0.3f); // 赤系
    private Color normalColor; // 自分のチームの平常色

    private BulletSpawner spawner; 
    private float orangeDelayTimer = 0f; // ★追加：現在の待機カウント

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody2D>();
        spawner = GetComponent<BulletSpawner>();
        if (visualSprite == null) visualSprite = GetComponentInChildren<SpriteRenderer>();
        if (enemyTransform != null) opponentAgent = enemyTransform.GetComponent<DodgerAgent>();

        if (spawner != null && spawner.characterProfile != null)
        {
            CharacterData profile = spawner.characterProfile;
            normalSpeed = profile.normalSpeed;
            slowSpeed = profile.slowSpeed;
            maxHealth = profile.maxHealth;
        }
    }

    public override void OnEpisodeBegin()
    {
        currentHealth = maxHealth;
        UpdateHPUI(true);

        if (spawner == null) spawner = GetComponent<BulletSpawner>();
        bool isP1 = (spawner != null && spawner.myTeam == BulletSpawner.TeamSide.Player1);

        // ★ チームカラーの決定
        normalColor = isP1 ? p1Color : p2Color;
        if (visualSprite != null) visualSprite.color = normalColor;

        // ★ 同期処理：同じフレーム内（エピソード開始時）に一度だけランダム判定を行う
        if (Time.frameCount != lastUpdateFrame)
        {
            isSideSwapped = (Random.value > 0.5f);
            lastUpdateFrame = Time.frameCount;

            // P1側が代表して弾をクリアする
            if (BulletPool.Instance != null) BulletPool.Instance.ReturnAllBullets();
        }

        // ★ シャッフル座標の計算
        float offset = 6.0f;
        float startX;
        if (isP1) startX = isSideSwapped ? offset : -offset;
        else startX = isSideSwapped ? -offset : offset;

        transform.localPosition = new Vector3(startX, 0f, 0f);
        if (rb != null) rb.linearVelocity = Vector2.zero;

        if (visualSprite != null) visualSprite.flipY = !isP1; //

        stunTimer = 0f;
        invincibilityTimer = 0f;
    }

    void Update()
    {
        if (visualSprite != null && enemyTransform != null)
        {
            visualSprite.flipY = (enemyTransform.position.x < transform.position.x);
        }

        if (hpSlider != null && orangeSlider != null)
        {
            // 1. メインHPは常に滑らかに追従
            hpSlider.value = Mathf.Lerp(hpSlider.value, targetHpRatio, Time.deltaTime * barSmoothSpeed);

            // 2. オレンジゲージの待機タイマーを減らす
            if (orangeDelayTimer > 0)
            {
                orangeDelayTimer -= Time.deltaTime;
            }
            else
            {
                // タイマーが0の時だけ、オレンジゲージを追従させる
                orangeSlider.value = Mathf.Lerp(orangeSlider.value, targetHpRatio, Time.deltaTime * orangeSmoothSpeed);
            }

            // 3. 3割以下なら赤
            if (hpFillImage != null)
            {
                hpFillImage.color = (hpSlider.value <= 0.3f) ? dangerHpColor : normalHpColor;
            }
        }

    }

    // ★新規追加：回復メソッド
    public void Heal(float amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth); // 最大値を超えない
        UpdateHPUI(false);
        Debug.Log($"{gameObject.name} が回復！ 現在のHP: {currentHealth}");
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.localPosition.x / maxX);
        sensor.AddObservation(transform.localPosition.y / maxY);
        sensor.AddObservation(stunTimer > 0);
        sensor.AddObservation(invincibilityTimer > 0);
        sensor.AddObservation(currentHealth / maxHealth);

        for (int i = 0; i < 4; i++) sensor.AddObservation(spawner.GetRecastProgress(i));

        if (enemyTransform != null)
        {
            sensor.AddObservation(enemyTransform.localPosition.x / maxX);
            sensor.AddObservation((Vector2)(enemyTransform.position - transform.position) / maxDetectionRadius);
        }
        else { sensor.AddObservation(0f); sensor.AddObservation(Vector2.zero); }

        string targetLayer = (spawner.myTeam == BulletSpawner.TeamSide.Player1) ? "Player2_Bullet" : "Player1_Bullet";
        var bullets = GameObject.FindGameObjectsWithTag("Enemy_Bullet")
            .Where(b => b.layer == LayerMask.NameToLayer(targetLayer))
            .OrderBy(b => Vector2.Distance(b.transform.position, transform.position))
            .Take(observeBulletCount).ToList();

        foreach (var b in bullets)
        {
            sensor.AddObservation((Vector2)(b.transform.position - transform.position) / maxDetectionRadius);
            var eb = b.GetComponent<EnemyBullet>();
            sensor.AddObservation(eb != null ? (Vector2)eb.Velocity / 10f : Vector2.zero);
            sensor.AddObservation(eb != null ? (Vector2)eb.Acceleration / 5f : Vector2.zero);
        }

        for (int i = 0; i < observeBulletCount - bullets.Count; i++)
        {
            sensor.AddObservation(Vector2.zero);
            sensor.AddObservation(Vector2.zero);
            sensor.AddObservation(Vector2.zero);
        }

        // 各スキルのチャージ状況（引き絞り具合）を観測に追加 (+4個)
        for (int i = 0; i < 4; i++) sensor.AddObservation(spawner.GetFanAngleProgress(i));
    }

    public override void OnActionReceived(ActionBuffers actions)
    {


        // 1. エピソード終了判定
        if (StepCount >= MaxStep - 1 && MaxStep > 0)
        {
            SetReward(clearBonus);
            EndEpisode();
            return;
        }

        // 2. 状態異常（スタン・無敵）の処理
        bool canMove = true;
        if (stunTimer > 0)
        {
            stunTimer -= Time.deltaTime;
            canMove = false;
            if (rb != null) rb.linearVelocity = Vector2.zero;
            if (visualSprite != null) visualSprite.color = Color.red;
            if (stunTimer <= 0) invincibilityTimer = invincibilityDuration;
        }
        else if (invincibilityTimer > 0)
        {
            invincibilityTimer -= Time.deltaTime;
            if (visualSprite != null) { Color c = Color.cyan; c.a = 0.5f; visualSprite.color = c; }
        }
        else
        {
            if (visualSprite != null) visualSprite.color = normalColor;
        }

        // 3. 移動・アクションのメイン処理
        if (canMove)
        {
            // --- A. 移動ロジック ---
            var CA = actions.ContinuousActions;
            float moveX = Mathf.Clamp(CA[0], -1f, 1f);
            float moveY = Mathf.Clamp(CA[1], -1f, 1f);
            bool isSlowInput = CA.Length >= 3 && CA[2] > 0.5f;

            float firingMultiplier = (spawner != null) ? spawner.GetCurrentSpeedMultiplier() : 1f;
            float speed = (isSlowInput ? slowSpeed : normalSpeed) * firingMultiplier;

            Vector2 moveInput = new Vector2(moveX, moveY);
            if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();
            if (rb != null) rb.linearVelocity = moveInput * speed;

            transform.localPosition = new Vector3(
                Mathf.Clamp(transform.localPosition.x, minX, maxX),
                Mathf.Clamp(transform.localPosition.y, minY, maxY), 0);

            // 1. AIの出力を取得
            int actionToExecute = actions.DiscreteActions[0];

            // 2. 学習モード限定の「積極的射撃」思考の注入
            if (Academy.Instance.IsCommunicatorOn)
            {
                // リキャストが完了している（＝撃てる）スキルがあるか確認
                bool anySkillReady = false;
                for (int i = 0; i < 4; i++)
                {
                    if (spawner.GetRemainingRecastTime(i) <= 0) anySkillReady = true;
                }

                if (actionToExecute > 0)
                {
                    // --- スキルを使った場合 ---
                    int skillIdx = actionToExecute - 1;
                    if (spawner.GetRemainingRecastTime(skillIdx) <= 0)
                    {
                        // リキャスト完了時に撃ったら、生存報酬(0.002)の5倍のボーナス
                        // これにより「とりあえず撃つ」ことが正解だと教え込みます
                        AddReward(0.01f);
                    }
                    else
                    {
                        // リキャスト中なのにボタンを押す（無駄撃ち）には小さな罰
                        AddReward(-0.001f);
                    }
                }
                else if (anySkillReady)
                {
                    // --- 撃てるのに撃たなかった場合（Action 0） ---
                    // 「撃てるのにサボっている」として、生存報酬を打ち消すペナルティ
                    AddReward(-0.003f);
                }
            }

            if (Academy.Instance.IsCommunicatorOn)
            {
                // 学習中：ランダム射撃設定がONなら上書きして多様な状況を経験させる
                if (useRandomFireInTraining)
                {
                    randomFireTimer -= Time.deltaTime;
                    if (randomFireTimer <= 0)
                    {
                        actionToExecute = Random.Range(0, 5);
                        randomFireTimer = 0.2f;
                    }
                    else
                    {
                        // 0.2秒間は入力を維持し、チャージを可能にする
                        // actionToExecute は更新せず、前フレームの値を維持することを想定
                    }
                }

                // 射撃すること自体への微小報酬（攻撃をためらわないようにする）
                if (actionToExecute > 0) AddReward(0.0001f);
            }

            // --- C. 命令の実行（★ここを1回だけにする） ---
            if (spawner != null)
            {
                spawner.UpdateInputState(actionToExecute);

                // 推論中（テスト外）：AIが何を選んでいるか確認
                if (actionToExecute != 0)
                {
                    Debug.Log($"AI Decision - Action: {actionToExecute}");
                }
            }

            // --- D. 引き絞り（チャージ）報酬ロジック ---
            for (int i = 0; i < 4; i++)
            {
                if (spawner != null && spawner.IsCharging(i))
                {
                    float progress = spawner.GetFanAngleProgress(i);
                    float exponentialBonus = Mathf.Pow(progress, 2); // 引き絞るほど価値が上がる

                    if (enemyTransform != null)
                    {
                        Vector2 toEnemy = (enemyTransform.position - transform.position).normalized;
                        float angleToEnemy = Mathf.Atan2(toEnemy.y, toEnemy.x) * Mathf.Rad2Deg;
                        float angleDiff = Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.z, angleToEnemy));

                        // 敵を射程内に捉えつつチャージしていれば加点
                        if (angleDiff < 30f)
                        {
                            AddReward(0.002f * exponentialBonus);
                        }
                    }

                    // 最大チャージ維持報酬
                    if (progress >= 1.0f) AddReward(0.01f);
                }
            }
        }

        // 4. 共通報酬処理
        ApplyPositionRewards();
        ApplyWallPenalty();
        AddReward(survivalReward);
    }
    void ApplyWallPenalty()
    {
        // 1. 壁からの距離のしきい値（例：1ユニット以内なら「壁際」とみなす）
        float wallThreshold = 1.0f;

        // 2. 現在の座標が各壁のしきい値内に入っているか判定
        bool nearLeft = transform.localPosition.x < minX + wallThreshold;
        bool nearRight = transform.localPosition.x > maxX - wallThreshold;
        bool nearBottom = transform.localPosition.y < minY + wallThreshold;
        bool nearTop = transform.localPosition.y > maxY - wallThreshold;

        // 3. いずれかの壁に近い場合に微小なペナルティを課す
        if (nearLeft || nearRight || nearBottom || nearTop)
        {
            // 生存報酬 (0.002f) よりも小さく設定するのがコツです
            AddReward(-0.001f);
        }
    }
    void ApplyPositionRewards()
    {
        if (enemyTransform == null || spawner == null) return;

        // 1. 敵との距離を計算
        float distance = Vector2.Distance(transform.position, enemyTransform.position);

        // キャラクターごとの理想的な範囲を取得
        float minDist = spawner.minOptimalDistance;
        float maxDist = spawner.maxOptimalDistance;

        // 近すぎることへの強い警告
        if (distance < minDist)
        {
            // 生存報酬 (0.002f) を打ち消す程度のペナルティを与える
            AddReward(-0.01f);
        }

        if (distance >= minDist && distance <= maxDist)
        {
            // 理想的な間合いにいる間、少しずつ加点
            AddReward(0.001f);
        }
        else
        {
            // 範囲外（近すぎ・遠すぎ）の場合は微小な減点
            AddReward(-0.0005f);
        }

        // 2. 画面端に寄っているかチェック（壁際ハメ防止）
        float edgeThreshold = 1.0f; // 壁から1ユニット以内を「端」とみなす
        bool isAtEdge =
            transform.localPosition.x < minX + edgeThreshold ||
            transform.localPosition.x > maxX - edgeThreshold ||
            transform.localPosition.y < minY + edgeThreshold ||
            transform.localPosition.y > maxY - edgeThreshold;

        if (isAtEdge)
        {
            // 画面端に滞在することへのペナルティ
            AddReward(-0.001f);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var CA = actionsOut.ContinuousActions;
        var DA = actionsOut.DiscreteActions;
        DA[0] = 0;

        if (spawner.myTeam == BulletSpawner.TeamSide.Player1)
        {
            float h = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            float v = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
            CA[0] = h; CA[1] = v;
            CA[2] = Input.GetKey(KeyCode.LeftShift) ? 1f : 0f;

            if (Input.GetKey(KeyCode.Z)) DA[0] = 1;
            else if (Input.GetKey(KeyCode.X)) DA[0] = 2;
            else if (Input.GetKey(KeyCode.C)) DA[0] = 3;
            else if (Input.GetKey(KeyCode.V)) DA[0] = 4;
        }
        else
        {
            float h = (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            float v = (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            CA[0] = h; CA[1] = v;
            CA[2] = Input.GetKey(KeyCode.RightShift) ? 1f : 0f;

            if (Input.GetKey(KeyCode.I)) DA[0] = 1;
            else if (Input.GetKey(KeyCode.O)) DA[0] = 2;
            else if (Input.GetKey(KeyCode.P)) DA[0] = 3;
        }
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (stunTimer > 0 || invincibilityTimer > 0) return;

        if (col.CompareTag("Enemy_Bullet"))
        {
            var eb = col.GetComponent<EnemyBullet>();
            if (eb != null)
            {
                currentHealth -= eb.DamageValue;
                UpdateHPUI(false);

                AddReward(hitPenalty);

                if (opponentAgent != null)
                {
                    // 1. 相手（弾を撃った側）と自分の距離を測る
                    float dist = Vector2.Distance(transform.position, opponentAgent.transform.position);
                    float multiplier = 1.0f;

                    // 2. スパナーに設定された「理想の最小距離」より近い場合は報酬を減衰させる
                    if (dist < opponentAgent.spawner.minOptimalDistance)
                    {
                        // 密着するほど 1.0 -> 0.1 へと報酬が下がるように計算
                        multiplier = Mathf.Lerp(0.1f, 1.0f, dist / opponentAgent.spawner.minOptimalDistance);
                    }

                    // 距離による倍率をかけて報酬を与える
                    opponentAgent.AddReward(hitOpponentReward * multiplier);
                }

                eb.Deactivate();

                if (currentHealth <= 0)
                {
                    // ★仕切り直しロジックに変更
                    HandleMatchOver();
                }
                else
                {
                    stunTimer = stunDuration;
                    if (rb != null) rb.linearVelocity = Vector2.zero;
                }
            }
        }
    }

    private void HandleMatchOver()
    {
        Debug.Log($"{gameObject.name} が撃破されました。試合をリセットします。");

        // 1. 敗者へのペナルティ
        AddReward(gameOverPenalty);

        // 2. 勝者（相手）への大きな報酬（トドメを刺したボーナス）
        if (opponentAgent != null)
        {
            // 単なるヒット報酬より大きな値を与えることで、AIが撃破を狙うようになります
            opponentAgent.AddReward(hitOpponentReward * 5f);

            // ★重要：両方のエピソードを終了させる
            // これにより、両者で同時に OnEpisodeBegin() が呼ばれ、体力が全快します
            opponentAgent.EndEpisode();
        }

        EndEpisode();
    }

    private void Respawn()
    {
        Debug.Log($"{gameObject.name} が倒されました。リスポーンします。");
        AddReward(-0.5f);

        currentHealth = maxHealth;
        UpdateHPUI(true); // ★リスポーン時は即座に満タン

        float offset = 6.0f;
        bool isP1 = (spawner != null && spawner.myTeam == BulletSpawner.TeamSide.Player1);
        float startX = isP1 ? -offset : offset;
        transform.localPosition = new Vector3(startX, 0f, 0f);
        if (rb != null) rb.linearVelocity = Vector2.zero;

        stunTimer = 0f;
        invincibilityTimer = invincibilityDuration;

        if (visualSprite != null)
        {
            Color c = Color.cyan;
            c.a = 0.5f;
            visualSprite.color = c;
        }
    }

    // UI更新メソッドを修正
    private void UpdateHPUI(bool immediate = false)
    {
        float oldRatio = targetHpRatio;
        targetHpRatio = currentHealth / maxHealth;

        // ★ダメージを受けた（体力が減った）場合のみ、オレンジゲージの待機タイマーをリセット
        if (targetHpRatio < oldRatio)
        {
            orangeDelayTimer = orangeDelayTime;
        }
        // ★回復した場合は、オレンジゲージも即座にメインHPへ追いつかせる（違和感防止）
        else if (targetHpRatio > oldRatio)
        {
            orangeDelayTimer = 0;
        }

        if (immediate)
        {
            if (hpSlider != null) hpSlider.value = targetHpRatio;
            if (orangeSlider != null) orangeSlider.value = targetHpRatio;
            orangeDelayTimer = 0;
        }
    }
}