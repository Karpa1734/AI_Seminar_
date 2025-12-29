using UnityEngine;

[CreateAssetMenu(fileName = "NewBulletData", menuName = "Danmaku/BulletData")]
public class BulletData : ScriptableObject
{
    // サイズ区分の定義
    public enum SizeCategory { Large, Middle, Small }

    public string bulletName;
    public Sprite bulletSprite;
    public SizeCategory sizeCategory; // インスペクターで Large/Middle/Small を選択

    [Header("Collision Settings")]
    public float colliderRadius = 0.1f;
    public Vector2 colliderOffset = Vector2.zero;

    [Header("Visual Settings")]
    public Vector3 localScale = Vector3.one;
}