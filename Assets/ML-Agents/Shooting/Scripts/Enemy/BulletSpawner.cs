using UnityEngine;
using Unity.MLAgents;
using System.Collections;
using System.Collections.Generic;

public class BulletSpawner : MonoBehaviour
{
    public enum TeamSide { Player1, Player2 }
    public TeamSide myTeam;
    public CharacterData characterProfile;

    [Header("Effect Settings")]
    public GameObject delayEffectPrefab;
    [Tooltip("RED, ORANGE, YELLOW, GREEN, AQUA, BLUE, PURPLE, WHITE の順で登録")]
    public Sprite[] delaySprites = new Sprite[8];

    [Header("Unique Skill Settings")]
    [SerializeField] private GameObject warningLinePrefab;
    [SerializeField] private float bottomY = -5.5f;

    [Header("Fan Charge Settings")]
    [SerializeField] private float shrinkSpeed = 200f; // 1秒間に絞る角度
    [SerializeField] private float expandSpeed = 400f; // 離した後に戻る速さ

    private AttackPattern[] attackPatterns = new AttackPattern[4];
    private float[] recastTimers = new float[4];
    private Transform target;
    private int[] burstRemain = new int[4];
    private float[] burstIntervalTimers = new float[4];
    private bool[] isFiringBurst = new bool[4];
    private bool[] isInputHeld = new bool[4];

    // ★追加：溜め管理用の変数
    private bool[] isCharging = new bool[4];
    private float[] currentFanAngles = new float[4];

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

        // 各スキルの初期角度を最大値に設定
        for (int i = 0; i < 4; i++)
        {
            if (attackPatterns[i] != null && attackPatterns[i].shotData != null)
                currentFanAngles[i] = attackPatterns[i].shotData.nWaySpread;
        }

