using UnityEngine;

public class BossAttackManager : MonoBehaviour
{
    public GameObject fireball;
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
}
