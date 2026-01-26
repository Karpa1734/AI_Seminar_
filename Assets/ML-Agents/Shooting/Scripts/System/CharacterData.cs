using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterData", menuName = "Danmaku/CharacterData")]
public class CharacterData : ScriptableObject
{
    [Header("Profile")]
    public string characterName;
    public Sprite characterSprite;

    [Header("Movement Stats")]
    public float normalSpeed = 5f;
    public float slowSpeed = 2f;

    [Header("Status")]
    public float maxHealth = 100f;

    [Header("Attack Settings")]
    [Tooltip("ZXCV‚ğ”p~‚µAg—p‚·‚é’eŠÛ‚Ì‘fŞî•ñ‚ğ‚±‚±‚ÉW–ñ‚µ‚Ü‚·")]
    // ˆÈ‘O‚Ì shotZ ‚ğ mainPattern ‚É•ÏX‚µ‚Ä®—
    public AttackPattern mainPattern;
}