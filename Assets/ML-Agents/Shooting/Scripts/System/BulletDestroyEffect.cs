using UnityEngine;

public class BulletDestroyEffect : MonoBehaviour
{
    public float t = 0.5f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Destroy(gameObject,t);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
