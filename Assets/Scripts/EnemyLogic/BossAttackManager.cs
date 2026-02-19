using UnityEngine;
using System.Collections;
using TimeRewind;
public class BossAttackManager : MonoBehaviour
{
    public GameObject fireball;
    public GameObject fireColumn;
    public GameObject fireRow;
    public GameObject fireWave;
    public GameObject slime;
    public GameObject platforms;
    public GameObject floorFire;
    public Transform player;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void spawnFireball()
    {
        float randX = Random.Range(-12f, 2.5f);
        Instantiate(fireball, new Vector3(randX,9f,0f), transform.rotation);
    }

    public void spawnFireColumns()
    {
        float playerX = player.transform.position.x;
        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        GameObject column = Instantiate(fireColumn, new Vector3(playerX,-1.2f,0f), transform.rotation);
        if(playerHealth.CurrentHealth < 3){
            // If player health is less than 3, cut the movement speed of fire columns in half
            column.GetComponent<Firecolumns>().moveSpeed = 1.5f;
        }
    }

    public void spawnFireRow()
    {
        Instantiate(fireRow, new Vector3(2.5f, -4.25f, 0f), transform.rotation);
    }

    public void spawnFireWave()
    {
        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        GameObject wave = Instantiate(fireWave, new Vector3(4.5f, -6.5f, 0f), transform.rotation);
        if(playerHealth.CurrentHealth < 3)
        {
            // If player health is less than 3, cut the movement speed of fire wave in half
            wave.GetComponent<FireWave>().moveSpeed = 1f;
        }
    }

    public void spawnEnemy()
    {
        float randX = Random.Range(-12f, 2.5f);
        if(Random.value > 0f) {
            GameObject newSlime = Instantiate(slime, new Vector3(randX, 6f, 0f), transform.rotation);
            SlimeEnemy slimeComponent = newSlime.GetComponent<SlimeEnemy>();
            if (slimeComponent != null)
            {
                slimeComponent.player = player;
            }
        }
    }

    public void raisePlatforms()
    {
        StartCoroutine(movePlatform(platforms.transform.position + Vector3.up * 5f));
    }

    public void lowerPlatforms()
    {
        StartCoroutine(movePlatform(platforms.transform.position - Vector3.up * 5f));
    }

private IEnumerator movePlatform(Vector3 targetPos)
    {
        Rigidbody2D rb = platforms.GetComponent<Rigidbody2D>();
        Vector3 startPos = platforms.transform.position;
        float duration = 2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (TimeRewindManager.Instance != null && TimeRewindManager.Instance.IsRewinding)
            {
                while (TimeRewindManager.Instance.IsRewinding)
                    yield return null;
                }
                
                float totalDistance = Vector3.Distance(startPos, targetPos);
                if (totalDistance > 0.01f)
                {
                    float currentDist = Vector3.Distance(startPos, platforms.transform.position);
                    float progress = Mathf.Clamp01(currentDist / totalDistance);
                    elapsed = progress * duration;
                }
            }
            rb.MovePosition(Vector2.Lerp(startPos, targetPos, elapsed / duration));
            elapsed += Time.deltaTime;

        platforms.transform.position = targetPos;
    }

    public void spawnFloorFire()
    {
        Instantiate(floorFire, new Vector3(11f, -6.75f, 0f), transform.rotation);
    }
}
