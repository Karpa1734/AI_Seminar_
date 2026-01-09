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

    [Header("Unique Skill Settings")]
    [SerializeField] private GameObject warningLinePrefab; // 予告線プレハブ
    [SerializeField] private float bottomY = -5.5f;       // 画面最下部のY座標

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
    // --- StartBurst メソッドを以下のように修正 ---
    private void StartBurst(int index)
    {
        // index 3 (Vキー) をユニークスキルとして扱う例
        if (index == 3)
        {
            StartCoroutine(ExecuteUniqueSkillRoutine(index));
        }
        else
        {
            isFiringBurst[index] = true;
            burstRemain[index] = attackPatterns[index].burstCount;
            Fire(index);
        }
    }

    // --- ユニークスキル専用のコルーチンを追加 ---
    private IEnumerator ExecuteUniqueSkillRoutine(int index)
    {
        if (target == null || attackPatterns[index] == null) yield break;

        // スキル発動中フラグを立てる（これで移動速度減衰が適用される）
        isFiringBurst[index] = true;

        // 1. 予告線の発生
        // 敵（target）の現在のX座標、画面最下部（bottomY）
        Vector3 spawnPos = new Vector3(target.position.x, bottomY, 0f);
        GameObject warningLine = null;
        if (warningLinePrefab != null)
        {
            warningLine = Instantiate(warningLinePrefab, spawnPos, Quaternion.identity);
        }

        // 2. 30フレーム待機
        for (int i = 0; i < 30; i++) yield return null;

        // 予告線を削除
        if (warningLine != null) Destroy(warningLine);

        // 3. 60フレーム間、弾幕を噴き出す
        ShotData data = attackPatterns[index].shotData;
        for (int i = 0; i < 60; i++)
        {
            // 射撃中も敵のX座標を追いかける場合はループ内で spawnPos を更新
            Vector3 currentFirePos = new Vector3(target.position.x, bottomY, 0f);

            // 88~92度の範囲でランダムに発射
            float randomAngle = Random.Range(88f, 92f);

            // 既存の速度差ロジックを適用して発射
            int sCount = Mathf.Max(1, data.speedCount);
            for (int s = 0; s < sCount; s++)
            {
                float currentSpeed = (sCount > 1)
                    ? Mathf.Lerp(data.speedData.default_, data.speedMax, (float)s / (sCount - 1))
                    : data.speedData.default_;

                // 弾を生成
                SpawnFromPool(data.bulletType, currentFirePos, randomAngle, data.speedData, data.angleAccData, data.launchDelay, currentSpeed);
            }

            yield return null; // 1フレーム待機
        }

        // 4. 終了処理とリキャスト開始
        // ここで isFiringBurst[index] が false になり、速度減衰も解除される
        StopBurstAndStartRecast(index);
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



    private void ShootSingle(ShotData data)
    {
        float angle = CalculateAngle(data);
        Vector3 spawnPos = GetOffsetPosition(angle, data.spawnRadius);

        // 速度ループを追加
        for (int s = 0; s < data.speedCount; s++)
        {
            float currentSpeed = (data.speedCount > 1)
                ? Mathf.Lerp(data.speedData.default_, data.speedMax, (float)s / (data.speedCount - 1))
                : data.speedData.default_;

            SpawnFromPool(data.bulletType, spawnPos, angle, data.speedData, data.angleAccData, data.launchDelay, currentSpeed);
        }
    }

    private void ShootExpandingNWay(ShotData data, int step)
    {
        int actualCount = data.nWayCount + (data.nWayExpand != null ? data.nWayExpand.countAdd * step : 0);
        float actualSpread = data.nWaySpread + (data.nWayExpand != null ? data.nWayExpand.spreadAdd * step : 0);

        float baseAngle = CalculateAngle(data);
        float startAngle = baseAngle - (actualSpread / 2f);
        float angleStep = (actualCount > 1) ? actualSpread / (actualCount - 1) : 0;

        // 速度ループを外側に追加
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

    private void ShootAllDirections(ShotData data)
    {
        float baseAngle = CalculateAngle(data);
        float step = 360f / data.RoundCount;
        float offset = (data.RoundCount % 2 == 0) ? step / 2f : 0f;

        // 速度ループを外側に追加
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

    // 引数の最後に float overrideSpeed を追加
    private void SpawnFromPool(BulletData bData, Vector3 spawnPos, float angle, spanData sData, spanData aData, int delayFrames, float overrideSpeed)
    {
        if (BulletPool.Instance == null) return;

        if (delayFrames <= 0)
        {
            ExecuteActualSpawn(bData, spawnPos, angle, sData, aData, overrideSpeed);
        }
        else
        {
            StartCoroutine(DelayedSpawnRoutine(bData, spawnPos, angle, sData, aData, delayFrames, overrideSpeed));
        }
    }

    private IEnumerator DelayedSpawnRoutine(BulletData bData, Vector3 spawnPos, float angle, spanData sData, spanData aData, int delayFrames, float overrideSpeed)
    {
        // --- エフェクト生成コード（変更なし） ---
        if (delayEffectPrefab != null) { /* ...既存のコード... */ }

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

            // ★修正ポイント：速度制限(spMax)を、
            // 「データ上の制限値(sData.max_)」か「今回の弾の速度(overrideSpeed)」の大きい方に合わせる
            float speedLimit = Mathf.Max(sData.max_, overrideSpeed);

            // 第3引数に overrideSpeed、第5引数に speedLimit を渡す
            bullet.Setup(bData, spawnPos, overrideSpeed, sData.accuracy_, speedLimit, angle, aData.accuracy_, aData.max_, order);

            // デバッグ用：実際に設定された速度をコンソールに出す（確認後消してOK）
            // Debug.Log($"Spawned Bullet - Speed: {overrideSpeed}, Limit: {speedLimit}");
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