using UnityEngine;
using Unity.MLAgents;

public class GameTimerManager : MonoBehaviour
{
    [Header("Timer Settings")]
    [SerializeField] private float initialTime = 30f;
    private float currentTime;

    [Header("References")]
    [SerializeField] private TopUITimerDisplay timerUI; // 前に作ったタイマーUI
    [SerializeField] private DodgerAgent player1;
    [SerializeField] private DodgerAgent player2;

    void Start()
    {
        ResetTimer();
    }

    // エピソード開始時（DodgerAgentのOnEpisodeBegin）から呼んでもOK
    public void ResetTimer()
    {
        currentTime = initialTime;
    }

    void Update()
    {
        if (currentTime > 0)
        {
            currentTime -= Time.deltaTime;

            if (timerUI != null)
            {
                // 文字列ではなく、数値をそのまま渡すように変更
                timerUI.UpdateTimer(currentTime);
            }

            if (currentTime <= 0)
            {
                TimeUp();
            }
        }
    }

    private void TimeUp()
    {
        currentTime = 0;

        // 文字列 ("0") ではなく、数値 (0) を渡すように修正
        if (timerUI != null)
        {
            timerUI.UpdateTimer(0f);
        }

        Debug.Log("タイムアップ！ステップを終了します。");

        // 両方のエージェントに終了を通知（EndEpisode を呼ぶと OnEpisodeBegin が走る）
        if (player1 != null) player1.EndEpisode();
        if (player2 != null) player2.EndEpisode();

        // 自身（タイマー）も初期値にリセット
        ResetTimer();
    }
}