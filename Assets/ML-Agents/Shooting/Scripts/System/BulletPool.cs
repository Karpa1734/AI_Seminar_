using UnityEngine;
using System.Collections.Generic;

public class BulletPool : MonoBehaviour
{
    public static BulletPool Instance;

    [Header("Pool Settings")]
    public GameObject bulletPrefab;
    public int initialPoolSize = 500; // 最初に生成しておく数

    private Queue<GameObject> pool = new Queue<GameObject>();

    private void Awake()
    {
        Instance = this;

        // 起動時にあらかじめ弾を生成して非表示にしておく
        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject obj = Instantiate(bulletPrefab);
            obj.transform.SetParent(this.transform);
            obj.SetActive(false);
            pool.Enqueue(obj);
        }
    }

    // プールから弾を取得する
    public GameObject Get()
    {
        if (pool.Count > 0)
        {
            GameObject obj = pool.Dequeue();
            obj.SetActive(true);
            return obj;
        }
        else
        {
            // プールが空になった場合は新しく生成（保険）
            GameObject obj = Instantiate(bulletPrefab);
            obj.transform.SetParent(this.transform);
            return obj;
        }
    }

    // 使い終わった弾をプールに戻す
    public void ReturnToPool(GameObject obj)
    {
        obj.SetActive(false);
        pool.Enqueue(obj);
    }

    public void ReturnAllBullets()
    {
        // シーン内のすべての弾を探して戻す
        var activeBullets = GameObject.FindGameObjectsWithTag("Enemy_Bullet");
        foreach (var b in activeBullets)
        {
            if (b.activeSelf)
            {
                ReturnToPool(b);
            }
        }
    }
}