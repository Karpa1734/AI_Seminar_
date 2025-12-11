using UnityEngine;

public class EnemyBullet : MonoBehaviour
{
    // Screen bounds (set in inspector / or keep defaults)
    [Header("Screen Bounds")]
    public float minX = -10f;
    public float maxX = 10f;
    public float minY = -6f;
    public float maxY = 6f;

    // Parameters set by spawner
    private float currentSpeed = 3f;
    private float currentAngle = 270f; // degrees
    private float speedAcc = 0f;
    private float angleAcc = 0f;
    private float maxSpeed = 0f;

    // internal
    private Vector3 velocity = Vector3.zero;
    private Vector3 lastVelocity = Vector3.zero;

    // Expose velocity and acceleration for Agent observation
    public Vector3 Velocity => velocity;
    public Vector3 Acceleration => velocity - lastVelocity;

    // Setup called by spawner
    public void Setup(Vector3 pos, float initSpeed, float spAcc, float spMax, float initAngle, float anAcc)
    {
        transform.position = pos;
        currentSpeed = initSpeed;
        speedAcc = spAcc;
        maxSpeed = spMax;
        currentAngle = initAngle;
        angleAcc = anAcc;

        UpdateVelocityInstant();
    }

    // If you have spanData types, you can adapt call to Setup(spanData,...). Using simple floats here for clarity.

    void Update()
    {
        // Save last velocity for accel calculation
        lastVelocity = velocity;

        // Update speed and angle by acceleration
        currentSpeed += speedAcc * Time.deltaTime;
        currentAngle += angleAcc * Time.deltaTime;

        if (maxSpeed > 0f && currentSpeed > maxSpeed)
            currentSpeed = maxSpeed;

        UpdateVelocityInstant();

        // Move
        transform.position += velocity * Time.deltaTime;

        // Out of bounds => destroy
        CheckOutOfBounds();
    }

    private void UpdateVelocityInstant()
    {
        float rad = currentAngle * Mathf.Deg2Rad;
        velocity = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * currentSpeed;
    }

    private void CheckOutOfBounds()
    {
        Vector3 p = transform.position;
        if (p.x < minX || p.x > maxX || p.y < minY || p.y > maxY)
        {
            Destroy(gameObject);
        }
    }
}
