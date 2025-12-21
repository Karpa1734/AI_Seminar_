using UnityEngine;

// --- データ定義クラス群 ---

[System.Serializable]
public class spanData
{
    public spanData() { }
    public spanData(float speed) { default_ = speed; }
    public spanData(float speed, float ac, float max) { default_ = speed; accuracy_ = ac; max_ = max; }
    public float default_;   // 初期速度
    public float accuracy_;  // 加速度
    public float max_;       // 最大速度
}

[System.Serializable]
public class MoveData
{
    [Tooltip("このフェーズで移動を行うか")]
    public bool shouldMove = false;

    [Tooltip("一度の移動で動く最短距離")]
    public float minMoveDistance = 1f;

    [Tooltip("一度の移動で動く最長距離")]
    public float maxMoveDistance = 3f;

    [Tooltip("移動処理を行う間隔 (フレーム)\n例: 120 = 2秒")]
    public int moveInterval = 120; // ★ int(フレーム)に変更

    [Tooltip("移動にかける時間 (フレーム)\n値が小さいほど速く動く")]
    public int moveDuration = 30;  // ★ int(フレーム)に変更

    [Tooltip("X軸の移動制限 (画面端から内側へのオフセット)")]
    public float xBoundOffset = 1f;
    [Tooltip("Y軸の移動制限 (画面端から内側へのオフセット)")]
    public float yBoundOffset = 1f;
}

[System.Serializable]
public class ShotData
{
    public enum PatternType { Single, NWay, AllDirections, None }
    public PatternType pattern = PatternType.Single;

    public enum AngleType { Fixed, AimAtPlayer, Random }
    public AngleType angleType = AngleType.AimAtPlayer;

    [Tooltip("弾の発射間隔 (フレーム)\n例: 5 = 高速連射, 60 = 1秒に1回")]
    public int interval = 30; // ★ int(フレーム)に変更

    [Header("Angle Settings")]
    [Tooltip("Fixed選択時の固定角度 (度)")]
    public float fixedAngle = 270f;

    [Header("N-Way Settings")]
    [Tooltip("N-Way選択時の弾数 (偶数+自機狙いで自機外しになります)")]
    public int nWayCount = 3;
    [Tooltip("N-Way選択時の全幅の広がり (度)")]
    public float nWaySpread = 30f;

    [Header("ALL-Direction Settings")]
    [Tooltip("全方位選択時の弾数 (偶数+自機狙いで自機外しになります)")]
    public int RoundCount = 36;

    [Header("Bullet Stats")]
    public spanData speedData = new spanData(3f, 0.5f, 8f);
    public spanData angleData = new spanData(0f, 0f, 0f);
}

[System.Serializable]
public class PhaseData
{
    public string phaseName = "Phase 1";
    [Tooltip("このフェーズが持続する時間 (フレーム)\n例: 300 = 5秒")]
    public int duration = 300; // ★ int(フレーム)に変更

    public ShotData shotData = new ShotData();
    public MoveData moveData = new MoveData();
}

// --- 本体クラス ---

public class BulletSpawner : MonoBehaviour
{
    [Header("Settings")]
    public GameObject bulletPrefab;
    public Transform player;

    [Header("Phase Management")]
    public PhaseData[] phases;

    // 内部状態変数 (フレームカウント用)
    private int currentPhaseIndex = 0;
    private int phaseFrameTimer = 0; // ★ intに変更
    private int shotFrameTimer = 0;  // ★ intに変更

    // 移動用変数
    private int moveIntervalTimer = 0;    // ★ 次の移動までの待機フレーム
    private int currentMoveProgress = 0;  // ★ 現在の移動の経過フレーム
    private int currentTotalMoveFrames = 0; // ★ 現在の移動にかかる総フレーム数
    private bool isMoving = false;         // ★ 移動中フラグ

    private Vector3 startPosition;  // 移動開始位置
    private Vector3 targetPosition; // 移動目標位置

