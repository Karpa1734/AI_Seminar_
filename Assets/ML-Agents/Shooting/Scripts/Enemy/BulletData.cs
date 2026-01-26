using UnityEngine;
using System.Collections.Generic;

// 弾の変化ステップを定義（EnemyBulletのUpdateで処理可能）
[System.Serializable]
public class BulletChangeStep
{
    [Tooltip("発射からの経過フレーム数でトリガー")]
    public int triggerFrame;

    [Header("Visual & Collision")]
    public Sprite newSprite;
    public float newColliderRadius = -1f;
    public Vector3 newScale = Vector3.zero;

    [Header("Trajectory Change")]
    public bool changeTrajectory = false;
    public float newSpeed;
    public float newSpeedAcc;
    public float newAngleOffset;
    public bool isAbsoluteAngle = false;
}

public enum BulletStartupType { None, RotateX, RotateY, RotateZ, MoveX, MoveY, Scale }

[System.Serializable]
public class BulletStartupEffect
{
    public BulletStartupType type;
    public float startValue;
    public float endValue;
    public int durationFrames;
}

public enum ColliderShape { Circle, Capsule }

[CreateAssetMenu(fileName = "NewBulletData", menuName = "Danmaku/BulletData")]
public class BulletData : ScriptableObject
{
    public enum SizeCategory { Large, Middle, Small }

    [Header("Basic Info")]
    public string bulletName;
    public Sprite bulletSprite;
    public GameObject deathEffectPrefab;
    public SizeCategory sizeCategory;

    [Header("Combat Settings")]
    public int damage = 10; // ボスのHP減少計算に使用

    [Header("Visual & Effect Settings")]
    public bool isAdditive; // 加算合成（光る演出）の有無
    public Vector3 localScale = Vector3.one;

    [Header("Collision Settings")]
    public ColliderShape colliderShape = ColliderShape.Circle;
    public float colliderRadius = 0.15f;
    public float capsuleHeight = 1.0f; // 槍型などの長い弾用
    public Vector2 colliderOffset = Vector2.zero;

    [Header("Pre-fire Animation")]
    public BulletStartupEffect startupEffect = new BulletStartupEffect();

    [Header("Sub-Shot Settings")]
    public bool spawnSubBullets = false;
    public ShotData subShotData;
    public int spawnIntervalFrames = 10;

    [Header("Life Settings")]
    public float bulletLifespan = 8.0f; // 画面外に出なくても一定時間で消去する（メモリ対策）

    [Header("Phase Changes")]
    [Tooltip("時間経過で変化するステップのリスト")]
    public List<BulletChangeStep> changeSteps = new List<BulletChangeStep>();
}