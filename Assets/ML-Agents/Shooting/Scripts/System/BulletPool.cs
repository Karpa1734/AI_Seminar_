using UnityEngine;
using System.Collections.Generic;

public class BulletPool : MonoBehaviour
{
    public static BulletPool Instance;

    [Header("Pool Settings")]
    public GameObject bulletPrefab;
    public int initialPoolSize = 500;

    private Queue<GameObject> pool = new Queue<GameObject>();
    // 全ての弾を管理するためのリストを追加
    private List<GameObject> allBullets = new List<GameObject>();

    private void Awake()
    {
        Instance = this;

        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewBullet();
        }
    }

    // 弾を新しく生成してリストとプールに入れる
    private GameObject CreateNewBullet()
    {
        GameObject obj = Instantiate(bulletPrefab);
        obj.transform.SetParent(this.transform);
        obj.SetActive(false);
        pool.Enqueue(obj);
        allBullets.Add(obj); // 管理リストに追加
        return obj;
    }

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
            // 足りなくなった場合もリストに追加して管理する
            return CreateNewBullet();
        }
    }

    public void ReturnToPool(GameObject obj)
    {
        obj.SetActive(false);
        pool.Enqueue(obj);
    }

    // ★ ML-Agents のリセット時に呼ぶ一括非アクティブ化メソッド
    public void DeactivateAll()
    {
        foreach (var b in allBullets)
        {
            if (b.activeSelf)
            {
                ReturnToPool(b);
            }
        }
    }
}