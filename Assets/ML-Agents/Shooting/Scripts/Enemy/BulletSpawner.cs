using UnityEngine;

[System.Serializable]
public class spanData
{
    public spanData() { }
    public spanData(float speed) { default_ = speed; }
    public spanData(float speed, float ac, float max) { default_ = speed; accuracy_ = ac; max_ = max; }
    public float default_;
    public float accuracy_;
    public float max_;
}

[System.Serializable]
public class spanData2
{
    public spanData2() { }
    public spanData2(float? speed) { default_ = speed; }
    public spanData2(float? speed, float ac, float max) { default_ = speed; accuracy_ = ac; max_ = max; }
    public float? default_;
    public float accuracy_;
    public float max_;
}

public class BulletSpawner : MonoBehaviour
{
    public GameObject bulletPrefab;
    public float interval = 0.5f;
    private float timer;

    // --- 弾の基本パラメータ ---
    public spanData speedData = new spanData(3f, 0.5f, 8f);
    public spanData angleData = new spanData(270f, 0f, 0f);

    // 自機参照（Inspectorに入れてもOK、nullならFindで探す）
    public Transform player;

    private void Start()
    {
        // 自動取得
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= interval)
        {
            timer = 0;

            // ★ 弾幕パターン切り替え（必要な方だけオンにする）
            //ShootAllDirections();   // ← 全方位弾
            ShootAimAtPlayer();       // ← 自機狙い弾
        }
    }

    // ================================================
    // ★ 1. 自機狙い弾（今回追加）
    // ================================================
    void ShootAimAtPlayer()
    {
        if (player == null) return;

        // プレイヤー方向の角度を求める
        Vector3 dir = (player.position - transform.position).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // 1. 弾の生成
        var b = Instantiate(bulletPrefab);

        // 2. 発射位置
        Vector3 pos = transform.position;
        b.transform.position = pos;

        // 3. Setup
        var bullet = b.GetComponent<EnemyBullet>();
        if (bullet != null)
        {
            float initSpeed = speedData.default_;
            float spAcc = speedData.accuracy_;
            float spMax = speedData.max_;

            float initAngle = angle + Random.Range(-5,5);            // 自機方向の角度
            float anAcc = angleData.accuracy_;  // 角加速度（必要なら）

            bullet.Setup(pos, initSpeed, spAcc, spMax, initAngle, anAcc);
        }
    }

    // ================================================
    // ★ 2. 全方位弾（元のコード / 必要なら使う）
    // ================================================
    void ShootAllDirections()
    {
        float angle = Random.Range(1, 360);

        for (int i = 0; i < 36; i++)
        {
            var b = Instantiate(bulletPrefab);
            Vector3 pos = transform.position;
            b.transform.position = pos;

            var bullet = b.GetComponent<EnemyBullet>();
            if (bullet != null)
            {
                float initSpeed = speedData.default_;
                float spAcc = speedData.accuracy_;
                float spMax = speedData.max_;

                float initAngle = angle;
                float anAcc = angleData.accuracy_;

                bullet.Setup(pos, initSpeed, spAcc, spMax, initAngle, anAcc);
            }

            angle += 360f / 36f;
        }
    }
}