    private void Awake()
    {
        // フレームレートを60に固定（推奨）
        Application.targetFrameRate = 60;
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        if (phases == null || phases.Length == 0)
        {
            Debug.LogError("Phases are not set up in Inspector!");
            enabled = false;
        }
        else
        {
            ResetToPhase(0);
            targetPosition = transform.position;
            startPosition = transform.position;
        }
    }

    // ★ Updateは毎フレーム呼ばれるので、ここでカウントアップする
    void Update()
    {
        if (phases == null || phases.Length == 0) return;

        PhaseData currentPhase = phases[currentPhaseIndex];
        ShotData currentShot = currentPhase.shotData;

        // ----------------------------------------------------
        // 1. フェーズ管理 (フレームカウント)
        // ----------------------------------------------------
        phaseFrameTimer++;
        if (phaseFrameTimer >= currentPhase.duration)
        {
            int nextIndex = currentPhaseIndex + 1;
            if (nextIndex >= phases.Length) nextIndex = 0;

            ResetToPhase(nextIndex);

            // フェーズ切り替え時は移動もリセットして現在地で止める
            targetPosition = transform.position;
            startPosition = transform.position;
            isMoving = false;

            currentPhase = phases[currentPhaseIndex];
            currentShot = currentPhase.shotData;
        }

        // ----------------------------------------------------
        // 2. 射撃処理 (フレームカウント)
        // ----------------------------------------------------
        shotFrameTimer++;
        if (shotFrameTimer >= currentShot.interval)
        {
            shotFrameTimer = 0;

            switch (currentShot.pattern)
            {
                case ShotData.PatternType.Single:
                    ShootSingle(currentShot);
                    break;
                case ShotData.PatternType.NWay:
                    ShootNWay(currentShot);
                    break;
                case ShotData.PatternType.AllDirections:
                    ShootAllDirections(currentShot);
                    break;
                case ShotData.PatternType.None:
                    break;
            }
        }

        // ----------------------------------------------------
        // 3. 移動処理 (フレームカウント)
        // ----------------------------------------------------
        HandleMovement(currentPhase.moveData);
    }

    private void ResetToPhase(int index)
    {
        currentPhaseIndex = index;
        phaseFrameTimer = 0;
        shotFrameTimer = 0;

        moveIntervalTimer = 0;
        currentMoveProgress = 0;
        isMoving = false;
    }

    // ========================================================================
    // ★ 移動ロジック (フレームベース)
    // ========================================================================
    void HandleMovement(MoveData moveData)
    {
        if (!moveData.shouldMove) return;

        // A. 移動中の処理
        if (isMoving)
        {
            currentMoveProgress++;

            // 進行度 (0.0 ～ 1.0)
            float t = (float)currentMoveProgress / (float)currentTotalMoveFrames;

            // 滑らかに移動
            transform.position = Vector3.Lerp(startPosition, targetPosition, t);

            // 移動完了判定
            if (currentMoveProgress >= currentTotalMoveFrames)
            {
                transform.position = targetPosition; // ズレ補正
                isMoving = false;
                moveIntervalTimer = 0; // 次の移動までのカウントダウン開始
            }
        }
        // B. 待機中の処理
        else
        {
            moveIntervalTimer++;
            if (moveIntervalTimer >= moveData.moveInterval)
            {
                SetNextTargetPosition(moveData);
            }
        }
    }

