using UnityEngine;
using System.Collections;
using TimeRewind;
using Unity.Collections;
public class BossAttackManager : MonoBehaviour, IRewindable
{
    public GameObject fireball;
    public GameObject fireColumn;
    public GameObject fireRow;
    public GameObject fireWave;
    public GameObject slime;
    public GameObject platforms;
    public GameObject floorFire;
    public Transform player;
    private bool isRewinding;
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
        if (isRewinding) return;
        float randX = Random.Range(-12f, 2.5f);
        Instantiate(fireball, new Vector3(randX,9f,0f), transform.rotation);
    }

    public void spawnFireColumns()
    {
        if (isRewinding) return;
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
        if (isRewinding) return;
        Instantiate(fireRow, new Vector3(2.5f, -4.25f, 0f), transform.rotation);
    }

    public void spawnFireWave()
    {
        if (isRewinding) return;
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
        if (isRewinding) return;
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

    public void spawnPlatforms()
    {
        if (isRewinding) return;
        Instantiate(platforms, new Vector3(0f, -5f, 1f), transform.rotation);
    }
    

    public void spawnFloorFire()
    {
        if (isRewinding) return;
        Instantiate(floorFire, new Vector3(11f, -6.75f, 0f), transform.rotation);
    }

    public void OnStartRewind()
    {
        isRewinding = true;
    }

    public void OnStopRewind()
    {
        isRewinding = false;
    }

    public RewindState CaptureState()
    {
        return RewindState.Create(transform.position, transform.rotation, Time.time);
    }

    public void ApplyState(RewindState state)
    {
        
    }
}
