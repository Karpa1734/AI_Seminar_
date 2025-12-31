using UnityEngine;

public class LaunchDelayEffect : MonoBehaviour
{
    private SpriteRenderer sr;
    private int totalFrames;
    private int currentFrames;
    private float maxScale;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    public void Setup(Sprite sprite, int frames, int sortingOrder, float initialScale)
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();

        sr.sprite = sprite;
        sr.sortingLayerName = "Middle";
        sr.sortingOrder = sortingOrder + 1; // 弾より手前に表示

        totalFrames = frames;
        currentFrames = frames;
        maxScale = initialScale;

        transform.localScale = Vector3.one * maxScale;
        gameObject.SetActive(true);
    }

    void Update()
    {
        if (currentFrames > 0)
        {
            currentFrames--;
            // 1.0 から 0.0 へスケールダウン
            float t = (float)currentFrames / totalFrames;
            transform.localScale = Vector3.one * (maxScale * t);
        }
        else
        {
            // 2. 演出が終わったら自分を消す
            Destroy(gameObject);
        }
    }
}