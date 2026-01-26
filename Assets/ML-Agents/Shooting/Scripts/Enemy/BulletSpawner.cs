using UnityEngine;
using System.Collections;

public class BulletSpawner : MonoBehaviour
{
    public enum TeamSide { Player1, Player2 }
    public TeamSide myTeam;
    public CharacterData characterProfile;
    private Transform target;

    private void Start()
    {
        gameObject.layer = LayerMask.NameToLayer(myTeam.ToString());
        FindTarget();
    }

    private void FindTarget()
    {
        string enemyTag = (myTeam == TeamSide.Player1) ? "Player2" : "Player1";
        GameObject targetObj = GameObject.FindGameObjectWithTag(enemyTag);
        if (targetObj != null) target = targetObj.transform;
    }

    // 自機（Player）用のシンプルな垂直発射は維持
    public void FireSimpleVertical(float speed)
    {
        if (characterProfile == null || characterProfile.mainPattern == null) return;

        SpawnFromPool(characterProfile.mainPattern.shotData.bulletType, transform.position + new Vector3(-0.2f, 0, 0), 90f, speed, 0, myTeam);
        SpawnFromPool(characterProfile.mainPattern.shotData.bulletType, transform.position + new Vector3(0.2f, 0, 0), 90f, speed, 0, myTeam);
    }
    public void FireRaw(Vector3 pos, float speed, float angle, float angularVelocity)
    {
        if (characterProfile == null || characterProfile.mainPattern == null) return;
        BulletData bData = characterProfile.mainPattern.shotData.bulletType;
        SpawnFromPool(bData, pos, angle, speed, angularVelocity, myTeam);
    }

    // ★ AIの思うがままに撃たせるための「生（Raw）」の発射メソッド
    public void FireMultiRaw(Vector3 spawnPos, float speed, float baseAngle, float angularVelocity, int count, float spread)
    {
        if (characterProfile == null || characterProfile.mainPattern == null) return;
        BulletData bData = characterProfile.mainPattern.shotData.bulletType;

        // countが1の場合は単発、それ以外は範囲内に分割して発射
        float startAngle = (count > 1) ? baseAngle - (spread / 2f) : baseAngle;
        float step = (count > 1 && spread < 360f) ? spread / (count - 1) : 360f / count;

        for (int i = 0; i < count; i++)
        {
            float finalAngle = startAngle + (step * i);
            SpawnFromPool(bData, spawnPos, finalAngle, speed, angularVelocity, myTeam);
        }
    }

    private float CalculateAngleToPlayer()
    {
        if (target != null)
        {
            Vector2 dir = target.position - transform.position;
            return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }
        return -90f;
    }

    // プールから弾を取得して発射するコアメソッド
    private void SpawnFromPool(BulletData bData, Vector3 pos, float angle, float speed, float angularVelocity, TeamSide team)
    {
        if (BulletPool.Instance == null) return;

        GameObject b = BulletPool.Instance.Get();
        b.transform.position = pos;
        b.layer = LayerMask.NameToLayer(team.ToString() + "_Bullet");

        var bullet = b.GetComponent<EnemyBullet>();
        if (bullet != null)
        {
            // angularVelocity は EnemyBullet.cs の Setup 内の「anAcc（角度の変化量）」に渡します
            // 第7引数（anAcc）に angularVelocity を指定し、第8引数（anMax）に上限を設定します
            bullet.Setup(
                bData,
                pos,
                speed, 0, speed,      // 加速度なし、最大速度固定
                angle,
                angularVelocity, 1000f, // 角速度（秒間角度変化）、最大角速度
                0, team, this         // 表示順、チーム、スパナー参照
            );
        }
    }
}