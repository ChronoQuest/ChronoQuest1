using UnityEngine;
using System.Collections.Generic;
using System.Linq;
public class ForesightSystem : MonoBehaviour
{
    private IForesightEnemy enemy;
    public float foresightThreshold = 0.75f;
    private int memorySize = 50;
    private Queue<PlayerState> currentTimeline = new Queue<PlayerState>();
    private Queue<PlayerState> previousTimeline = new Queue<PlayerState>();
    private int bestMatchIndex = -1;

    // Possible 'tactics' a player could be employing
    public struct PlayerState
    {
        public Vector2 relativeDirection;
        public float distance;
        public Vector2 velocity;
        public int attackType; // 0: Not attacking, 1: Melee, 2: Spell
    }

    public enum ForesightTactics { Dodge, Lunge }
    public int futureLookaheadSteps = 20;
    public float weightDistance = 0.5f;
    public float weightVelocity = 0.2f;
    public float weightAttack = 2.0f;
    public int minTimelineSize = 3;
    public int bandWidth = 6;
    private int highestAttackThisInterval = 0;
    private bool enemyDamagedPreviously = false;
    public float recordInterval = 0.3f;
    private float recordTimer = 0f;
    private float sequenceSimilarity = 0f;
    private bool hasForesight = false;
    private ForesightTactics? lockedTactic = null;
    private int lockedAttackIndex = -1;
    private float minDistToPlayer = 30f;

    void Awake()
    {
        enemy = GetComponent<IForesightEnemy>();
        // Immediately get a player state
        PlayerState state = GetCurrentPlayerState();
        currentTimeline.Enqueue(state);
    }

    void Update()
    {
        if (enemy.IsDead() || enemy.IsRewinding() || enemy.IsPerformingForesightAction()) return;
        // If the player is too far away, don't store any info about the player
        if(enemy.GetDistanceToPlayer() > minDistToPlayer) return;

        int currentAttackState = enemy.GetPlayerAttackState();
        if (currentAttackState > highestAttackThisInterval) 
            highestAttackThisInterval = currentAttackState;

        recordTimer += Time.deltaTime;
        if (recordTimer >= recordInterval)
        {
            PlayerState state = GetCurrentPlayerState();
            state.attackType = highestAttackThisInterval;
            
            if (currentTimeline.Count >= memorySize) currentTimeline.Dequeue();
            currentTimeline.Enqueue(state);
            
            sequenceSimilarity = CalculateDTWSimilarity();
            Debug.Log("SEQUENCE SIMILARITY: " + sequenceSimilarity);
            if (sequenceSimilarity >= foresightThreshold)
            {
                if (!hasForesight)
                {
                    hasForesight = true;
                    enemy.SetForesightState(true);
                }

                ForesightTactics tactic = DetermineForesightAction();
                if (tactic == ForesightTactics.Dodge) enemy.ExecuteDodge();
                else if (tactic == ForesightTactics.Lunge) enemy.ExecuteLunge();
            }
            else
            {
                // Player is behaving differently
                if (hasForesight)
                {
                    hasForesight = false;
                    enemy.SetForesightState(false);
                }
            }

            highestAttackThisInterval = 0;
            recordTimer = 0f;
        }
    }
    public void NotifyDamage()
    {
        enemyDamagedPreviously = true;
    }
    private PlayerState GetCurrentPlayerState()
    {
        PlayerState state = new PlayerState();
        Vector2 offset = enemy.player.position - transform.position;
        state.relativeDirection = offset.normalized;
        state.distance = offset.magnitude;
        Rigidbody2D playerRb = enemy.player.GetComponent<Rigidbody2D>();
        state.velocity = playerRb.linearVelocity;
        state.attackType = enemy.GetPlayerAttackState(); 
        return state;
    }
    // Heuristic for calculating distance between tactics
    float GetPlayerStateDistance(PlayerState a, PlayerState b)
    {
        // Differences squared
        float distDiffSq = Mathf.Pow(a.distance - b.distance, 2);
        float velDiffSq = (a.velocity - b.velocity).sqrMagnitude;
        float attackDiffSq = Mathf.Pow(a.attackType - b.attackType, 2);
        
        // Weighted distance: attacking is most important, then distance, then velocity
        float distance = Mathf.Sqrt(
            (weightDistance * distDiffSq) + 
            (weightVelocity * velDiffSq) +
            (weightAttack * attackDiffSq)
        );

        return distance;
    }
    // Use Dynamic Time Warp algorithm to calculate similarity between current and previous timeline
    float CalculateDTWSimilarity()
    {
        // We need both timelines to have at least the window size number of samples
        if (currentTimeline.Count < minTimelineSize || previousTimeline.Count < minTimelineSize) return 0f;

        // If we reach an unobserved point in time, we can't trigger foresight
        if (currentTimeline.Count > previousTimeline.Count) return 0f;

        // Convert both queues into arrays for easier manipulation
        var current = currentTimeline.ToArray();
        var previous = previousTimeline.ToArray();
        int c_len = current.Length;
        int p_len = previous.Length;

        // Band must be able to reach the final cell!
        int w = Mathf.Max(bandWidth, Mathf.Abs(c_len - p_len));

        float[,] dtw = new float[c_len + 1, p_len + 1];

        // Initialise table
        for (int i = 0; i <= c_len; i++)
            for (int j = 0; j <= p_len; j++)
                dtw[i, j] = float.PositiveInfinity;

        dtw[0, 0] = 0;

        // Populate matrix, using Sakoe-Chiba Band
        for (int i = 1; i <= c_len; i++)
            {
                // Calculate dynamic start and end bounds for the inner loop
                int startJ = Mathf.Max(1, i - w);
                int endJ = Mathf.Min(p_len, i + w);

                for (int j = startJ; j <= endJ; j++)
                {
                    float cost = GetPlayerStateDistance(current[i - 1], previous[j - 1]);
                    // D(i,j) = d(i,j) + min(D(i-1,j), D(i,j-1), D(i-1,j-1))
                    dtw[i, j] = cost + Mathf.Min(dtw[i - 1, j], Mathf.Min(dtw[i, j - 1], dtw[i - 1, j - 1]));
                }
            }
        // Find best match column in final row
        float bestCost = float.PositiveInfinity;
        int bestJ = -1;

        for (int j = 1; j <= p_len; j++)
        {
            if (dtw[c_len, j] < bestCost)
            {
                bestCost = dtw[c_len, j];
                bestJ = j - 1;
            }
        }

        bestMatchIndex = bestJ;

        float averageCost = bestCost / (c_len + p_len);
        return 1.0f - Mathf.Clamp01(averageCost / 2.0f);
    }

