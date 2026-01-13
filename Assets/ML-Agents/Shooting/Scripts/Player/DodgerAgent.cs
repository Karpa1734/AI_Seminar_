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
        UpdateHPUI(true); // ★即時反映

        // リセット時は両方のバーを即座に満タンにする
        if (hpSlider != null) hpSlider.value = 1f;
        if (orangeSlider != null) orangeSlider.value = 1f;

        if (spawner == null) spawner = GetComponent<BulletSpawner>();

        float offset = 6.0f;
        bool isP1 = (spawner != null && spawner.myTeam == BulletSpawner.TeamSide.Player1);
        float startX = isP1 ? -offset : offset;

        transform.localPosition = new Vector3(startX, 0f, 0f);
        if (rb != null) rb.linearVelocity = Vector2.zero;

        if (visualSprite != null) visualSprite.flipY = !isP1;

        stunTimer = 0f;
        invincibilityTimer = 0f;

        if (BulletPool.Instance != null) BulletPool.Instance.ReturnAllBullets();
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
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (StepCount >= MaxStep - 1 && MaxStep > 0)
        {
            SetReward(clearBonus);
            EndEpisode();
            return;
        }

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
            if (visualSprite != null) visualSprite.color = Color.white;
        }

        if (canMove)
        {
            var CA = actions.ContinuousActions;
            float moveX = Mathf.Clamp(CA[0], -1f, 1f);
            float moveY = Mathf.Clamp(CA[1], -1f, 1f);
            bool isSlowInput = CA.Length >= 3 && CA[2] > 0.5f;

            float firingMultiplier = (spawner != null) ? spawner.GetCurrentSpeedMultiplier() : 1f;
            float speed = isSlowInput ? slowSpeed : normalSpeed;
            speed *= firingMultiplier;

            Vector2 moveInput = new Vector2(moveX, moveY);
            if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();
            if (rb != null) rb.linearVelocity = moveInput * speed;

            transform.localPosition = new Vector3(
                Mathf.Clamp(transform.localPosition.x, minX, maxX),
                Mathf.Clamp(transform.localPosition.y, minY, maxY), 0);

            if (spawner != null) spawner.UpdateInputState(actions.DiscreteActions[0]);
        }
        AddReward(survivalReward);
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
                UpdateHPUI(false); // ★滑らかに減少

                AddReward(hitPenalty);
                if (opponentAgent != null) opponentAgent.AddReward(hitOpponentReward);

                eb.Deactivate();

                if (currentHealth <= 0)
                {
                    Respawn();
                }
                else
                {
                    stunTimer = stunDuration;
                    if (rb != null) rb.linearVelocity = Vector2.zero;
                }
            }
        }
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