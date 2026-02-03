using System.IO;
using System.Text;
using UnityEngine;

public class ResearchLogger : MonoBehaviour
{
    private string filePath;
    private float logInterval = 0.2f;
    private float timer = 0f;

    [Header("References")]
    public BossAgent boss;
    public PlayerAgent player;

    void Start()
    {
        // Assetsフォルダ直下に保存
        filePath = Path.Combine(Application.dataPath, "ResearchData_Log.csv");

        // 初回のみヘッダーを作成
        if (!File.Exists(filePath))
        {
            string header = "Timestamp,SkillLevel,ChosenTactic,RemoteX,RemoteY,PlayerHP,BossHP,WayMultiplier,SpeedMultiplier\n";
            File.WriteAllText(filePath, header, Encoding.UTF8);
        }
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= logInterval)
        {
            LogData();
            timer = 0f;
        }
    }

    void LogData()
    {
        // 参照が外れている場合は記録しない
        if (boss == null || player == null || GlobalSkillManager.Instance == null) return;

        float skill = GlobalSkillManager.Instance.currentSkillLevel;

        // ★ BossAgent から実際の値を取得するように修正
        int tactic = boss.LastChosenTactic;
        float remoteX = boss.LastRemoteOffset.x;
        float remoteY = boss.LastRemoteOffset.y;

        // 文字列の組み立て
        string line = string.Format("{0:F2},{1:F2},{2},{3:F2},{4:F2},{5:F2},{6:F2},{7:F2},{8:F2}\n",
            Time.time,
            skill,
            tactic,
            remoteX,
            remoteY,
            player.CurrentHealthRatio,
            boss.CurrentHealthRatio,
            1.0f + skill,            // 難易度による弾数倍率
            1.0f + skill * 0.5f      // 難易度による速度倍率
        );

        // ファイルに追記
        File.AppendAllText(filePath, line, Encoding.UTF8);
    }
}