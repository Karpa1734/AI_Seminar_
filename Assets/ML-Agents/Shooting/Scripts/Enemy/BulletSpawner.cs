using UnityEngine;
using UnityEngine.UIElements;

public class BulletSpawner : MonoBehaviour
{
    public GameObject bulletPrefab;
    public float interval = 0.3f;
    private float angle = 0;
    private float timer;

    // --- 弾の基本パラメータ ---
    public spanData speedData = new spanData(3f, 0.5f, 8f);
    public spanData angleData = new spanData(270f, 0f, 0f);

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= interval)
        {
            timer = 0;
            Spawn();
        }
    }

    void Spawn()
    {
        angle = Random.Range(1, 360);
        Vector3 pos = transform.position;
        for (int i = 0; i < 36; i++)
        {
            // 1. 弾の生成
            var b = Instantiate(bulletPrefab);

            var bullet = b.GetComponent<EnemyBullet>();

            // 速度データはそのまま使用
            var setupSpeedData = speedData;

            // 角度はループ内で計算し、新しいspanDataとして渡す（加速度などは0でOKなら）
            var setupAngleData = new spanData(angle);

            bullet.Setup(pos, setupSpeedData, setupAngleData);
            angle += 360 / 36;
        }
    }
}
