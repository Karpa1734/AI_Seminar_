using UnityEngine;

// --- 予兆エフェクトの色定義 ---
public enum DelayColor { RED, ORANGE, YELLOW, GREEN, AQUA, BLUE, PURPLE, WHITE }

[System.Serializable]
public class spanData
{
    public float default_;
    public float accuracy_;
    public float max_;
    public spanData() { }
    public spanData(float val, float acc, float max) { default_ = val; accuracy_ = acc; max_ = max; }
}

[System.Serializable]
public class NWayExpandSettings
{
    public int countAdd = 0;
    public float spreadAdd = 0f;
}

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
    public int speedCount = 1; // 弾数（速度のレイヤー数）
    public float speedMax = 8f; // 最大速度（一番外側の弾）

    [Header("NWay Expansion")]
    public NWayExpandSettings nWayExpand;

    [Header("Movement Settings")]
    public spanData speedData = new spanData(3f, 0.5f, 8f);
    public spanData angleAccData = new spanData(0f, 0f, 0f);

    [Header("Delay Settings")]
    public int launchDelay = 0;

    [Header("Spawn Position Settings")]
    public float spawnRadius = 0f;
}

// 連打(Manual)は削除し、Holdのみに整理
public enum FireType { Hold }

[System.Serializable]
public class AttackPattern
{
    public ShotData shotData;
    public float recastTime;

    [Header("Burst Settings")]
    public int burstCount = 1;
    public float burstInterval = 0.1f;

    [Header("Input & Speed Settings")]
    public FireType fireType = FireType.Hold;
    [Range(0f, 1f)]
    [Tooltip("発射中の移動速度倍率 (1.0で等速、0.5で50%減速、0で停止)")]
    public float firingSpeedMultiplier = 1.0f; // ★追加
}