using UnityEngine;
using System.Collections;

public class BulletSpawner : MonoBehaviour
{
    public enum TeamSide { Player1, Player2 }
    public TeamSide myTeam;
    public CharacterData characterProfile;
    private Transform target;
    // ★ 技ごとの弾データをインスペクターで設定できるようにする
    [Header("Tactics Bullet Presets")]
    public BulletData[] tacticBulletData; // 要素数を4に設定してください
    // パターンの種類を定義
    public enum BulletPattern { Circle, Flower, Spiral, AimedNWay }
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
    // ★ プリセット弾幕の発射ロジック
    public void FirePreset(BulletPattern pattern, Vector3 spawnPos, float playerSkill, float spinningOffset)
    {
        if (characterProfile == null || characterProfile.mainPattern == null) return;
        BulletData bData = characterProfile.mainPattern.shotData.bulletType;

        switch (pattern)
        {
            case BulletPattern.Circle:
                // 【円形弾】 密度重視の全方位
                int cCount = Mathf.RoundToInt(Mathf.Lerp(12f, 32f, playerSkill));
                float cStep = 360f / cCount;
                for (int i = 0; i < cCount; i++)
                {
                    SpawnFromPool(bData, spawnPos, i * cStep + spinningOffset, 3.5f, 0, myTeam);
                }
                break;

            case BulletPattern.Flower:
                // 【花形弾】 数学的な「揺らぎ」を持つ美しい弾幕
                int fCount = Mathf.RoundToInt(Mathf.Lerp(24f, 48f, playerSkill));
                float fStep = 360f / fCount;
                float petals = 5f; // 花びらの数
                for (int i = 0; i < fCount; i++)
                {
                    // Sin波を使って速度に差を出し、花びらの形を作る
                    float speed = 2.5f + Mathf.Abs(Mathf.Sin(i * fStep * petals * Mathf.Deg2Rad)) * 2.0f;
                    SpawnFromPool(bData, spawnPos, i * fStep + spinningOffset, speed, 0, myTeam);
                }
                break;

            case BulletPattern.Spiral:
                // 【螺旋弾】 渦を巻くように追い詰める
                int arms = (playerSkill > 0.6f) ? 3 : 2; // 技量が高いと3条の螺旋に
                for (int a = 0; a < arms; a++)
                {
                    float angle = (360f / arms) * a + spinningOffset;
                    // 角速度(angularVelocity)を付与して曲げる
                    SpawnFromPool(bData, spawnPos, angle, 4.0f, 15f, myTeam);
                }
                break;

            case BulletPattern.AimedNWay:
                // 【狙い撃ちN-way】 プレイヤーの現在位置を狙う
                float aimAngle = CalculateAngleToPlayer();
                int nWay = Mathf.RoundToInt(Mathf.Lerp(3f, 7f, playerSkill));
                float spread = 30f + (playerSkill * 20f);
                FireMultiRaw(spawnPos, 5.0f, aimAngle, 0, nWay, spread);
                break;
        }
    }
    // BulletSpawner.cs の ExecuteTactic メソッド内

    // BulletSpawner.cs の ExecuteTactic メソッドを更新

    public void ExecuteTactic(int tacticIndex, Vector3 bossPos, Vector3 remoteOffset, float playerSkill)
    {
        if (characterProfile == null || tacticBulletData == null || tacticBulletData.Length <= tacticIndex) return;

        // ★ タクティクスに応じた弾データを選択
        BulletData bData = tacticBulletData[tacticIndex];

        float baseAngle = Random.Range(0f, 360f);
        float wayMultiplier = 1.0f + playerSkill * 2.5f;
        float speedMultiplier = 1.0f + playerSkill * 0.5f;

        switch (tacticIndex)
        {
            case 0: // 【周辺】bData を使用して発射
                int way0 = Mathf.RoundToInt(8 * wayMultiplier);
                Vector3 remoteCenter22 = bossPos + remoteOffset;
                for (int i = 0; i < way0; i++)
                {
                    float angle = i * (360f / way0) + baseAngle;
                    Vector3 offset = Quaternion.Euler(0, 0, angle) * Vector3.right * 1.8f;
                    SpawnFromPool(bData, remoteCenter22, angle, 3.0f * speedMultiplier, 0, myTeam);
                }
                break;

            case 1: // 【リモート】bData を使用して発射
                int way1 = Mathf.RoundToInt(6 * (wayMultiplier/2));
                Vector3 remoteCenter = bossPos + remoteOffset;
                for (int i = 0; i < way1; i++)
                {
                    float angle = i * (360f / way1) + baseAngle;
                    SpawnFromPool(bData, remoteCenter, angle, 2.8f * speedMultiplier, 0, myTeam);
                    SpawnFromPool(bData, remoteCenter, angle, 2.0f * speedMultiplier, 0, myTeam);
                }
                break;

            case 2: // 【画面端】bData を使用して発射
                int way2 = Mathf.RoundToInt(9 * wayMultiplier);
                float xPos = (Random.value > 0.5f) ? -4.5f : 4.5f;
                Vector3 edgePos = new Vector3(xPos, 4.5f, 0);
                for (int i = 0; i < way2; i++)
                {
                    float angle = i * (360f / way2) + baseAngle;
                    SpawnFromPool(bData, edgePos, angle, 4.5f * speedMultiplier, 0, myTeam);
                }
                break;

            case 3: // 【高速中心】bData を使用して発射
                int way3 = Mathf.RoundToInt(6 * wayMultiplier);
                for (int i = 0; i < way3; i++)
                {
                    float angle = (i * (360f / way3) + baseAngle) % 360f;
                    if (angle > 250f && angle < 290f) continue;
                    SpawnFromPool(bData, bossPos, angle, 6.5f * speedMultiplier, 0, myTeam);
                }
                break;
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