using UnityEngine;
using Unity.MLAgents;

public class GameTimerManager : MonoBehaviour
{
    [Header("Timer Settings")]
    [SerializeField] private float stageTimeLimit = 30f; // 1つの弾幕フェーズの制限時間
    private float currentTime;

    [Header("References")]
    [SerializeField] private TopUITimerDisplay timerUI;
    [SerializeField] private PlayerAgent playerAgent;
    [SerializeField] private BossAgent bossAgent;
    // 現在の残り時間の割合 (1.0 ～ 0.0) を返すプロパティ
    public float CurrentTimeRatio => Mathf.Clamp01(currentTime / stageTimeLimit);
    void Start()
    {
        ResetTimer();
    }

    public void ResetTimer()
    {
        currentTime = stageTimeLimit;
    }

    void Update()
    {
        // AI学習中かどうかの判定
        bool isTraining = Academy.Instance.IsCommunicatorOn;

        if (isTraining)
        {
            // --- 1. 学習中：エージェントのステップ進捗に同期 ---
            // PlayerAgentのMaxStepをフェーズの時間制限として UI に反映
            if (playerAgent != null && playerAgent.MaxStep > 0)
            {
                float ratio = 1f - ((float)playerAgent.StepCount / playerAgent.MaxStep);
                currentTime = ratio * stageTimeLimit;

                if (timerUI != null) timerUI.UpdateTimer(currentTime);
            }
        }
        else
        {
            // --- 2. 通常プレイ：実時間でのカウントダウン ---
            if (currentTime > 0)
            {
                currentTime -= Time.deltaTime;
                if (timerUI != null) timerUI.UpdateTimer(currentTime);

                if (currentTime <= 0)
                {
                    PhaseTimeUp();
                }
            }
        }
    }

    private void PhaseTimeUp()
    {
        currentTime = 0;
        if (timerUI != null) timerUI.UpdateTimer(0f);

        Debug.Log("弾幕フェーズ終了！次のフェーズまたはリセットへ移行します。");

        // タイムアップ時は、プレイヤーが生き残ったとしてボーナスを判定する場合が多い
        if (playerAgent != null)
        {
            playerAgent.SetReward(2.0f); // 生存クリアボーナス
            playerAgent.EndEpisode();
        }

        // ボス側もエピソードを終了させてリセット
        if (bossAgent != null) bossAgent.EndEpisode();

        ResetTimer();
    }
}