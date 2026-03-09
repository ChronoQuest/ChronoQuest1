using UnityEngine;

public class RainAttack : MonoBehaviour
{
    [Header("Settings")]
    public GameObject projectilePrefab;
    public int projectileCount = 10;
    public float spawnAreaWidth = 10f;
    public float spawnHeight = 8f;
    public float delayBetweenShots = 0.1f;
    public LayerMask groundLayer;
    public TutorialManager tutorialManager; 

    // This method will be called by the Animation Event
    public void StartProjectileRain()
    {
        tutorialManager?.OnPlayerRainSpell();
        StartCoroutine(SpawnRain());
    }

    private System.Collections.IEnumerator SpawnRain()
    {
        for (int i = 0; i < projectileCount; i++)
        {
            float randomX = Random.Range(-spawnAreaWidth / 2, spawnAreaWidth / 2);
            Vector2 spawnOrigin = new Vector2(transform.position.x + randomX, transform.position.y);
        
            // Raycast for EVERY drop to handle uneven ceilings
            float actualSpawnY = transform.position.y + spawnHeight;
            RaycastHit2D hit = Physics2D.Raycast(spawnOrigin, Vector2.up, 20f, groundLayer);

            if (hit.collider != null)
            {
                actualSpawnY = hit.point.y - 0.5f;
            }

            // Spawn the bolt
            Vector3 spawnPos = new Vector3(spawnOrigin.x, actualSpawnY + Random.Range(-0.5f, 0.5f), 0);
            GameObject bolt = Instantiate(projectilePrefab, spawnPos, Quaternion.Euler(0, 0, -90));
        
            float randomSpeed = Random.Range(12f, 18f);
            bolt.GetComponent<Rigidbody2D>().linearVelocity = Vector2.down * randomSpeed;

            yield return new WaitForSeconds(delayBetweenShots);
        }
    }
}