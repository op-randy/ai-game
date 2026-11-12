using UnityEngine;

/// <summary>
/// A range target. Flashes red and rocks back when hit; knock-down targets fall over and stand up again.
/// The hinge rotates around its bottom edge.
/// </summary>
public class ShootingTarget : MonoBehaviour
{
    public Transform hinge;
    public Renderer face;
    public bool knockDown;
    public float resetTime = 1.6f;

    public int Hits { get; private set; }

    float flash, wobble, wobblePhase, fall, downTimer;
    bool down;
    MaterialPropertyBlock block;
    Color baseColor = Color.white;

    void Awake()
    {
        block = new MaterialPropertyBlock();
        if (face && face.sharedMaterial && face.sharedMaterial.HasProperty("_Color")) baseColor = face.sharedMaterial.color;
    }

    public void Hit(Vector3 point, Vector3 direction)
    {
        Hits++;
        flash = 1f;
        if (knockDown)
        {
            if (!down) { down = true; downTimer = resetTime; }
        }
        else
        {
            wobble = Mathf.Min(wobble + 0.6f, 1.2f);
            wobblePhase = 0f;
        }
    }

    void Update()
    {
        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 6f);
        if (block == null) block = new MaterialPropertyBlock();
        if (face)
        {
            face.GetPropertyBlock(block);
            block.SetColor("_Color", Color.Lerp(baseColor, new Color(1f, 0.25f, 0.2f), flash));
            face.SetPropertyBlock(block);
        }

        float angle;
        if (knockDown)
        {
            if (down)
            {
                fall = Mathf.MoveTowards(fall, 1f, Time.deltaTime * 6f);
                downTimer -= Time.deltaTime;
                if (downTimer <= 0f) down = false;
            }
            else fall = Mathf.MoveTowards(fall, 0f, Time.deltaTime * 2.5f);
            // ease out with a little bounce at the bottom
            angle = 82f * (1f - Mathf.Pow(1f - fall, 3f));
        }
        else
        {
            wobblePhase += Time.deltaTime * 18f;
            wobble = Mathf.MoveTowards(wobble, 0f, Time.deltaTime * 1.8f);
            angle = Mathf.Sin(wobblePhase) * 9f * wobble;
        }
        if (hinge) hinge.localRotation = Quaternion.Euler(angle, 0f, 0f);
    }
}
