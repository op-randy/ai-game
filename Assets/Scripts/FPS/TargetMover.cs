using UnityEngine;

/// <summary>Slides a target back and forth along its local X axis.</summary>
public class TargetMover : MonoBehaviour
{
    public float distance = 5f;
    public float speed = 2.2f;

    Vector3 start;

    void Start() => start = transform.localPosition;

    void Update()
    {
        float t = Mathf.PingPong(Time.time * speed / distance, 1f);
        t = t * t * (3f - 2f * t);
        transform.localPosition = start + Vector3.right * Mathf.Lerp(-distance / 2f, distance / 2f, t);
    }
}
