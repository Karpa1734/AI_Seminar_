using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using System.Linq;

public class DodgerAgent : Agent
{
    [Header("Target Settings")]
    public Transform enemyTransform;
    private DodgerAgent opponentAgent;

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
    public int maxHealth = 3;
    private int currentHealth;

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

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody2D>();
        spawner = GetComponent<BulletSpawner>();
        if (visualSprite == null) visualSprite = GetComponentInChildren<SpriteRenderer>();
        if (enemyTransform != null) opponentAgent = enemyTransform.GetComponent<DodgerAgent>();

        if (spawner != null && spawner.characterProfile != null)
        {
            normalSpeed = spawner.characterProfile.normalSpeed;
            slowSpeed = spawner.characterProfile.slowSpeed;
        }
    }

    public override void OnEpisodeBegin()
    {
        currentHealth = maxHealth;
        if (spawner == null) spawner = GetComponent<BulletSpawner>();

        float offset = 6.0f;
        bool isP1 = (spawner != null && spawner.myTeam == BulletSpawner.TeamSide.Player1);
        float startX = isP1 ? -offset : offset;

        transform.localPosition = new Vector3(startX, 0f, 0f);
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // 初期向き設定
        if (visualSprite != null) visualSprite.flipY = !isP1;

        stunTimer = 0f;
        invincibilityTimer = 0f;
        if (BulletPool.Instance != null) BulletPool.Instance.ReturnAllBullets();
    }

    void Update()
    {
        // 常に相手を向く (flipY)
        if (visualSprite != null && enemyTransform != null)
        {
            visualSprite.flipY = (enemyTransform.position.x < transform.position.x);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Observation Size: 72
        sensor.AddObservation(transform.localPosition.x / maxX);
        sensor.AddObservation(transform.localPosition.y / maxY);
        sensor.AddObservation(stunTimer > 0);
        sensor.AddObservation(invincibilityTimer > 0);
        sensor.AddObservation((float)currentHealth / maxHealth); // 5

        for (int i = 0; i < 4; i++) sensor.AddObservation(spawner.GetRecastProgress(i)); // 4

        if (enemyTransform != null)
        {
            sensor.AddObservation(enemyTransform.localPosition.x / maxX);
            sensor.AddObservation((Vector2)(enemyTransform.position - transform.position) / maxDetectionRadius); // 3
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

        for (int i = 0; i < observeBulletCount - bullets.Count; i++) { sensor.AddObservation(Vector2.zero); sensor.AddObservation(Vector2.zero); sensor.AddObservation(Vector2.zero); }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (StepCount >= MaxStep - 1 && MaxStep > 0) { SetReward(clearBonus); EndEpisode(); return; }

        bool canMove = true;
        if (stunTimer > 0)
        {
            stunTimer -= Time.deltaTime; canMove = false;
            if (rb != null) rb.linearVelocity = Vector2.zero;
            if (visualSprite != null) visualSprite.color = Color.red;
            if (stunTimer <= 0) invincibilityTimer = invincibilityDuration;
        }
        else if (invincibilityTimer > 0)
        {
            invincibilityTimer -= Time.deltaTime;
            if (visualSprite != null) { Color c = Color.cyan; c.a = 0.5f; visualSprite.color = c; }
        }
        else { if (visualSprite != null) visualSprite.color = Color.white; }

        if (canMove)
        {
            var CA = actions.ContinuousActions;
            float moveX = Mathf.Clamp(CA[0], -1f, 1f);
            float moveY = Mathf.Clamp(CA[1], -1f, 1f);
            bool isSlowInput = CA.Length >= 3 && CA[2] > 0.5f;

            // ★追加：発射中減衰率の適用
            float firingMultiplier = (spawner != null) ? spawner.GetCurrentSpeedMultiplier() : 1f;

            float speed = isSlowInput ? slowSpeed : normalSpeed;
            speed *= firingMultiplier; // 減衰を適用

            Vector2 moveInput = new Vector2(moveX, moveY);
            if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();
            if (rb != null) rb.linearVelocity = moveInput * speed;

            transform.localPosition = new Vector3(Mathf.Clamp(transform.localPosition.x, minX, maxX), Mathf.Clamp(transform.localPosition.y, minY, maxY), 0);

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
            CA[0] = h; CA[1] = v; CA[2] = Input.GetKey(KeyCode.LeftShift) ? 1f : 0f;
            if (Input.GetKey(KeyCode.Z)) DA[0] = 1; else if (Input.GetKey(KeyCode.X)) DA[0] = 2;
        }
        else
        {
            float h = (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            float v = (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            CA[0] = h; CA[1] = v; CA[2] = Input.GetKey(KeyCode.RightShift) ? 1f : 0f;
            if (Input.GetKey(KeyCode.I)) DA[0] = 1; else if (Input.GetKey(KeyCode.O)) DA[0] = 2;
        }
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (stunTimer > 0 || invincibilityTimer > 0) return;
        if (col.CompareTag("Enemy_Bullet"))
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
            AddReward(hitPenalty);
            if (opponentAgent != null) opponentAgent.AddReward(hitOpponentReward);
            currentHealth--;
            if (currentHealth <= 0) { SetReward(gameOverPenalty); EndEpisode(); }
            else stunTimer = stunDuration;
        }
    }
}