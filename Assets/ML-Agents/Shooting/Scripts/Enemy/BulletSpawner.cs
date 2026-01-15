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
    [SerializeField] private float shrinkSpeed = 200f;
    [SerializeField] private float expandSpeed = 400f;

    [SerializeField] private FanMeshVisualizer fanVisualizer;
    [SerializeField] private float previewRadius = 5f;

    private AttackPattern[] attackPatterns = new AttackPattern[4];
    private float[] recastTimers = new float[4];
    private Transform target;
    private int[] burstRemain = new int[4];
    private float[] burstIntervalTimers = new float[4];
    private bool[] isFiringBurst = new bool[4];
    private bool[] isInputHeld = new bool[4];

    private bool[] isCharging = new bool[4];
    private float[] currentFanAngles = new float[4];

    private static int largeCounter = 1000;
    private static int middleCounter = 6000;
    private static int smallCounter = 11000;
    // BulletSpawner.cs 内に追加
    [SerializeField] private SkillUIManager uiManager;
    public static BulletSpawner Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (characterProfile != null)
        {
            attackPatterns[0] = characterProfile.shotZ;
            attackPatterns[1] = characterProfile.shotX;
            attackPatterns[2] = characterProfile.shotC;
            attackPatterns[3] = characterProfile.shotV;
        }

        for (int i = 0; i < 4; i++)
        {
            if (attackPatterns[i] != null && attackPatterns[i].shotData != null)
                currentFanAngles[i] = attackPatterns[i].shotData.nWaySpread;
        }
        if (uiManager != null)
        {
            uiManager.SetupIcons(attackPatterns); // スキル配列を渡してアイコンをセット
        }
        gameObject.layer = LayerMask.NameToLayer(myTeam.ToString());
        FindTarget();
    }

    private void Update()
    {
        bool anyCharging = false;
        int chargingIndex = -1;

        for (int i = 0; i < 4; i++)
        {
            if (attackPatterns[i] == null) continue;

            if (recastTimers[i] > 0) recastTimers[i] -= Time.deltaTime;

            UpdateFanAngleValues(i);

            if (isCharging[i])
            {
                anyCharging = true;
                chargingIndex = i;
            }

            if (isFiringBurst[i])
            {
                if (attackPatterns[i].fireType == FireType.Instant && !isInputHeld[i])
                {
                    StopBurstAndStartRecast(i);
                    continue;
                }

                burstIntervalTimers[i] -= Time.deltaTime;
                if (burstIntervalTimers[i] <= 0) Fire(i);
            }
        }

        if (anyCharging && fanVisualizer != null)
        {
            float baseAngle = CalculateAngle(attackPatterns[chargingIndex].shotData, transform.position);
            fanVisualizer.DrawFan(baseAngle, currentFanAngles[chargingIndex], previewRadius);
        }
        else if (fanVisualizer != null)
        {
            fanVisualizer.Hide();
        }
    }

    private void UpdateFanAngleValues(int i)
    {
        if (attackPatterns[i] == null || attackPatterns[i].shotData == null) return;

        float maxAngle = attackPatterns[i].shotData.nWaySpread;
        float minAngle = 10f;

        if (isCharging[i])
        {
            currentFanAngles[i] = Mathf.MoveTowards(currentFanAngles[i], minAngle, shrinkSpeed * Time.deltaTime);
        }
        else if (!isFiringBurst[i])
        {
            currentFanAngles[i] = Mathf.MoveTowards(currentFanAngles[i], maxAngle, expandSpeed * Time.deltaTime);
        }
    }

    public void UpdateInputState(int attackAction)
    {
        for (int i = 0; i < 4; i++)
        {
            if (attackPatterns[i] == null) continue;

            bool currentlyPressed = (attackAction == i + 1);
            bool wasPressed = isInputHeld[i];
            FireType fType = attackPatterns[i].fireType;
            bool autoRepeat = attackPatterns[i].isAutoRepeat;

            if (currentlyPressed)
            {
                bool triggerInput = false;
                if (!wasPressed) triggerInput = true;
                else if (autoRepeat && recastTimers[i] <= 0 && !isFiringBurst[i] && !isCharging[i]) triggerInput = true;

                if (triggerInput && recastTimers[i] <= 0)
                {
                    if (fType == FireType.Instant) StartBurst(i);
                    else if (fType == FireType.OnRelease) isCharging[i] = true;
                }
            }

            if (!currentlyPressed && wasPressed)
            {
                if (fType == FireType.OnRelease && isCharging[i])
                {
                    isCharging[i] = false;
                    StartBurst(i);
                }
                isCharging[i] = false;
            }
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
        ExecuteShot(attackPatterns[index].shotData, currentStep, index);
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
        isCharging[index] = false;
        burstRemain[index] = 0;
        recastTimers[index] = attackPatterns[index].recastTime;
    }

    public void ExecuteShot(ShotData data, int step, int skillIndex, Vector3? overrideOrigin = null, TeamSide? overrideTeam = null)
    {
        if (data == null || data.bulletType == null) return;

        Vector3 origin = overrideOrigin ?? transform.position;
        TeamSide team = overrideTeam ?? myTeam;
        switch (data.pattern)
        {
            case ShotData.PatternType.Single:
                ShootSingle(data, origin, team);
                break;
            case ShotData.PatternType.NWay:
                float spread = currentFanAngles[skillIndex];
                ShootExpandingNWay(data, step, spread, origin, team);
                break;
            case ShotData.PatternType.AllDirections:
                ShootAllDirections(data, origin, team);
                break;
        }
    }

    // 1. NWay弾の修正
    private void ShootExpandingNWay(ShotData data, int step, float overridenSpread, Vector3 origin, TeamSide team) // 第4引数を追加
    {
        int actualCount = data.nWayCount + (data.nWayExpand != null ? data.nWayExpand.countAdd * step : 0);
        float actualSpread = overridenSpread + (data.nWayExpand != null ? data.nWayExpand.spreadAdd * step : 0);

        float baseAngle = CalculateAngle(data, origin); // originを渡す
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
                // ★ GetOffsetPositionに origin を渡す！
                Vector3 spawnPos = GetOffsetPosition(currentAngle, data.spawnRadius, origin);
                SpawnFromPool(data.bulletType, spawnPos, currentAngle, data.speedData, data.angleAccData, data.launchDelay, currentSpeed, team);
            }
        }
    }

    // 2. Single弾の修正
    private void ShootSingle(ShotData data, Vector3 origin, TeamSide team) // 第2引数を追加
    {
        float angle = CalculateAngle(data, origin); // originを渡す
        Vector3 spawnPos = GetOffsetPosition(angle, data.spawnRadius, origin); // originを渡す
        for (int s = 0; s < data.speedCount; s++)
        {
            float currentSpeed = (data.speedCount > 1)
                ? Mathf.Lerp(data.speedData.default_, data.speedMax, (float)s / (data.speedCount - 1))
                : data.speedData.default_;
            SpawnFromPool(data.bulletType, spawnPos, angle, data.speedData, data.angleAccData, data.launchDelay, currentSpeed, team);
        }
    }

    // 3. 全方位弾の修正
    private void ShootAllDirections(ShotData data, Vector3 origin, TeamSide team) // 第2引数を追加
    {
        float baseAngle = CalculateAngle(data, origin); // originを渡す
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
                Vector3 spawnPos = GetOffsetPosition(currentAngle, data.spawnRadius, origin); // originを渡す
                SpawnFromPool(data.bulletType, spawnPos, currentAngle, data.speedData, data.angleAccData, data.launchDelay, currentSpeed, team);
            }
        }
    }

    private void SpawnFromPool(BulletData bData, Vector3 spawnPos, float angle, spanData sData, spanData aData, int delayFrames, float overrideSpeed, TeamSide team)
    {
        if (BulletPool.Instance == null) return;

        if (delayFrames <= 0)
        {
            ExecuteActualSpawn(bData, spawnPos, angle, sData, aData, overrideSpeed, team);
        }
        else
        {
            // ★ 修正：データ側で「予兆を使う」設定になっており、プレハブがある場合のみ実行
            if (bData.useLaunchDelayEffect && delayEffectPrefab != null)
            {
                GameObject effectObj = Instantiate(delayEffectPrefab, spawnPos, Quaternion.Euler(0, 0, angle));
                LaunchDelayEffect effect = effectObj.GetComponent<LaunchDelayEffect>();

                if (effect != null)
                {
                    // ★ 修正：bData.delayColor をインデックスとして使用し、適切な色のスプライトを渡す
                    int colorIndex = (int)bData.delayColor;
                    Sprite delaySprite = (colorIndex < delaySprites.Length) ? delaySprites[colorIndex] : bData.bulletSprite;

                    // 槍のスケールに合わせてエフェクトの初期サイズも調整（bData.localScaleを利用）
                    float effectScale = bData.localScale.x * 2.0f;
                    effect.Setup(delaySprite, delayFrames, 1000, effectScale);
                }
            }

            StartCoroutine(DelayedSpawnRoutine(bData, spawnPos, angle, sData, aData, delayFrames, overrideSpeed, team));
        }
    }

    private IEnumerator DelayedSpawnRoutine(BulletData bData, Vector3 spawnPos, float angle, spanData sData, spanData aData, int delayFrames, float overrideSpeed, TeamSide team) // ★teamを追加
    {
        for (int i = 0; i < delayFrames; i++) yield return null;

        // 遅延後に生成する際、保持していた team を渡す
        ExecuteActualSpawn(bData, spawnPos, angle, sData, aData, overrideSpeed, team);
    }
    private void ExecuteActualSpawn(BulletData bData, Vector3 spawnPos, float angle, spanData sData, spanData aData, float overrideSpeed, TeamSide team) // 引数に team を追加
    {
        GameObject b = BulletPool.Instance.Get();
        b.transform.position = spawnPos;

        // ★ 渡されたチーム情報に基づいてレイヤーを設定する
        b.layer = LayerMask.NameToLayer(team.ToString() + "_Bullet");

        var bullet = b.GetComponent<EnemyBullet>();
        if (bullet != null)
        {
            int order = GetNextOrder(bData.sizeCategory);
            float speedLimit = Mathf.Max(sData.max_, overrideSpeed);
            // Setup時にチーム情報を渡す
            bullet.Setup(bData, spawnPos, overrideSpeed, sData.accuracy_, speedLimit, angle, aData.accuracy_, aData.max_, order, team,this);
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
    // 角度計算：起点(origin)からプレイヤーへの向きを出す
    private float CalculateAngle(ShotData data, Vector3 origin)
    {
        switch (data.angleType)
        {
            case ShotData.AngleType.AimAtPlayer:
                if (target != null)
                {
                    // ★ transform.position ではなく、引数の origin を使う
                    Vector2 dir = target.position - origin;
                    return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                }
                return data.fixedAngle;
            case ShotData.AngleType.Random:
                return Random.Range(1f, 360f);
            default:
                return data.fixedAngle;
        }
    }

    // 座標計算：起点(origin)から指定の半径分オフセットさせる
    private Vector3 GetOffsetPosition(float angle, float radius, Vector3 origin)
    {
        float rad = angle * Mathf.Deg2Rad;
        // ★ ここも transform.position ではなく origin を使う
        return origin + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * radius;
    }
    // --- ★ 復元した補助メソッド ---

    public float GetCurrentSpeedMultiplier()
    {
        float multiplier = 1f;
        for (int i = 0; i < 4; i++)
        {
            // 発射中またはチャージ中のスキルがあれば、その倍率を適用（最も低いものを優先）
            if ((isFiringBurst[i] || isCharging[i]) && attackPatterns[i] != null)
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