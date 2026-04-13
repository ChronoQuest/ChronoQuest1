using UnityEngine;
using TimeRewind;
using System.Collections.Generic;

public class BossAttackManager : MonoBehaviour, IRewindable
{
    public GameObject fireball;
    public GameObject homingFireball;
    public GameObject fireColumn;
    public GameObject fireRow;
    public GameObject fireWave;
    public List<GameObject> enemyList;
    public GameObject platforms;
    public GameObject floorFire;
    public GameObject fireExplosion;
    public Transform player;
    private bool isRewinding;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // To stop the game freezing when a bat is first spawned, spawn one at the very start and destroy it
        GameObject firstBat = Instantiate(enemyList[0], new Vector3(0, -50f, 0), Quaternion.identity);
        firstBat.SetActive(false); 
        Destroy(firstBat);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void spawnFireball(int facingDirection)
    {
        if (isRewinding) return;
        float randX = Random.Range(-12f, 2.5f) * facingDirection;
        Instantiate(fireball, new Vector3(randX,9f,0f), fireball.transform.rotation);
    }
    public void spawnHomingFireball(int facingDirection)
    {
        if (isRewinding) return;
        float randX = Random.Range(-12f, 2.5f) * facingDirection;
        Instantiate(homingFireball, new Vector3(randX,6f,0f), homingFireball.transform.rotation);
    }
    public void spawnFireExplosion(int facingDirection)
    {
        if (isRewinding) return;
        float randX = Random.Range(-12f, 2.5f) * facingDirection;
        float randY = Random.Range(-5.7f, -0.9f);
        Instantiate(fireExplosion, new Vector3(randX,randY,0f), fireExplosion.transform.rotation);
    }

    public void spawnFireColumns(int facingDirection)
    {
        if (isRewinding) return;
        float playerX = player.transform.position.x;
        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        GameObject column = Instantiate(fireColumn, new Vector3(playerX,-1.2f,0f), transform.rotation);
        Firecolumns fc = column.GetComponent<Firecolumns>();
        fc.bossFacingDirection = facingDirection;
        if(playerHealth.CurrentHealth < 3){
            // If player health is less than 3, cut the movement speed of fire columns in half
            column.GetComponent<Firecolumns>().moveSpeed = 1.5f;
        }
    }

    public void spawnFireRow(int facingDirection)
    {
        if (isRewinding) return;
        // Spawn a big persistent fire explosion in front of the boss as the visual source
        GameObject explosion = Instantiate(fireExplosion, new Vector3(7.5f * facingDirection, -3f, 0f), fireExplosion.transform.rotation);
        explosion.transform.localScale = new Vector3(2f, 2f, 1f);
        FireExplosion fe = explosion.GetComponent<FireExplosion>();
        fe.persistent = true;
        // Fire row extends out from the explosion
        GameObject fire_row = Instantiate(fireRow, new Vector3(6f * facingDirection, -4.25f, 0f), transform.rotation);
        FireRow fr = fire_row.GetComponent<FireRow>();
        fr.bossFacingDirection = facingDirection;
        fr.sourceExplosion = explosion;
    }

    public void spawnFireWave(int facingDirection)
    {
        if (isRewinding) return;
        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        GameObject wave = Instantiate(fireWave, new Vector3(6.5f * facingDirection, -6.15f, 0f), transform.rotation);
        wave.transform.localScale = new Vector3(facingDirection * -2f, 2f, 1f);
        FireWave fw = wave.GetComponent<FireWave>();
        fw.bossFacingDirection = facingDirection;
        if(playerHealth.CurrentHealth < 3)
        {
            // If player health is less than 3, cut the movement speed of fire wave in half
            fw.moveSpeed *= 0.5f;
        }
    }

    public void spawnEnemy(int facingDirection)
    {
        if (isRewinding) return;
        float randX = Random.Range(-12f * facingDirection, 3.25f);
        int size = enemyList.Count;
        GameObject chosenEnemy = enemyList[Random.Range(0, size)];
        GameObject newEnemy = Instantiate(chosenEnemy, new Vector3(randX, 5f, 0f), transform.rotation);
        IBossSpawnable enemyComponent = newEnemy.GetComponent<IBossSpawnable>();
        if (enemyComponent != null) {
            enemyComponent.player = player;
            enemyComponent.DoubleDetectionRange();
        }
    }

    public void spawnPlatforms(int facingDirection)
    {
        if (isRewinding) return;
        //Instantiate(platforms, new Vector3(4f * facingDirection, -5f, 1f), transform.rotation);
        Instantiate(platforms, new Vector3(-4f * facingDirection, -5f, -2f), transform.rotation);
    }
    

    public void spawnFloorFire(int facingDirection)
    {
        if (isRewinding) return;
        GameObject fire_floor = Instantiate(floorFire, new Vector3(12.25f * facingDirection, -6.75f, 0f), transform.rotation);
        FloorFireRow ff = fire_floor.GetComponent<FloorFireRow>();
        ff.bossFacingDirection = facingDirection;
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
