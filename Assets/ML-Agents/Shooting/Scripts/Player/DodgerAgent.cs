using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using System.Linq;

public class DodgerAgent : Agent
{
    [Header("Movement")]
    public float speed = 5f;
    private Rigidbody2D rb;

    [Header("Observation")]
    public int observeBulletCount = 3; // keep = 3

    [Header("Prediction / Reward")]
    public float predictionTime = 0.5f; // seconds into future to predict bullet position
    public float safeDistance = 1.0f;   // 理想の最小距離（これより近いと罰則）
    public float futureRewardScale = 0.001f; // 未来距離に対するスケール
    public float survivalReward = 0.005f;    // 生存報酬 / step
    public float hitPenalty = -1.0f;         // 被弾ペナルティ

    // Derived: observation size = 2 (player pos) + observeBulletCount * (2+2+2) = 2 + 6*observeBulletCount
    // For observeBulletCount=3 => 2 + 18 = 20

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody2D>();
        // optional initial pos
        transform.localPosition = Vector3.zero;
    }

    public override void OnEpisodeBegin()
    {
        // Reset player
        transform.localPosition = Vector3.zero + new Vector3(-5,0,0);
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // Destroy existing bullets
        foreach (var b in GameObject.FindGameObjectsWithTag("Enemy_Bullet"))
            Destroy(b);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 1) Player position (local)
        sensor.AddObservation(transform.localPosition); // Vector3 -> 3, but ML-Agents expects size consistent.
        // NOTE: We want 2 dimensions (x,y). If using Vector3, it adds 3. To ensure exact size, use Vector2:
        // But VectorSensor.AddObservation accepts Vector3 — to keep observation count exact, add Vector2 components manually:
        // To be safe, replace above with:
        // sensor.AddObservation(new Vector2(transform.localPosition.x, transform.localPosition.y));
        // We'll do that below to ensure fixed dimensionality.

        // Clear and re-add correctly as Vector2 to enforce count:
        sensor.Reset(); // reset is available on some sensors; if not available in your MLAgents version, ensure you count correctly.
        // Instead, to be robust: write manual additions:

        // Rebuild observations deterministically:
        // Player pos (2)
        sensor.AddObservation(transform.localPosition.x);
        sensor.AddObservation(transform.localPosition.y);

        // Prepare fixed-length arrays
        Vector2[] relPos = new Vector2[observeBulletCount];
        Vector2[] vel = new Vector2[observeBulletCount];
        Vector2[] accel = new Vector2[observeBulletCount];

        var bullets = GameObject.FindGameObjectsWithTag("Enemy_Bullet")
            .OrderBy(b => Vector2.Distance(b.transform.position, transform.position))
            .Take(observeBulletCount)
            .ToArray();

        for (int i = 0; i < bullets.Length; i++)
        {
            var b = bullets[i];
            relPos[i] = (Vector2)b.transform.localPosition - (Vector2)transform.localPosition;

            var eb = b.GetComponent<EnemyBullet>();
            if (eb != null)
            {
                Vector3 v3 = eb.Velocity;
                Vector3 a3 = eb.Acceleration;
                vel[i] = new Vector2(v3.x, v3.y);
                accel[i] = new Vector2(a3.x, a3.y);
            }
            else
            {
                // Fallback: try Rigidbody2D
                var br = b.GetComponent<Rigidbody2D>();
                if (br != null)
                {
                    vel[i] = br.linearVelocity;
                    accel[i] = Vector2.zero;
                }
                else
                {
                    vel[i] = Vector2.zero;
                    accel[i] = Vector2.zero;
                }
            }
        }

        // Zero padding for missing bullets is already in initialized arrays (Vector2 default zero)

        // Add observations in fixed order: [relPos0, vel0, accel0, relPos1, vel1, accel1, ...]
        for (int i = 0; i < observeBulletCount; i++)
        {
            sensor.AddObservation(relPos[i]); // 2
            sensor.AddObservation(vel[i]);    // 2
            sensor.AddObservation(accel[i]);  // 2
        }
    }

    public float minX;
    public float maxX;
    public float minY;
    public float maxY;

    public float normalSpeed = 5f;
    public float slowSpeed = 2f;   // 低速時の速度

    public override void OnActionReceived(ActionBuffers actions)
    {
        var c = actions.ContinuousActions;

        float moveX = Mathf.Clamp(c[0], -1f, 1f);
        float moveY = Mathf.Clamp(c[1], -1f, 1f);

        // --- 低速フラグ（AI用） ---
        float slowFlag = c.Length >= 3 ? Mathf.Clamp01(c[2]) : 0f;

        // --- プレイヤー操作時（Heuristic）で Shift を判定 ---
        bool shift = Input.GetKey(KeyCode.LeftShift);
        bool isSlow = (slowFlag > 0.5f) || shift;

        float currentSpeed = isSlow ? slowSpeed : normalSpeed;

        // 🔻 ここが新しい：斜め移動時の √2 補正（正規化）
        Vector2 moveInput = new Vector2(moveX, moveY);

        if (moveInput.sqrMagnitude > 1f)
            moveInput.Normalize(); // √2問題を完全に解決

        Vector2 move = moveInput * currentSpeed;

        // ---- 境界チェックは現状のまま ----
        Vector3 currentPos = transform.localPosition;

        if ((currentPos.x <= minX && move.x < 0) || (currentPos.x >= maxX && move.x > 0))
            move.x = 0;

        if ((currentPos.y <= minY && move.y < 0) || (currentPos.y >= maxY && move.y > 0))
            move.y = 0;

        if (rb != null)
            rb.linearVelocity = move;

        Vector3 pos = transform.localPosition;
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        transform.localPosition = pos;

        // 生存報酬
        AddReward(survivalReward);

        float centerBonus = 1f - (Mathf.Abs(transform.localPosition.y) / maxY);
        AddReward(centerBonus * 0.02f);


        // 未来位置予測の reward shaping（ここは元のまま）
        ApplyFuturePredictionReward();
    }

    // DodgerAgent クラス内に追加してください
    private void ApplyFuturePredictionReward()
    {
        float minFutureDist = float.MaxValue;
        var bullets = GameObject.FindGameObjectsWithTag("Enemy_Bullet");

        // 予測に使う時間（秒）はクラスの predictionTime を使用
        float t = Mathf.Max(0f, predictionTime);

        foreach (var b in bullets)
        {
            if (b == null) continue;

            Vector2 bPos = b.transform.position;
            Vector2 bVel = Vector2.zero;
            Vector2 bAcc = Vector2.zero;

            var eb = b.GetComponent<EnemyBullet>();
            if (eb != null)
            {
                // EnemyBullet 側で public プロパティ Velocity, Acceleration を用意していること
                bVel = new Vector2(eb.Velocity.x, eb.Velocity.y);
                bAcc = new Vector2(eb.Acceleration.x, eb.Acceleration.y);
            }
            else
            {
                var br = b.GetComponent<Rigidbody2D>();
                if (br != null) bVel = br.linearVelocity;
            }

            // 予測位置： p + v*t + 0.5*a*t^2
            Vector2 futurePos = bPos + bVel * t + 0.5f * bAcc * t * t;
            float dist = Vector2.Distance(futurePos, (Vector2)transform.position);

            if (dist < minFutureDist) minFutureDist = dist;
        }

        if (minFutureDist == float.MaxValue)
        {
            // 弾が存在しなければ追加報酬はなし（あるいは小ボーナスにしてもよい）
            return;
        }

        // 近すぎたらペナルティ、十分離れていれば小さなボーナス
        if (minFutureDist < safeDistance)
        {
            // safeDistance に達していない分だけペナルティ。係数は tunable。
            float penalty = (safeDistance - minFutureDist) * futureRewardScale;
            AddReward(-Mathf.Abs(penalty)); // マイナス固定
        }
        else
        {
            // 安全圏に入っていれば少しボーナス
            float bonus = (minFutureDist - safeDistance) * futureRewardScale;
            // 緩やかな上限を設定（過度なボーナスを避ける）
            bonus = Mathf.Min(bonus, 0.01f);
            AddReward(bonus);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var c = actionsOut.ContinuousActions;

        c[0] = Input.GetAxisRaw("Horizontal");
        c[1] = Input.GetAxisRaw("Vertical");

        // Shift 押してたら低速モード 1.0
        c[2] = Input.GetKey(KeyCode.LeftShift) ? 1f : 0f;
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Enemy_Bullet"))
        {
            Debug.Log("Agent HIT");
            AddReward(hitPenalty);
            EndEpisode();
        }
    }
}
