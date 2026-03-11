using UnityEngine;

public interface IBossSpawnable
{
    Transform player { get; set; }
    void DoubleDetectionRange();
}