using UnityEngine;
using Unity.MLAgents;

public class BulletSpawner : MonoBehaviour
{
    public enum TeamSide { Player1, Player2 }
    public TeamSide myTeam;
    public CharacterData characterProfile;

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
        ExecuteShot(attackPatterns[index].shotData);
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
    private void ExecuteShot(ShotData data)
    {
        if (data == null || data.bulletType == null) return;
        switch (data.pattern)
        {
            case ShotData.PatternType.Single: ShootSingle(data); break;
            case ShotData.PatternType.NWay: ShootNWay(data); break;
            case ShotData.PatternType.AllDirections: ShootAllDirections(data); break;
        }
    }
    private void ShootSingle(ShotData data)
    {

        float angle = CalculateAngle(data);
        SpawnFromPool(data.bulletType, angle, data.speedData, data.angleAccData);


    }

    private void ShootNWay(ShotData data)
    {
        float baseAngle = CalculateAngle(data);
        float startAngle = baseAngle - (data.nWaySpread / 2f);
        float step = (data.nWayCount > 1) ? data.nWaySpread / (data.nWayCount - 1) : 0;

        for (int i = 0; i < data.nWayCount; i++)
        {
            SpawnFromPool(data.bulletType, startAngle + (step * i), data.speedData, data.angleAccData);
        }
    }

    private void ShootAllDirections(ShotData data)
    {
        float step = 360f / data.RoundCount;
        for (int i = 0; i < data.RoundCount; i++)
        {
            SpawnFromPool(data.bulletType, step * i, data.speedData, data.angleAccData);
        }
    }
    private void SpawnFromPool(BulletData bData, float angle, spanData sData, spanData aData)
    {
        if (BulletPool.Instance == null) return;
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

    private float CalculateAngle(ShotData data)
    {
        if (data.angleType == ShotData.AngleType.AimAtPlayer && target != null)
        {
            Vector2 dir = target.position - transform.position;
            return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }
        return data.fixedAngle;
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