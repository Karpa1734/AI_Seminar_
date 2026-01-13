using UnityEngine;

public class GameManager : MonoBehaviour
{
    void Awake()
    {
        // 垂直同期をオフにする（0:オフ, 1:オン）
        QualitySettings.vSyncCount = 0;

        // FPSを60に固定
        Application.targetFrameRate = 60;
    }
}
