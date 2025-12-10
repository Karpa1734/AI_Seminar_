using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using System.Linq;

public class DodgerAgent : Agent
{
    public float speed = 5f;
    private Rigidbody2D rb;

    public int observeBulletCount = 3;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody2D>();
        transform.position = new Vector3(-5, 0, 0);
    }

    public override void OnEpisodeBegin()
    {
        transform.localPosition = new Vector3(-5, 0, 0);
        rb.linearVelocity = Vector2.zero;

        // 全弾削除
        foreach (var b in GameObject.FindGameObjectsWithTag("Enemy_Bullet"))
            Destroy(b);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Player position
        sensor.AddObservation(transform.localPosition);

        // -----------------------------
        // 近い弾 TOP3 のみ観測
        // -----------------------------
        var bullets = GameObject.FindGameObjectsWithTag("Enemy_Bullet");
        var nearest = bullets
            .OrderBy(b => Vector2.Distance(b.transform.position, transform.position))
            .Take(observeBulletCount)
            .ToList();

        foreach (var b in nearest)
        {
            Vector2 relPos = (Vector2)b.transform.localPosition - (Vector2)transform.localPosition;
            sensor.AddObservation(relPos);

            // 👈 【修正】EnemyBullet から Velocity を取得する
            var bulletComponent = b.GetComponent<EnemyBullet>();
            Vector3 bulletVelocity = bulletComponent != null ? bulletComponent.Velocity : Vector3.zero;

            // Rigidbody2D の代わりに弾の実際の速度を観測値に追加
            sensor.AddObservation(bulletVelocity);
        }

        // 足りない分はゼロパディング
        for (int i = nearest.Count; i < observeBulletCount; i++)
        {
            sensor.AddObservation(Vector2.zero);
            // 速度ベクトル用に追加
            sensor.AddObservation(Vector2.zero);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveX = actions.ContinuousActions[0];
        float moveY = actions.ContinuousActions[1];

        rb.linearVelocity = new Vector2(moveX, moveY) * speed;

        // 生存報酬
        AddReward(0.005f);
    }

    // 被弾
    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Enemy_Bullet"))
        {
            Debug.Log("HIT!!");
            AddReward(-1f);
            EndEpisode();
        }
    }
}