    // Old Hamming-weight distance function
    // float CalculateSimilarity()
    // {
    //     if (currentTimeline.Count < windowSize || previousTimeline.Count < windowSize)
    //         return 0f;

    //     var currentArray = currentTimeline.ToArray();
    //     var previousArray = previousTimeline.ToArray();

    //     int matches = 0;
    //     for (int i = 0; i < windowSize; i++)
    //     {
    //         PlayerTactic current = currentArray[currentArray.Length - 1 - i];
    //         PlayerTactic previous = previousArray[previousArray.Length - 1 - i];
    //         if (current == previous) matches++;
    //     }

    //     return (float)matches / windowSize;
    // }
    ForesightTactics DetermineForesightAction()
    {
        if (lockedTactic != null)
        {   
            // Add a 1.2second buffer between unlocking so attack has time to move to enemy
            if (bestMatchIndex < lockedAttackIndex + 4) return lockedTactic.Value;
            else lockedTactic = null;
        }
        bool attacking = enemy.GetPlayerAttackState() > 0;
        if (attacking || enemyDamagedPreviously) return ForesightTactics.Dodge;
        int nextIndex = bestMatchIndex + 1;
        var previousArray = previousTimeline.ToArray();
        // Look ahead into the future
        for (int i = nextIndex; i < nextIndex + futureLookaheadSteps; i++)
        {
            // Stop looking if we hit the end of the recorded present
            if (i >= previousArray.Length) break; 

            if (previousArray[i].attackType > 0) 
            {
                lockedTactic = ForesightTactics.Dodge;
                lockedAttackIndex = i;
                return ForesightTactics.Dodge;
            }
        }
        return ForesightTactics.Lunge; 
    }
    public void HandleRewindStop(int statesErased)
    {
        // Clamp to avoid out-of-bounds errors
        statesErased = Mathf.Clamp(statesErased, 0, currentTimeline.Count);
        var currentArray = currentTimeline.ToArray();
        
        previousTimeline.Clear();
        int startIndex = currentArray.Length - statesErased;
        
        // Push the erased "present" states into the "previous" timeline (the remembered future)
        for (int i = startIndex; i < currentArray.Length; i++)
        {
            previousTimeline.Enqueue(currentArray[i]);
        }

        // Wipe current timeline and start fresh
        currentTimeline.Clear();
        PlayerState state = GetCurrentPlayerState();
        currentTimeline.Enqueue(state);
        
        recordTimer = 0f; 
    }
}