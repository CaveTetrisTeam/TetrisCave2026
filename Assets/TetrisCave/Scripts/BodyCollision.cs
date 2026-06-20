using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Detects collisions of the bodyparts and creates a particle system and sound if hands of avatar collide with each other.
/// </summary>
public class BodyCollision : MonoBehaviour
{
    private ParticleSystem particleEffectPrefab;
    private AudioClip collisionSound;

    public void SetParticleEffect(ParticleSystem ps)
    {
        particleEffectPrefab = ps;
    }
    public void SetCollisionSound(AudioClip clip)
    {
        collisionSound = clip;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (gameObject.tag == "Hand" && collision.gameObject.tag == "Hand"){
           if (collision.contacts.Length > 0)
            {
                Vector3 contactPoint = collision.contacts[0].point;
                CreateBasicParticleEffect(contactPoint);
                PlayCollisionSound(contactPoint);
            }
        }
    }

    void CreateBasicParticleEffect(Vector3 position)
    {
        if (particleEffectPrefab == null)
        {
            Debug.LogWarning("Particle system prefab not set.");
            return;
        }
       
        ParticleSystem instance = Instantiate(particleEffectPrefab, position, Quaternion.identity);
        instance.Play();
        Destroy(instance.gameObject, instance.main.startLifetime.constant + 0.2f);
    }

    void PlayCollisionSound(Vector3 position)
    {
        if (collisionSound == null) return;

        GameObject soundObj = new GameObject("CollisionSound");
        soundObj.transform.position = position;

        AudioSource audioSource = soundObj.AddComponent<AudioSource>();
        audioSource.clip = collisionSound;
        audioSource.Play();

        Destroy(soundObj, collisionSound.length + 0.1f);
    }
}
