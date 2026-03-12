using UnityEngine;

public interface IForesightEnemy
{
    Transform player { get; }
    int GetPlayerAttackState(); 

    void PerformForesightDodge(Vector2 attackDirection);
    void PerformForesightLunge(Vector2 approachDirection);
    void SetForesightState(bool hasForesight);
    bool IsDead();
    bool IsRewinding();
}