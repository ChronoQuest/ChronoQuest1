using UnityEngine;

public class RainAttack : MonoBehaviour
{
    [Header("Settings")]
    public GameObject projectilePrefab;
    public int projectileCount = 10;
    public float spawnAreaWidth = 10f;
    public float spawnHeight = 8f;
    public float delayBetweenShots = 0.1f;

    // This method will be called by the Animation Event
    public void StartProjectileRain()
    {
        StartCoroutine(SpawnRain());
    }

    private System.Collections.IEnumerator SpawnRain()
    {
        for (int i = 0; i < projectileCount; i++)
        {
            // Calculate a random position above the player
            float randomX = Random.Range(-spawnAreaWidth / 2, spawnAreaWidth / 2);
            float randomYOffset = Random.Range(-1f, 1f);
            Vector3 spawnPos = new Vector3(transform.position.x + randomX, transform.position.y + spawnHeight + randomYOffset, 0);
            // Spawn the projectile pointing downward
            GameObject bolt = Instantiate(projectilePrefab, spawnPos, Quaternion.Euler(0, 0, -90));
            float randomSpeed = Random.Range(12f, 18f);
            bolt.GetComponent<Rigidbody2D>().linearVelocity = Vector2.down * randomSpeed;

            yield return new WaitForSeconds(delayBetweenShots);
        }
    }
}