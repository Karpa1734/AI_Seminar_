using UnityEngine;
using TMPro;

public class BattleDebugger : MonoBehaviour
{
    public TextMeshProUGUI debugText;
    public PlayerAgent player;
    public BossAgent boss;

    private float updateInterval = 0.5f; // 0.5秒おき
    private float nextUpdateTime = 0f;

    void Update()
    {
        // 倍速学習中でも、現実世界の時間で 0.5秒経過するまで待つ
        if (Time.unscaledTime < nextUpdateTime) return;

        nextUpdateTime = Time.unscaledTime + updateInterval;

        if (debugText == null || GlobalSkillManager.Instance == null) return;

        float skill = GlobalSkillManager.Instance.currentSkillLevel;

        string info = "<color=yellow>[EPISODE INFO]</color>\n";
        info += $"Global Skill: <color=red>{skill:P0}</color>\n";

        if (boss != null)
        {
            info += $"\n<color=orange>[BOSS LIMITS]</color>\n";
            // DebugMaxWayLimit などが更新されているか確認
            info += $"Max Way: {boss.DebugMaxWayLimit}\n";
            info += $"Speed Mul: x{boss.DebugSpeedMultiplier:F2}\n";
            // 変化が見えない場合は、BossのEnergy消費が少なすぎる可能性があります
            info += $"Energy: {boss.CurrentEnergyRatio:P0}\n";
        }

        if (player != null)
        {
            info += $"\n<color=green>[PLAYER STATUS]</color>\n";
            info += $"Risk: {player.GetNormalizedRisk():F2}\n";
            info += $"Heat: {player.CurrentHeat:F2}\n";
            info += $"Graze: {player.GrazeCount}\n";
        }

        debugText.text = info;
    }
}