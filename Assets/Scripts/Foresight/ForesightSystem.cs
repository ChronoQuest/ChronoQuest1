using UnityEngine;
using System.Collections.Generic;
using System.Linq;
public class ForesightSystem : MonoBehaviour
{
    private IForesightEnemy enemy;
    public int baselineHistorySize = 30;
    public float anomalyZScore = -1.5f;
    private int memorySize = 50;
    public float instantTriggerCost = 0.5f;
    private float maxPlayerSpeed = 20f; // Based on dash speed
    // Our custom queue data structures
    private FixedQueue<float> historicalCosts;
    private FixedQueue<PlayerState> currentTimeline;
    private FixedQueue<PlayerState> previousTimeline;
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
    private bool damagedThisTimeline = false;
    public float recordInterval = 0.3f;
    private float recordTimer = 0f;
    private bool hasForesight = false;
    private ForesightTactics? lockedTactic = null;
    private int lockedAttackIndex = -1;
    public float minDistToPlayer = 30f;

    void Awake()
    {
        // Pre-allocate memory just once to save performance
        historicalCosts = new FixedQueue<float>(baselineHistorySize);
        currentTimeline = new FixedQueue<PlayerState>(memorySize);
        previousTimeline = new FixedQueue<PlayerState>(memorySize);
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
            
            currentTimeline.Enqueue(state);
            
            // Only calculate cost if the previous timeline actually exists
            if (previousTimeline.Count >= minTimelineSize)
            {
                float currentCost = CalculateDTWCost();
                bool isPredictable = EvaluateZScore(currentCost);

                if (isPredictable)
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
            }

            highestAttackThisInterval = 0;
            recordTimer = 0f;
        }
    }
    public void NotifyDamage()
    {
        damagedThisTimeline = true;
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
    // (Normalised) Heuristic for calculating distance between tactics
    float GetPlayerStateDistance(PlayerState a, PlayerState b)
    {
        float normDistA = Mathf.Clamp01(a.distance / minDistToPlayer);
        float normDistB = Mathf.Clamp01(b.distance / minDistToPlayer);
        float distDiffSq = Mathf.Pow(normDistA - normDistB, 2);

        Vector2 normVelA = a.velocity / maxPlayerSpeed;
        Vector2 normVelB = b.velocity / maxPlayerSpeed;
        if (normVelA.sqrMagnitude > 1f) normVelA.Normalize();
        if (normVelB.sqrMagnitude > 1f) normVelB.Normalize();
        float velDiffSq = (normVelA - normVelB).sqrMagnitude;

        float normAttA = a.attackType / 2f;
        float normAttB = b.attackType / 2f;
        float attackDiffSq = Mathf.Pow(normAttA - normAttB, 2);
        
        float distance = Mathf.Sqrt(
            (weightDistance * distDiffSq) + 
            (weightVelocity * velDiffSq) +
            (weightAttack * attackDiffSq)
        );

        return distance;
    }
    // Use Dynamic Time Warp algorithm to calculate similarity between current and previous timeline
    float CalculateDTWCost()
    {
        // We need both timelines to have at least the window size number of samples
        if (currentTimeline.Count < minTimelineSize || previousTimeline.Count < minTimelineSize) return 0f;

        // If we reach an unobserved point in time, we can't trigger foresight
        if (currentTimeline.Count > previousTimeline.Count) return 0f;

        int c_len = currentTimeline.Count;
        int p_len = previousTimeline.Count;

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
                    float cost = GetPlayerStateDistance(currentTimeline.Get(i - 1), previousTimeline.Get(j - 1));
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

        // Return the raw, average cost per step
        return bestCost / (c_len + p_len); 
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
    bool EvaluateZScore(float currentCost)
    {
        historicalCosts.Enqueue(currentCost);

        // If we don't have enough long term data, use a fallback to favour triggering foresight
        if (historicalCosts.Count < 7) 
        {
            Debug.Log($"Not enough samples yet, using fallback... Cost: {currentCost:F2} | Threshold: {instantTriggerCost}");
            return currentCost <= instantTriggerCost;
        }

        float sum = 0f;
        for (int i = 0; i < historicalCosts.Count; i++) sum += historicalCosts.Get(i);
        float mean = sum / historicalCosts.Count;

        float sumOfSquares = 0f;
        for (int i = 0; i < historicalCosts.Count; i++)
        {
            sumOfSquares += Mathf.Pow(historicalCosts.Get(i) - mean, 2);
        }
        float variance = sumOfSquares / historicalCosts.Count;
        float standardDeviation = Mathf.Sqrt(variance);

        // Prevent division by zero if all costs are perfectly identical
        if (standardDeviation < 0.0001f) return false;

        float zScore = (currentCost - mean) / standardDeviation;

        Debug.Log($"Cost: {currentCost:F2} | Mean: {mean:F2} | Z-Score: {zScore:F2}");

        // We want the cost to be lower than an anomalous score
        return zScore <= anomalyZScore;
    }
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
        // Look ahead into the future
        for (int i = nextIndex; i < nextIndex + futureLookaheadSteps; i++)
        {
            // Stop looking if we hit the end of the recorded present
            if (i >= previousTimeline.Count) break; 

            if (previousTimeline.Get(i).attackType > 0) 
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
        enemyDamagedPreviously = damagedThisTimeline;
        statesErased = Mathf.Clamp(statesErased, 0, currentTimeline.Count);
        damagedThisTimeline = false;
        
        previousTimeline.Clear();
        int startIndex = currentTimeline.Count - statesErased;
        
        // Push the erased "present" states into the "previous" timeline (the remembered future)
        for (int i = startIndex; i < currentTimeline.Count; i++)
        {
            previousTimeline.Enqueue(currentTimeline.Get(i));
        }

        historicalCosts.Clear(); 
        currentTimeline.Clear();
        currentTimeline.Enqueue(GetCurrentPlayerState());
        
        recordTimer = 0f; 
    }
}

// A zero-allocation circular buffer to replace/optimise System.Collections.Generic.Queue
public class FixedQueue<T>
{
    private T[] data;
    private int head;
    public int Count { get; private set; }
    public int Capacity => data.Length;

    public FixedQueue(int capacity)
    {
        data = new T[capacity];
        head = 0;
        Count = 0;
    }

    public void Enqueue(T item)
    {
        if (Count == Capacity)
        {
            // If full, overwrite the oldest item and move the head forward
            data[head] = item;
            head = (head + 1) % Capacity;
        }
        else
        {
            // If not full, just add to the end
            data[(head + Count) % Capacity] = item;
            Count++;
        }
    }

    public void Clear()
    {
        head = 0;
        Count = 0;
    }

    // Retrieve items from oldest (0) to newest (Count - 1)
    public T Get(int index)
    {
        return data[(head + index) % Capacity];
    }
}