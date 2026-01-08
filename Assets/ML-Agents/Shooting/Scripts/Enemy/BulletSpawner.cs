using UnityEngine;
using Unity.MLAgents;
using System.Collections;

public class BulletSpawner : MonoBehaviour
{
    public enum TeamSide { Player1, Player2 }
    public TeamSide myTeam;
    public CharacterData characterProfile;

    [Header("Effect Settings")]
    public GameObject delayEffectPrefab;
    [Tooltip("RED, ORANGE, YELLOW, GREEN, AQUA, BLUE, PURPLE, WHITE の順で登録")]
    public Sprite[] delaySprites = new Sprite[8];

    private AttackPattern[] attackPatterns = new AttackPattern[4];
    private float[] recastTimers = new float[4];
    private Transform target;
    private bool[] lastInputState = new bool[4]; // ★前フレームのボタン状態を保持
    private int[] burstRemain = new int[4];
    private float[] burstIntervalTimers = new float[4];
    private bool[] isFiringBurst = new bool[4];
    private bool[] isInputHeld = new bool[4];

    private static int largeCounter = 1000;
    private static int middleCounter = 6000;
    private static int smallCounter = 11000;

    private void Start()
    {
        if (characterProfile != null)
        {
            attackPatterns[0] = characterProfile.shotZ;
            attackPatterns[1] = characterProfile.shotX;
            attackPatterns[2] = characterProfile.shotC;
            attackPatterns[3] = characterProfile.shotV;
        }
        gameObject.layer = LayerMask.NameToLayer(myTeam.ToString());
        FindTarget();
    }

    private void Update()
    {
        for (int i = 0; i < 4; i++)
        {
            if (recastTimers[i] > 0) recastTimers[i] -= Time.deltaTime;

            if (isFiringBurst[i])
            {
                if (!isInputHeld[i])
                {
                    StopBurstAndStartRecast(i);
                    continue;
                }

                burstIntervalTimers[i] -= Time.deltaTime;
                if (burstIntervalTimers[i] <= 0) Fire(i);
            }
        }
    }

    public void UpdateInputState(int attackAction)
    {
        for (int i = 0; i < 4; i++)
        {
            bool currentlyPressed = (attackAction == i + 1);
            isInputHeld[i] = currentlyPressed;

            if (!isInputHeld[i] && isFiringBurst[i])
            {
                StopBurstAndStartRecast(i);
            }
            else if (isInputHeld[i] && !isFiringBurst[i] && recastTimers[i] <= 0)
            {
                if (attackPatterns[i] != null) StartBurst(i);
            }
        }
    }

    private void StartBurst(int index)
    {
        isFiringBurst[index] = true;
        burstRemain[index] = attackPatterns[index].burstCount;
        Fire(index);
    }

    private void Fire(int index)
    {
        int currentStep = attackPatterns[index].burstCount - burstRemain[index];
        ExecuteShot(attackPatterns[index].shotData, currentStep);

        burstRemain[index]--;

        if (burstRemain[index] > 0)
        {
            burstIntervalTimers[index] = attackPatterns[index].burstInterval;
        }
        else
        {
            StopBurstAndStartRecast(index);
        }
    }

    private void StopBurstAndStartRecast(int index)
    {
        isFiringBurst[index] = false;
        burstRemain[index] = 0;
        recastTimers[index] = attackPatterns[index].recastTime;
    }

    private void ExecuteShot(ShotData data, int step)
    {
        if (data == null || data.bulletType == null) return;

        switch (data.pattern)
        {
            case ShotData.PatternType.Single:
                ShootSingle(data);
                break;
            case ShotData.PatternType.NWay:
                ShootExpandingNWay(data, step);
                break;
            case ShotData.PatternType.AllDirections:
                ShootAllDirections(data);
                break;
        }
    }

    // 指定半径分遠ざけた座標を計算するメソッド
    private Vector3 GetOffsetPosition(float angle, float radius)
    {
        float rad = angle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * radius;
        return transform.position + offset;
    }

    private void ShootExpandingNWay(ShotData data, int step)
    {
        int actualCount = data.nWayCount + (data.nWayExpand != null ? data.nWayExpand.countAdd * step : 0);
        float actualSpread = data.nWaySpread + (data.nWayExpand != null ? data.nWayExpand.spreadAdd * step : 0);

        float baseAngle = CalculateAngle(data);
        float startAngle = baseAngle - (actualSpread / 2f);
        float angleStep = (actualCount > 1) ? actualSpread / (actualCount - 1) : 0;

        for (int i = 0; i < actualCount; i++)
        {
            float currentAngle = startAngle + (angleStep * i);
            Vector3 spawnPos = GetOffsetPosition(currentAngle, data.spawnRadius); // 半径オフセット適用
            SpawnFromPool(data.bulletType, spawnPos, currentAngle, data.speedData, data.angleAccData, data.launchDelay);
        }
    }

    private void ShootSingle(ShotData data)
    {
        float angle = CalculateAngle(data);
        Vector3 spawnPos = GetOffsetPosition(angle, data.spawnRadius); // 半径オフセット適用
        SpawnFromPool(data.bulletType, spawnPos, angle, data.speedData, data.angleAccData, data.launchDelay);
    }

