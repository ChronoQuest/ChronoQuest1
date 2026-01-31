using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
public class BatAgent : Agent 
{
    [Header("References")]
    public Transform player;
    private Collider2D playerCollider;
    // Eventually to be used for 'flanking'
    public Transform otherBat;
    
    private Rigidbody2D rb;
    private Vector3 startPos;
    
    public float moveSpeed = 5f;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody2D>();
        startPos = transform.position;
        playerCollider = player.GetComponent<Collider2D>();
    }

    public override void OnEpisodeBegin()
    {
        // Start bat in a random position every time
        transform.position = startPos + (Vector3)Random.insideUnitCircle * 2f;
        rb.linearVelocity = Vector2.zero;

    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Get distance to player
        Vector2 toPlayer = playerCollider.bounds.center - transform.position;
    
        // Give the AI access to the player's position
        sensor.AddObservation(toPlayer.x); 
        sensor.AddObservation(toPlayer.y);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveX = actions.ContinuousActions[0];
        float moveY = actions.ContinuousActions[1];

        Vector2 movement = new Vector2(moveX, moveY) * moveSpeed * Time.deltaTime;
        rb.MovePosition(rb.position + movement);

        // Reward function
        float distanceToPlayer = Vector2.Distance(transform.position, playerCollider.bounds.center);

        // Make sure speed is taken into account (longer, less reward)
        AddReward(-0.001f);

        // Reward for touching player
        if (distanceToPlayer < 1.0f)
        {
            AddReward(1.0f);
            EndEpisode();
        }
    }
}