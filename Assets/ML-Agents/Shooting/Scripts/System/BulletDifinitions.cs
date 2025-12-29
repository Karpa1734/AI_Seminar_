using UnityEngine;

// --- 数値の変化を扱うクラス ---
[System.Serializable]
public class spanData
{
    public float default_;
    public float accuracy_;
    public float max_;

    public spanData() { }
    public spanData(float val, float acc, float max)
    {
        default_ = val;
        accuracy_ = acc;
        max_ = max;
    }
}

// --- 射撃パターン（見た目＋挙動） ---
[System.Serializable]
public class ShotData
{
    public enum PatternType { Single, NWay, AllDirections, None }
    public enum AngleType { Fixed, AimAtPlayer, Random }

    [Header("Visual & Collision")]
    public BulletData bulletType;

    [Header("Pattern Settings")]
    public PatternType pattern = PatternType.Single;
    public AngleType angleType = AngleType.AimAtPlayer;
    public float fixedAngle = 270f;
    public int nWayCount = 3;
    public float nWaySpread = 30f;
    public int RoundCount = 36;

    [Header("Movement Settings")]
    public spanData speedData = new spanData(3f, 0.5f, 8f);
    public spanData angleAccData = new spanData(0f, 0f, 0f);
}

// --- 技のスロット（ショットデータ＋リキャスト時間） ---
// ★クラスとしての定義はここだけにする！
[System.Serializable]
public class AttackPattern
{
    public ShotData shotData;
    public float recastTime;    // 全発射終了（または中断）後のリキャスト（秒）

    [Header("Burst Settings")]
    public int burstCount = 1;      // 最大何連射するか
    public float burstInterval = 0.1f; // 連射の間隔（秒）
}

[System.Serializable]
public class MoveData
{
    public bool shouldMove = false;
    public float minMoveDistance = 1f;
    public float maxMoveDistance = 3f;
    public int moveInterval = 120;
    public int moveDuration = 30;
    public float xBoundOffset = 1f;
    public float yBoundOffset = 1f;
}

[System.Serializable]
public class PhaseData
{
    public string phaseName = "Phase 1";
    public int duration = 300;
    public ShotData shotData = new ShotData();
    public MoveData moveData = new MoveData();
}