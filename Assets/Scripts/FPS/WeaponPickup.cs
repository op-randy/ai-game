using UnityEngine;

/// <summary>A weapon lying in the world. Look at it and press E to pick it up. Plays a sound when it lands after a throw.</summary>
public class WeaponPickup : MonoBehaviour
{
    public string displayName = "Assault Rifle";
    public AudioClip landClip;

    float lastSound;

    public void PickUp(RifleController rifle)
    {
        rifle.Equip();
        Destroy(gameObject);
    }

    void OnCollisionEnter(Collision c)
    {
        if (landClip == null || Time.time - lastSound < 0.12f) return;
        float v = c.relativeVelocity.magnitude;
        if (v < 1.2f) return;
        lastSound = Time.time;
        AudioSource.PlayClipAtPoint(landClip, c.GetContact(0).point, Mathf.Clamp01(v / 8f));
    }
}