        gameObject.layer = LayerMask.NameToLayer(myTeam.ToString());
        FindTarget();
    }

    private void Update()
    {
        for (int i = 0; i < 4; i++)
        {
            if (recastTimers[i] > 0) recastTimers[i] -= Time.deltaTime;

            // ★ 扇の角度制御 (溜め中の角度収束)
            UpdateFanAngle(i);

            // バースト（連射）中の処理
            if (isFiringBurst[i])
            {
                // OnReleaseタイプの場合はボタンを離してもバーストが続くように条件を調整
                // (Instantタイプの場合のみ、ボタンを離したら止める)
                if (attackPatterns[i].shotData.bulletType.fireType == FireType.Instant && !isInputHeld[i])
                {
                    StopBurstAndStartRecast(i);
                    continue;
                }

                burstIntervalTimers[i] -= Time.deltaTime;
                if (burstIntervalTimers[i] <= 0) Fire(i);
            }
        }
    }

    private void UpdateFanAngle(int i)
    {
        if (attackPatterns[i] == null || attackPatterns[i].shotData == null) return;

        float maxAngle = attackPatterns[i].shotData.nWaySpread;
        float minAngle = 30f; // 最小30度まで絞れる

        if (isCharging[i])
        {
            // 溜め中：角度を絞る
            currentFanAngles[i] = Mathf.MoveTowards(currentFanAngles[i], minAngle, shrinkSpeed * Time.deltaTime);
        }
        else if (!isFiringBurst[i])
        {
            // 待機中：角度を元に戻す
            currentFanAngles[i] = Mathf.MoveTowards(currentFanAngles[i], maxAngle, expandSpeed * Time.deltaTime);
        }
    }

    public void UpdateInputState(int attackAction)
    {
        for (int i = 0; i < 4; i++)
        {
            if (attackPatterns[i] == null || attackPatterns[i].shotData == null || attackPatterns[i].shotData.bulletType == null) continue;

            bool currentlyPressed = (attackAction == i + 1);
            bool wasPressed = isInputHeld[i]; // 前フレームの状態を保持

            FireType fType = attackPatterns[i].fireType;

            // --- 以下の判定処理 ---
            if (currentlyPressed && !wasPressed)
            {
                if (recastTimers[i] <= 0)
                {
                    if (fType == FireType.Instant)
                    {
                        StartBurst(i);
                    }
                    else
                    {
                        isCharging[i] = true; // これで正しく溜め状態に入るようになります
                        Debug.Log($"Skill {i} Charge Start");
                    }
                }
            }

            // --- 2. 離された瞬間の判定 ---
            if (!currentlyPressed && wasPressed)
            {
                if (fType == FireType.OnRelease && isCharging[i])
                {
                    Debug.Log($"Skill {i} Released. Angle: {currentFanAngles[i]}");
                    isCharging[i] = false;
                    StartBurst(i);
                }

                // Instantタイプで連射を止めたい場合などはここ
                if (fType == FireType.Instant && isFiringBurst[i])
                {
                    StopBurstAndStartRecast(i);
                }

                // 溜めずに離した場合などのケア
                isCharging[i] = false;
            }

            // ★ 最後に状態を更新する
            isInputHeld[i] = currentlyPressed;
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

        // ★ ShootExpandingNWay で使用するスプレッド値を現在の溜め角度で上書きする準備
        ExecuteShot(attackPatterns[index].shotData, currentStep, index);

        burstRemain[index]--;

        if (burstRemain[index] > 0)
        {
            burstIntervalTimers[index] = attackPatterns[index].burstInterval;
        }
        else
        {
            StopBurstAndStartRecast(index);
            // 発射終了後に角度を即座にリセットする場合はここ
            // currentFanAngles[index] = attackPatterns[index].shotData.nWaySpread;
        }
    }

    private void StopBurstAndStartRecast(int index)
    {
        isFiringBurst[index] = false;
        isCharging[index] = false; // 念のため
        burstRemain[index] = 0;
        recastTimers[index] = attackPatterns[index].recastTime;
    }

    private void ExecuteShot(ShotData data, int step, int skillIndex)
    {
        if (data == null || data.bulletType == null) return;

        switch (data.pattern)
        {
            case ShotData.PatternType.Single:
                ShootSingle(data);
                break;
            case ShotData.PatternType.NWay:
                // ★ 現在の溜め角度を引数として渡すように修正
                ShootExpandingNWay(data, step, currentFanAngles[skillIndex]);
                break;
            case ShotData.PatternType.AllDirections:
                ShootAllDirections(data);
                break;
        }
    }

    private void ShootExpandingNWay(ShotData data, int step, float overridenSpread)
    {
        int actualCount = data.nWayCount + (data.nWayExpand != null ? data.nWayExpand.countAdd * step : 0);

        // ★ 元の固定値(data.nWaySpread)の代わりに溜め角度(overridenSpread)を使用
        float actualSpread = overridenSpread + (data.nWayExpand != null ? data.nWayExpand.spreadAdd * step : 0);

        float baseAngle = CalculateAngle(data);
        float startAngle = baseAngle - (actualSpread / 2f);
        float angleStep = (actualCount > 1) ? actualSpread / (actualCount - 1) : 0;

        for (int s = 0; s < data.speedCount; s++)
        {
            float currentSpeed = (data.speedCount > 1)
                ? Mathf.Lerp(data.speedData.default_, data.speedMax, (float)s / (data.speedCount - 1))
                : data.speedData.default_;

            for (int i = 0; i < actualCount; i++)
            {
                float currentAngle = startAngle + (angleStep * i);
                Vector3 spawnPos = GetOffsetPosition(currentAngle, data.spawnRadius);
                SpawnFromPool(data.bulletType, spawnPos, currentAngle, data.speedData, data.angleAccData, data.launchDelay, currentSpeed);
            }
        }
    }

    // --- 以降の ShootSingle, ShootAllDirections, SpawnFromPool, GetOffsetPosition 等は変更なし ---
    // (既存のコードをそのまま維持)

    private void ShootSingle(ShotData data)
    {
        float angle = CalculateAngle(data);
        Vector3 spawnPos = GetOffsetPosition(angle, data.spawnRadius);
        for (int s = 0; s < data.speedCount; s++)
        {
            float currentSpeed = (data.speedCount > 1)
                ? Mathf.Lerp(data.speedData.default_, data.speedMax, (float)s / (data.speedCount - 1))
                : data.speedData.default_;
            SpawnFromPool(data.bulletType, spawnPos, angle, data.speedData, data.angleAccData, data.launchDelay, currentSpeed);
        }
    }

    private void ShootAllDirections(ShotData data)
    {
        float baseAngle = CalculateAngle(data);
        float step = 360f / data.RoundCount;
        float offset = (data.RoundCount % 2 == 0) ? step / 2f : 0f;
        for (int s = 0; s < data.speedCount; s++)
        {
            float currentSpeed = (data.speedCount > 1)
                ? Mathf.Lerp(data.speedData.default_, data.speedMax, (float)s / (data.speedCount - 1))
                : data.speedData.default_;
            for (int i = 0; i < data.RoundCount; i++)
            {
                float currentAngle = baseAngle + (step * i) + offset;
                Vector3 spawnPos = GetOffsetPosition(currentAngle, data.spawnRadius);
                SpawnFromPool(data.bulletType, spawnPos, currentAngle, data.speedData, data.angleAccData, data.launchDelay, currentSpeed);
            }
        }
    }

    private void SpawnFromPool(BulletData bData, Vector3 spawnPos, float angle, spanData sData, spanData aData, int delayFrames, float overrideSpeed)
    {
        if (BulletPool.Instance == null) return;
        if (delayFrames <= 0) ExecuteActualSpawn(bData, spawnPos, angle, sData, aData, overrideSpeed);
        else StartCoroutine(DelayedSpawnRoutine(bData, spawnPos, angle, sData, aData, delayFrames, overrideSpeed));
    }

    private IEnumerator DelayedSpawnRoutine(BulletData bData, Vector3 spawnPos, float angle, spanData sData, spanData aData, int delayFrames, float overrideSpeed)
    {
        if (delayEffectPrefab != null) { /* エフェクト処理 */ }
        for (int i = 0; i < delayFrames; i++) yield return null;
        ExecuteActualSpawn(bData, spawnPos, angle, sData, aData, overrideSpeed);
    }

    private void ExecuteActualSpawn(BulletData bData, Vector3 spawnPos, float angle, spanData sData, spanData aData, float overrideSpeed)
    {
        GameObject b = BulletPool.Instance.Get();
        b.transform.position = spawnPos;
        b.layer = LayerMask.NameToLayer(myTeam.ToString() + "_Bullet");
        var bullet = b.GetComponent<EnemyBullet>();
        if (bullet != null)
        {
            int order = GetNextOrder(bData.sizeCategory);
            float speedLimit = Mathf.Max(sData.max_, overrideSpeed);
            bullet.Setup(bData, spawnPos, overrideSpeed, sData.accuracy_, speedLimit, angle, aData.accuracy_, aData.max_, order);
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
                return Random.Range(1f, 360f);
            default:
                return data.fixedAngle;
        }
    }

    private Vector3 GetOffsetPosition(float angle, float radius)
    {
        float rad = angle * Mathf.Deg2Rad;
        return transform.position + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * radius;
    }

    public float GetCurrentSpeedMultiplier()
    {
        float multiplier = 1f;
        for (int i = 0; i < 4; i++)
        {
            if (isFiringBurst[i] && attackPatterns[i] != null)
                multiplier = Mathf.Min(multiplier, attackPatterns[i].firingSpeedMultiplier);
        }
        return multiplier;
    }

    public float GetRecastProgress(int index)
    {
        if (index < 0 || index >= 4 || attackPatterns[index] == null || attackPatterns[index].recastTime <= 0) return 0f;
        return Mathf.Clamp01(recastTimers[index] / attackPatterns[index].recastTime);
    }

    public float GetRemainingRecastTime(int index)
    {
        if (index < 0 || index >= 4) return 0;
        return Mathf.Max(0, recastTimers[index]);
    }

    private void FindTarget()
    {
        string enemyTag = (myTeam == TeamSide.Player1) ? "Player2" : "Player1";
        GameObject targetObj = GameObject.FindGameObjectWithTag(enemyTag);
        if (targetObj != null) target = targetObj.transform;
    }
}