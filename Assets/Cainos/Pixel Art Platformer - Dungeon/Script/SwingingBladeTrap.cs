using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cainos.PixelArtPlatformer_Dungeon
{
    public class SwingingBladeTrap : MonoBehaviour
    {
        [Header("Params")]
        public AnimationCurve bladeRotationCurve;
        public float bladeRotationMaxAngle = 50.0f;
        public float bladeRotationTime = 3.0f;

        [Header("Damage Settings")]
        [Tooltip("Amount of damage dealt to the player on contact")]
        public int damageAmount = 1;

        [Header("Objects")]
        public Transform blade;

        private float timer;
        private float v;

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer > bladeRotationTime) timer = 0.0f;

            v = bladeRotationCurve.Evaluate(timer / bladeRotationTime);

            blade.transform.localRotation = Quaternion.Euler(0.0f, 0.0f, v * bladeRotationMaxAngle);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Check if the colliding object is the player
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                // Deal damage (negative value for damage)
                playerHealth.ModifyHealth(-damageAmount);
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            // Also check for regular collisions in case a Collider2D is used instead of a trigger
            PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                // Deal damage (negative value for damage)
                playerHealth.ModifyHealth(-damageAmount);
            }
        }
    }
}