    void SetNextTargetPosition(MoveData moveData)
    {
        float distance = Random.Range(moveData.minMoveDistance, moveData.maxMoveDistance);
        Vector3 direction = Vector3.zero;

        if (player != null)
        {
            float deltaY = player.position.y - transform.position.y;
            direction.y = Mathf.Sign(deltaY);
            direction.x = Random.Range(-1f, 1f);
        }
        else
        {
            direction = Random.insideUnitCircle.normalized;
        }

        direction.Normalize();
        Vector3 newTarget = transform.position + direction * distance;

        // --- 画面制限 ---
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 minScreen = cam.ViewportToWorldPoint(new Vector3(0, 0, transform.position.z - cam.transform.position.z));
            Vector3 maxScreen = cam.ViewportToWorldPoint(new Vector3(1, 1, transform.position.z - cam.transform.position.z));

            float minX = minScreen.x + moveData.xBoundOffset;
            float maxX = maxScreen.x - moveData.xBoundOffset;
            float minY = minScreen.y + moveData.yBoundOffset;
            float maxY = maxScreen.y - moveData.yBoundOffset;

            newTarget.x = Mathf.Clamp(newTarget.x, minX, maxX);
            newTarget.y = Mathf.Clamp(newTarget.y, minY, maxY);
        }

        // 新しい移動のセットアップ
        startPosition = transform.position;
        targetPosition = newTarget;

        currentTotalMoveFrames = moveData.moveDuration;
        if (currentTotalMoveFrames < 1) currentTotalMoveFrames = 1; // 0除算防止

        currentMoveProgress = 0;
        isMoving = true;
    }

    // ========================================================================
    // ★ 射撃ロジック
    // ========================================================================

    private float CalculateBaseAngle(ShotData shotData)
    {
        float baseAngle = shotData.fixedAngle;

        switch (shotData.angleType)
        {
            case ShotData.AngleType.AimAtPlayer:
                if (player != null)
                {
                    Vector3 dir = (player.position - transform.position).normalized;
                    baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                }
                break;
            case ShotData.AngleType.Random:
                baseAngle = Random.Range(0f, 360f);
                break;
        }
        return baseAngle;
    }

    private void SpawnBullet(Vector3 position, float initSpeed, float spAcc, float spMax, float initAngle, float anAcc)
    {
        if (bulletPrefab == null) return;

        var b = Instantiate(bulletPrefab, position, Quaternion.identity);
        var bullet = b.GetComponent<EnemyBullet>();
        if (bullet != null)
        {
            bullet.Setup(position, initSpeed, spAcc, spMax, initAngle, anAcc);
        }
    }

    void ShootSingle(ShotData shotData)
    {
        float baseAngle = CalculateBaseAngle(shotData);
        SpawnBullet(transform.position, shotData.speedData.default_, shotData.speedData.accuracy_, shotData.speedData.max_, baseAngle, shotData.angleData.accuracy_);
    }

    void ShootNWay(ShotData shotData)
    {
        if (shotData.nWayCount <= 0) return;

        float baseAngle = CalculateBaseAngle(shotData);
        float startAngle = baseAngle - (shotData.nWaySpread / 2f);
        float anglePerBullet = (shotData.nWayCount > 1) ? shotData.nWaySpread / (shotData.nWayCount - 1) : 0;

        for (int i = 0; i < shotData.nWayCount; i++)
        {
            float currentAngle = startAngle + (anglePerBullet * i);
            SpawnBullet(transform.position, shotData.speedData.default_, shotData.speedData.accuracy_, shotData.speedData.max_, currentAngle, shotData.angleData.accuracy_);
        }
    }

    // 偶数＆自機狙いで自機外し対応済み
    void ShootAllDirections(ShotData shotData)
    {
        float baseAngle = CalculateBaseAngle(shotData);
        int numBullets = shotData.RoundCount;
        if (numBullets <= 0) return;

        float angleStep = 360f / numBullets;
        float startAngle = baseAngle;

        if (shotData.angleType == ShotData.AngleType.AimAtPlayer && numBullets % 2 == 0)
        {
            startAngle += angleStep / 2f;
        }

        for (int i = 0; i < numBullets; i++)
        {
            float currentAngle = startAngle + (angleStep * i);
            SpawnBullet(transform.position, shotData.speedData.default_, shotData.speedData.accuracy_, shotData.speedData.max_, currentAngle, shotData.angleData.accuracy_);
        }
    }
}