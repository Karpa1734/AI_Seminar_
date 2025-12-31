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
    public Sprite[] delaySprites = new Sprite[8]; // ★ 追加：8枚のスプライトを登録

    private AttackPattern[] attackPatterns = new AttackPattern[4];
    private float[] recastTimers = new float[4];
    private Transform target;

    // バースト制御用
    private int[] burstRemain = new int[4];
    private float[] burstIntervalTimers = new float[4];
    private bool[] isFiringBurst = new bool[4];
    private bool[] isInputHeld = new bool[4]; // 現在ボタンが押されているか

    // Sorting Order ループ用カウンタ
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
                // ここでの !isInputHeld[i] チェックは UpdateInputState 側に任せるか
                // あるいはそのまま維持（UpdateInputStateで更新された値が使われる）
                if (!isInputHeld[i])
                {
                    StopBurstAndStartRecast(i);
                    continue;
                }

                burstIntervalTimers[i] -= Time.deltaTime;
                if (burstIntervalTimers[i] <= 0)
                {
                    Fire(i);
                }
            }

        }
    }
    // Update内の「isInputHeld[i] = false;」を削除し、以下のメソッドを追加
    public void UpdateInputState(int attackAction)
    {
        for (int i = 0; i < 4; i++)
        {
            // 現在のアクションがこのスロット（Z,X,C,V）に対応するか
            bool currentlyPressed = (attackAction == i + 1);
            isInputHeld[i] = currentlyPressed;

            // 1. ボタンが離された（または別ボタンに移った）際の中断処理
            if (!isInputHeld[i] && isFiringBurst[i])
            {
                StopBurstAndStartRecast(i);
            }
            // 2. ボタンが押されており、まだ連射が始まっておらず、リキャスト中でないなら開始
            else if (isInputHeld[i] && !isFiringBurst[i] && recastTimers[i] <= 0)
            {
                if (attackPatterns[i] != null)
                {
                    StartBurst(i);
                }
            }
        }
    }
    // Agent や Heuristic から毎フレーム呼ばれる
    public void SetInput(int index)
    {
        if (index < 0 || index >= 4) return;
        isInputHeld[index] = true;

        // リキャスト中でなく、まだ連射が始まっていないなら開始
        if (recastTimers[index] <= 0 && !isFiringBurst[index] && attackPatterns[index] != null)
        {
            StartBurst(index);
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
        // 現在のステップ（0, 1, 2...）を計算
        int currentStep = attackPatterns[index].burstCount - burstRemain[index];

        // stepを引数として渡す
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
        recastTimers[index] = attackPatterns[index].recastTime; // ここでリキャスト開始
    }

    // --- 以下、既存の ExecuteShot, SpawnFromPool, GetNextOrder 等はそのまま ---
    private void ExecuteShot(ShotData data, int step)
    {
        if (data == null || data.bulletType == null) return;

        switch (data.pattern)
        {
            case ShotData.PatternType.Single:
                ShootSingle(data); // stepは無視される
                break;

            case ShotData.PatternType.NWay:
                // stepを考慮したNWay発射
                ShootExpandingNWay(data, step);
                break;

            case ShotData.PatternType.AllDirections:
                ShootAllDirections(data); // stepは無視される
                break;
        }
    }

    private void ShootExpandingNWay(ShotData data, int step)
    {
        int actualCount = data.nWayCount;
        float actualSpread = data.nWaySpread;

        if (data.nWayExpand != null)
        {
            actualCount += data.nWayExpand.countAdd * step;
            actualSpread += data.nWayExpand.spreadAdd * step;
        }

        float baseAngle = CalculateAngle(data);
        float startAngle = baseAngle - (actualSpread / 2f);
        float angleStep = (actualCount > 1) ? actualSpread / (actualCount - 1) : 0;

        for (int i = 0; i < actualCount; i++)
        {
            // ★第5引数に data.launchDelay を追加
            SpawnFromPool(data.bulletType, startAngle + (angleStep * i), data.speedData, data.angleAccData, data.launchDelay);
        }
    }

    private void ShootSingle(ShotData data)
    {
        float angle = CalculateAngle(data);
        // ★第5引数に data.launchDelay を追加
        SpawnFromPool(data.bulletType, angle, data.speedData, data.angleAccData, data.launchDelay);
    }

    private void ShootAllDirections(ShotData data)
    {
        float baseAngle = CalculateAngle(data);
        float step = 360f / data.RoundCount;

        // 【偶数弾の自機外しロジック】
        // 弾数が偶数の場合、正面に弾が来ないように角度をステップの半分だけずらす
        float offset = (data.RoundCount % 2 == 0) ? step / 2f : 0f;

        for (int i = 0; i < data.RoundCount; i++)
        {
            // baseAngle（基準）に現在のステップとオフセットを足す
            SpawnFromPool(data.bulletType, baseAngle + (step * i) + offset, data.speedData, data.angleAccData, data.launchDelay);
        }
    }

    private void ShootNWay(ShotData data)
    {
        float baseAngle = CalculateAngle(data);
        float startAngle = baseAngle - (data.nWaySpread / 2f);
        float step = (data.nWayCount > 1) ? data.nWaySpread / (data.nWayCount - 1) : 0;

        for (int i = 0; i < data.nWayCount; i++)
        {
            // ★第5引数に data.launchDelay を追加
            SpawnFromPool(data.bulletType, startAngle + (step * i), data.speedData, data.angleAccData, data.launchDelay);
        }
    }

    // BulletSpawner.cs の SpawnFromPool 内にログを追加
    private void SpawnFromPool(BulletData bData, float angle, spanData sData, spanData aData, int delayFrames)
    {
        if (BulletPool.Instance == null) return;

        // ↓ この一行を追加して、コンソール（Console）を確認してください
        Debug.Log($"発射を試行中: {bData.bulletName}, 遅延フレーム: {delayFrames}");

        if (delayFrames <= 0)
        {
            ExecuteActualSpawn(bData, angle, sData, aData);
        }
        else
        {
            StartCoroutine(DelayedSpawnRoutine(bData, angle, sData, aData, delayFrames));
        }
    }
    // BulletSpawner.cs の DelayedSpawnRoutine 内を修正

    private IEnumerator DelayedSpawnRoutine(BulletData bData, float angle, spanData sData, spanData aData, int delayFrames)
    {
        if (delayEffectPrefab != null)
        {
            GameObject fxObj = Instantiate(delayEffectPrefab, transform.position, Quaternion.identity, transform);
            var fx = fxObj.GetComponent<LaunchDelayEffect>();

            if (fx != null)
            {
                // 配列の範囲チェックを追加してクラッシュを防ぐ
                int colorIndex = (int)bData.delayColor;
                Sprite targetSprite = (delaySprites != null && colorIndex < delaySprites.Length)
                                      ? delaySprites[colorIndex] : null;

                if (targetSprite == null)
                {
                    Debug.LogWarning($"{bData.delayColor} のスプライトが登録されていません！");
                }

                float baseScale = (bData.sizeCategory == BulletData.SizeCategory.Small) ? 0.6f :
                                  (bData.sizeCategory == BulletData.SizeCategory.Large) ? 1.8f : 1.0f;
                float finalMaxScale = baseScale * bData.localScale.x;

                int order = GetNextOrder(bData.sizeCategory);
                fx.Setup(targetSprite, delayFrames, order, finalMaxScale);
            }
        }

        // 待機処理
        for (int i = 0; i < delayFrames; i++) yield return null;
        ExecuteActualSpawn(bData, angle, sData, aData);
    }

    private void ExecuteActualSpawn(BulletData bData, float angle, spanData sData, spanData aData)
    {
        GameObject b = BulletPool.Instance.Get();
        b.transform.position = transform.position;
        b.layer = LayerMask.NameToLayer(myTeam.ToString() + "_Bullet");

        var bullet = b.GetComponent<EnemyBullet>();
        if (bullet != null)
        {
            int order = GetNextOrder(bData.sizeCategory);
            bullet.Setup(bData, transform.position, sData.default_, sData.accuracy_, sData.max_, angle, aData.accuracy_, aData.max_, order);
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

    // BulletSpawner.cs の CalculateAngle メソッドを更新

    private float CalculateAngle(ShotData data)
    {
        switch (data.angleType)
        {
            case ShotData.AngleType.AimAtPlayer:
                if (target != null)
                {
                    // ターゲット（自機）への方向ベクトルを計算し、角度に変換
                    Vector2 dir = target.position - transform.position;
                    return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                }
                return data.fixedAngle; // ターゲット不在時のフォールバック

            case ShotData.AngleType.Random:
                // 1.0f から 360.0f までの乱数を返す
                return Random.Range(1f, 360f);

            case ShotData.AngleType.Fixed:
            default:
                // インスペクターで設定した固定角度を返す
                return data.fixedAngle;
        }
    }

    public float GetRecastProgress(int index)
    {
        if (index < 0 || index >= 4 || attackPatterns[index] == null || attackPatterns[index].recastTime <= 0) return 0f;
        return Mathf.Clamp01(recastTimers[index] / attackPatterns[index].recastTime);
    }
    private void FindTarget()
    {
        // 自分の陣営がPlayer1なら敵のタグは"Player2"、逆なら"Player1"
        string enemyTag = (myTeam == TeamSide.Player1) ? "Player2" : "Player1";

        // シーン内から該当するタグを持つオブジェクトを探す
        GameObject targetObj = GameObject.FindGameObjectWithTag(enemyTag);

        if (targetObj != null)
        {
            target = targetObj.transform;
        }
        else
        {
            // 開発中にミスに気づけるよう警告を出す
            Debug.LogWarning($"{enemyTag} タグの付いたオブジェクトが見つかりません。ヒエラルキー上の対象オブジェクトにタグを設定してください。");
        }
    }
}