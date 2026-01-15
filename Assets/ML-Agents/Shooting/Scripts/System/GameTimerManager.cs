using UnityEngine;
using Unity.MLAgents;

public class GameTimerManager : MonoBehaviour
{
    [Header("Timer Settings")]
    [SerializeField] private float initialTime = 30f;
    private float currentTime;

    [Header("References")]
    [SerializeField] private TopUITimerDisplay timerUI;
    [SerializeField] private DodgerAgent player1;
    [SerializeField] private DodgerAgent player2;

    void Start()
    {
        ResetTimer();
    }

    public void ResetTimer()
    {
        currentTime = initialTime;
    }

    void Update()
    {
        // ★ IsTraining ではなく Academy.Instance.IsCommunicatorOn を使用
        bool isTraining = Academy.Instance.IsCommunicatorOn;

        if (isTraining)
        {
            // --- 1. AI学習中の処理：ステップ数に同期 ---
            // MaxStepを「30秒」として扱い、進捗をUIに反映させる
            if (player1 != null && player1.MaxStep > 0)
            {
                // 現在のステップ数と最大ステップ数から比率を出す
                float ratio = 1f - ((float)player1.StepCount / player1.MaxStep);
                currentTime = ratio * initialTime;

                if (timerUI != null) timerUI.UpdateTimer(currentTime);

                // 学習中は DodgerAgent 内部の MaxStep 到達判定で 
                // EndEpisode が呼ばれるため、ここでは EndEpisode を呼ばない
            }
        }
        else
        {
            // --- 2. 通常プレイ時の処理：実時間に同期 ---
            if (currentTime > 0)
            {
                currentTime -= Time.deltaTime;
                if (timerUI != null) timerUI.UpdateTimer(currentTime);

                if (currentTime <= 0)
                {
                    TimeUp();
                }
            }
        }
    }

    private void TimeUp()
    {
        currentTime = 0;

        if (timerUI != null)
        {
            timerUI.UpdateTimer(0f);
        }

        Debug.Log("タイムアップ！ステップを終了します。");

        // 両方のエージェントに終了を通知（EndEpisode を呼ぶと OnEpisodeBegin が走る）
        if (player1 != null) player1.EndEpisode();
        if (player2 != null) player2.EndEpisode();

        ResetTimer();
    }
}