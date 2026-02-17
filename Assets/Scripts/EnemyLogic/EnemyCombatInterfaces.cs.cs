using UnityEngine;
public interface IDamageable
{
    void TakeDamage(int amount);
}

public interface IKnockbackable
{
    void ApplyKnockback(UnityEngine.Vector2 force);
}
