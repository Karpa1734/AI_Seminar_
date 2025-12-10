using UnityEngine;

[System.Serializable]
public class spanData
{
    public float default_;
    public float accuracy_;
    public float max_;

    public spanData() { }

    public spanData(float speed)
    {
        default_ = speed;
    }

    public spanData(float speed, float ac, float max)
    {
        default_ = speed;
        accuracy_ = ac;
        max_ = max;
    }
}

[System.Serializable]
public class spanData2
{
    public float? default_;
    public float accuracy_;
    public float max_;

    public spanData2() { }

    public spanData2(float? speed)
    {
        default_ = speed;
    }

    public spanData2(float? speed, float ac, float max)
    {
        default_ = speed;
        accuracy_ = ac;
        max_ = max;
    }
}
