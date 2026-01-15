using UnityEngine;
using UnityEngine.UI;

public class SkillUIManager : MonoBehaviour
{
    [SerializeField] private Image[] skillIconImages = new Image[4]; // UI上のImageコンポーネント

    public void SetupIcons(AttackPattern[] patterns)
    {
        for (int i = 0; i < 4; i++)
        {
            if (patterns[i] != null && patterns[i].skillIcon != null)
            {
                skillIconImages[i].sprite = patterns[i].skillIcon;
                skillIconImages[i].enabled = true;
            }
            else
            {
                skillIconImages[i].enabled = false; // アイコンがない場合は非表示
            }
        }
    }
}