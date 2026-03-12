using UnityEngine;

public interface IForesightEnemy
{
    Transform player { get; }
    int GetPlayerAttackState(); 

    void ExecuteDodge();
    void ExecuteLunge();
    void SetForesightState(bool hasForesight);
    bool IsDead();
    bool IsRewinding();
    bool IsPerformingForesightAction();
}