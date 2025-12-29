using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterData", menuName = "Danmaku/CharacterData")]
public class CharacterData : ScriptableObject
{
    public string characterName;
    public Sprite characterSprite;

    [Header("Movement Stats")]
    public float normalSpeed = 5f;
    public float slowSpeed = 2f;

    [Header("Attack Patterns")]
    public AttackPattern shotZ;
    public AttackPattern shotX;
    public AttackPattern shotC;
    public AttackPattern shotV;
}

// š ‚±‚±‚É‚ ‚Á‚½ [System.Serializable] public class AttackPattern ... ‚Ííœ‚µ‚Ü‚µ‚½