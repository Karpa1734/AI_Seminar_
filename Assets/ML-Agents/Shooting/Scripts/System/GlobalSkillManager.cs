using UnityEngine;

public class GlobalSkillManager : MonoBehaviour
{
    public static GlobalSkillManager Instance;

    [Header("Skill Parameters")]
    public float currentSkillLevel = 0.5f; // 0.0 (初心者) ～ 1.0 (プロ)
    public float skillAdjustRate = 0.02f; // ★頻繁に更新するため、値を 0.1 -> 0.02 程度に下げる

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    // 技量の更新ロジック
    public void UpdateSkill(float performance)
    {
        // 変化が分かりにくい場合は、skillAdjustRate を 0.3 くらいまで上げる
        // Lerp が強すぎると初期値（0.5）からなかなか離れません
        currentSkillLevel = Mathf.Lerp(currentSkillLevel, performance, skillAdjustRate);

        // 数値が極端に振れるか確認するために、ログを出す
        Debug.Log($"<color=magenta>[SKILL UPDATE]</color> Performance: {performance:F2} -> New Skill: {currentSkillLevel:F2}");
    }
}