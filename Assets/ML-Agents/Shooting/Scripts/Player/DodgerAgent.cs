using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using System.Linq;

public class DodgerAgent : Agent
{
    [Header("Movement")]
    public float normalSpeed = 5f;
    public float slowSpeed = 2f;
    private Rigidbody2D rb;

    [Header("Observation")]
    public int observeBulletCount = 3;

    [Header("Prediction / Reward")]
    public float predictionTime = 0.5f;
    public float safeDistance = 1.0f;
    public float futureRewardScale = 0.001f;
    public float survivalReward = 0.005f;
    public float hitPenalty = -1.0f;

    public float minX, maxX, minY, maxY;

    private Vector2 prevVel;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public override void OnEpisodeBegin()
    {
        transform.localPosition = new Vector3(-5, 0, 0);
        if (rb != null) rb.linearVelocity = Vector2.zero;

        foreach (var b in GameObject.FindGameObjectsWithTag("Enemy_Bullet"))
            Destroy(b);

        prevVel = Vector2.zero;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.localPosition.x);
        sensor.AddObservation(transform.localPosition.y);

        var bullets = GameObject.FindGameObjectsWithTag("Enemy_Bullet")
            .OrderBy(b => Vector2.Distance(b.transform.position, transform.position))
            .Take(observeBulletCount)
            .ToArray();

        foreach (var b in bullets)
        {
            Vector2 rel = (Vector2)b.transform.localPosition - (Vector2)transform.localPosition;

            var eb = b.GetComponent<EnemyBullet>();
            Vector2 v = eb != null ? (Vector2)eb.Velocity : Vector2.zero;
            Vector2 a = eb != null ? (Vector2)eb.Acceleration : Vector2.zero;

            sensor.AddObservation(rel);
            sensor.AddObservation(v);
            sensor.AddObservation(a);
        }

        // 認識する弾が少ない場合はゼロパディング
        for (int i = bullets.Length; i < observeBulletCount; i++)
        {
            sensor.AddObservation(Vector2.zero);
            sensor.AddObservation(Vector2.zero);
            sensor.AddObservation(Vector2.zero);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var c = actions.ContinuousActions;
        float moveX = Mathf.Clamp(c[0], -1f, 1f);
        float moveY = Mathf.Clamp(c[1], -1f, 1f);

        float slowFlag = c.Length >= 3 ? Mathf.Clamp01(c[2]) : 0f;

        bool isSlow = slowFlag > 0.5f;
        float currentSpeed = isSlow ? slowSpeed : normalSpeed;

        Vector2 moveInput = new Vector2(moveX, moveY);
        if (moveInput.sqrMagnitude > 1f)
            moveInput.Normalize();

        Vector2 move = moveInput * currentSpeed;

        Vector3 currentPos = transform.localPosition;

        if ((currentPos.y <= minY && move.y < 0) ||
            (currentPos.y >= maxY && move.y > 0))
            move.y = 0;

        if (rb != null)
            rb.linearVelocity = move;

        Vector3 pos = transform.localPosition;
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        transform.localPosition = pos;

        // ------------- 生存報酬 -------------
        AddReward(survivalReward);

        // ------------- 壁ペナルティ（弱め） -------------
        float wallPenalty = Mathf.Abs(transform.localPosition.y) / maxY;
        AddReward(-wallPenalty * 0.005f);  // ← 0.02 → 0.005 に弱く

        // ------------- 切り返しボーナス（速度ではなく方向反転で判定） -------------
        Vector2 vel = rb.linearVelocity;
        if (Mathf.Abs(prevVel.y) > 0.05f && prevVel.y * vel.y < 0)
        {
            AddReward(0.1f);  // 強めに
        }
        prevVel = vel;

        // ------------- 未来予測 -------------
        ApplyFuturePredictionReward();

    }

    private void ApplyFuturePredictionReward()
    {
        float minFutureDist = float.MaxValue;
        float t = predictionTime;

        var bullets = GameObject.FindGameObjectsWithTag("Enemy_Bullet");

        foreach (var b in bullets)
        {
            var eb = b.GetComponent<EnemyBullet>();
            if (eb == null) continue;

            Vector2 p = b.transform.position;
            Vector2 v = eb.Velocity;
            Vector2 a = eb.Acceleration;

            Vector2 futurePos = p + v * t + 0.5f * a * t * t;

            float dist = Vector2.Distance(futurePos, transform.position);
            if (dist < minFutureDist)
                minFutureDist = dist;
        }

        if (minFutureDist == float.MaxValue) return;

        if (minFutureDist < safeDistance)
        {
            AddReward(-(safeDistance - minFutureDist) * 0.01f);
        }
        else
        {
            AddReward(Mathf.Min((minFutureDist - safeDistance) * 0.005f, 0.02f));
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var c = actionsOut.ContinuousActions;

        c[0] = Input.GetAxisRaw("Horizontal");
        c[1] = Input.GetAxisRaw("Vertical");
        c[2] = Input.GetKey(KeyCode.LeftShift) ? 1f : 0f;
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Enemy_Bullet"))
        {
            AddReward(hitPenalty);
            EndEpisode();
        }
    }
}
