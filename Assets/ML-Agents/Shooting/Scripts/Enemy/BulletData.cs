using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class BulletChangeStep
{
    [Tooltip("発射からの経過フレーム数でトリガー（1 = 次のフレーム）")]
    public int triggerFrame; // float から int に変更

    [Header("Visual & Collision")]
    public Sprite newSprite;
    public float newColliderRadius = -1f; // -1なら変更なし
    public Vector3 newScale = Vector3.zero; // Zeroなら変更なし

    [Header("Trajectory (Absolute)")]
    public bool changeTrajectory = false;
    public float newSpeed;
    public float newSpeedAcc;
    public float newAngleOffset; // 現在の角度に対するオフセット、または絶対角
    public bool isAbsoluteAngle = false;
}

[CreateAssetMenu(fileName = "NewBulletData", menuName = "Danmaku/BulletData")]
public class BulletData : ScriptableObject
{
    public enum SizeCategory { Large, Middle, Small }
    public string bulletName;
    public Sprite bulletSprite;
    public SizeCategory sizeCategory;

    [Header("Effect Settings")]
    public DelayColor delayColor;

    [Header("Collision Settings")]
    public float colliderRadius = 0.1f;
    public Vector2 colliderOffset = Vector2.zero;

    [Header("Visual Settings")]
    public Vector3 localScale = Vector3.one;

    [Header("Phase Changes")]
    [Tooltip("時間経過で変化するステップのリスト（実行順に並べること）")]
    public List<BulletChangeStep> changeSteps = new List<BulletChangeStep>();
}