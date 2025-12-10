using UnityEngine;

public class EnemyBullet : MonoBehaviour
{
    [Header("Screen Bounds")]
    public float minX = -10f;
    public float maxX = 10f;
    public float minY = -6f;
    public float maxY = 6f;

    // パラメータ（値型 struct に変更）
    private spanData speedData;
    private spanData angleData;

    private float currentSpeed;
    private float currentAngle;
    private float speedAcc;
    private float angleAcc;
    private float maxSpeed;

    private Vector3 velocity;
    // 👈 【追加】現在の速度ベクトルを外部に公開するプロパティ
    public Vector3 Velocity => velocity;

    // --- Setup（スポーナーから設定される） ---
    public void Setup(Vector3 pos, spanData speed, spanData angle)
    {
        transform.position = pos;

        speedData = speed;
        angleData = angle;

        currentSpeed = speedData.default_;
        currentAngle = angleData.default_;

        speedAcc = speedData.accuracy_;
        angleAcc = angleData.accuracy_;
        maxSpeed = speedData.max_;

        UpdateVelocity();
    }

    void Update()
    {
        // 加速
        currentSpeed += speedAcc * Time.deltaTime;
        currentAngle += angleAcc * Time.deltaTime;

        // 最大速度制限
        if (maxSpeed > 0 && currentSpeed > maxSpeed)
            currentSpeed = maxSpeed;

        UpdateVelocity();

        transform.position += velocity * Time.deltaTime;

        CheckOutOfBounds();
    }

    private void UpdateVelocity()
    {
        float rad = currentAngle * Mathf.Deg2Rad;
        velocity = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * currentSpeed;
    }

    private void CheckOutOfBounds()
    {
        Vector3 p = transform.position;

        if (p.x < minX || p.x > maxX || p.y < minY || p.y > maxY)
            Destroy(gameObject);
    }

}
