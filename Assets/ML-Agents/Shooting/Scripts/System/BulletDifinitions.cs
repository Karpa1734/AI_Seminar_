using UnityEngine;

// --- 予兆エフェクトや弾の色味を識別する定義（ビジュアル用） ---
public enum DelayColor { RED, ORANGE, YELLOW, GREEN, AQUA, BLUE, PURPLE, WHITE }

[System.Serializable]
public class ShotData
{
    [Header("Bullet Material")]
    [Tooltip("使用する弾のデータ（見た目や当たり判定）")]
    public BulletData bulletType;

    // AIが0から構築するため、以前の PatternType や AngleType などの
    // 複雑な固定設定は不要となりました。
    // 必要に応じて、AIが参照しない「デフォルト値」をここに残すことも可能です。
}

[System.Serializable]
public class AttackPattern
{
    [Header("Shot Settings")]
    public ShotData shotData;

    [Header("Basic Properties")]
    [Tooltip("発射中の移動速度倍率 (1.0で等速、0.5で50%減速)")]
    [Range(0f, 1f)]
    public float firingSpeedMultiplier = 1.0f;

    // AI側（BossAgent）でクールタイムを管理するため recastTime は削除可能ですが、
    // 自機（Player）の連射設定などのために残しておくこともできます。
    [Tooltip("連射間隔（秒）")]
    public float recastTime = 0.1f;

    [Header("Visual")]
    public Sprite skillIcon;
}