    private void ShootAllDirections(ShotData data)
    {
        float baseAngle = CalculateAngle(data);
        float step = 360f / data.RoundCount;
        float offset = (data.RoundCount % 2 == 0) ? step / 2f : 0f; // 偶数自機外し

        for (int i = 0; i < data.RoundCount; i++)
        {
            float currentAngle = baseAngle + (step * i) + offset;
            Vector3 spawnPos = GetOffsetPosition(currentAngle, data.spawnRadius); // 半径オフセット適用
            SpawnFromPool(data.bulletType, spawnPos, currentAngle, data.speedData, data.angleAccData, data.launchDelay);
        }
    }

    private void SpawnFromPool(BulletData bData, Vector3 spawnPos, float angle, spanData sData, spanData aData, int delayFrames)
    {
        if (BulletPool.Instance == null) return;

        if (delayFrames <= 0)
        {
            ExecuteActualSpawn(bData, spawnPos, angle, sData, aData);
        }
        else
        {
            StartCoroutine(DelayedSpawnRoutine(bData, spawnPos, angle, sData, aData, delayFrames));
        }
    }

    private IEnumerator DelayedSpawnRoutine(BulletData bData, Vector3 spawnPos, float angle, spanData sData, spanData aData, int delayFrames)
    {
        if (delayEffectPrefab != null)
        {
            // エフェクトを指定座標（spawnPos）に生成
            GameObject fxObj = Instantiate(delayEffectPrefab, spawnPos, Quaternion.identity, transform);
            var fx = fxObj.GetComponent<LaunchDelayEffect>();

            if (fx != null)
            {
                float baseScale = (bData.sizeCategory == BulletData.SizeCategory.Small) ? 0.6f :
                                  (bData.sizeCategory == BulletData.SizeCategory.Large) ? 1.8f : 1.0f;
                float finalMaxScale = baseScale * bData.localScale.x;

                Sprite targetSprite = (delaySprites != null && (int)bData.delayColor < delaySprites.Length)
                                      ? delaySprites[(int)bData.delayColor] : null;

                int order = GetNextOrder(bData.sizeCategory);
                fx.Setup(targetSprite, delayFrames, order, finalMaxScale);
            }
        }

        // Delayフレーム待機（この間、弾の変化カウントは進まない）
        for (int i = 0; i < delayFrames; i++) yield return null;

        ExecuteActualSpawn(bData, spawnPos, angle, sData, aData);
    }

    private void ExecuteActualSpawn(BulletData bData, Vector3 spawnPos, float angle, spanData sData, spanData aData)
    {
        GameObject b = BulletPool.Instance.Get();
        b.transform.position = spawnPos; // 指定座標に配置
        b.layer = LayerMask.NameToLayer(myTeam.ToString() + "_Bullet");

        var bullet = b.GetComponent<EnemyBullet>();
        if (bullet != null)
        {
            int order = GetNextOrder(bData.sizeCategory);
            // 弾のセットアップ（ここから多段変化のカウントが始まる）
            bullet.Setup(bData, spawnPos, sData.default_, sData.accuracy_, sData.max_, angle, aData.accuracy_, aData.max_, order);
        }
    }

    private int GetNextOrder(BulletData.SizeCategory category)
    {
        switch (category)
        {
            case BulletData.SizeCategory.Large:
                largeCounter = (largeCounter >= 5999) ? 1000 : largeCounter + 1;
                return largeCounter;
            case BulletData.SizeCategory.Middle:
                middleCounter = (middleCounter >= 10999) ? 6000 : middleCounter + 1;
                return middleCounter;
            case BulletData.SizeCategory.Small:
                smallCounter = (smallCounter >= 15999) ? 11000 : smallCounter + 1;
                return smallCounter;
            default: return 0;
        }
    }

    private float CalculateAngle(ShotData data)
    {
        switch (data.angleType)
        {
            case ShotData.AngleType.AimAtPlayer:
                if (target != null)
                {
                    Vector2 dir = target.position - transform.position;
                    return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                }
                return data.fixedAngle;
            case ShotData.AngleType.Random:
                return Random.Range(1f, 360f); // 1-360度の乱数
            default:
                return data.fixedAngle;
        }
    }
    // ★追加：現在の射撃状況から速度倍率を算出
    public float GetCurrentSpeedMultiplier()
    {
        float multiplier = 1f;
        for (int i = 0; i < 4; i++)
        {
            if (isFiringBurst[i] && attackPatterns[i] != null)
            {
                // アクティブな攻撃の中で最も小さい倍率を採用
                multiplier = Mathf.Min(multiplier, attackPatterns[i].firingSpeedMultiplier);
            }
        }
        return multiplier;
    }
    public float GetRecastProgress(int index)
    {
        if (index < 0 || index >= 4 || attackPatterns[index] == null || attackPatterns[index].recastTime <= 0) return 0f;
        return Mathf.Clamp01(recastTimers[index] / attackPatterns[index].recastTime);
    }

    private void FindTarget()
    {
        string enemyTag = (myTeam == TeamSide.Player1) ? "Player2" : "Player1";
        GameObject targetObj = GameObject.FindGameObjectWithTag(enemyTag);
        if (targetObj != null) target = targetObj.transform;
    }
}