using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class HeartDisplay : MonoBehaviour
{
    [Header("References")]
    public PlayerHealth playerHealth;
    public GameObject heartPrefab; // Must have HeartIcon script!
    public Transform container;

    [Header("Assets")]
    // Drag sprite sheet here. 
    // Element 0 = Full Heart
    // Element 4 = Empty Heart
    public Sprite[] heartSprites; 

    private List<HeartIcon> hearts = new List<HeartIcon>();

    private void Start()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged += RefreshDisplay;
            RefreshDisplay(playerHealth.CurrentHealth, playerHealth.MaxHealth);
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= RefreshDisplay;
    }

    public void RefreshDisplay(int currentHealth, int maxHealth)
    {
        // 1. Correct the number of heart objects
        while (hearts.Count < maxHealth)
        {
            CreateHeart();
        }
        while (hearts.Count > maxHealth)
        {
            Destroy(hearts[hearts.Count - 1].gameObject);
            hearts.RemoveAt(hearts.Count - 1);
        }

        // 2. Set the state of each heart
        for (int i = 0; i < hearts.Count; i++)
        {
            // If this heart index is less than current health, it should be FULL.
            // Example: HP is 3. 
            // Heart 0 (1st) < 3 -> Full
            // Heart 1 (2nd) < 3 -> Full
            // Heart 2 (3rd) < 3 -> Full
            // Heart 3 (4th) >= 3 -> Empty (Animate away)
            
            bool shouldBeFull = i < currentHealth;
            hearts[i].SetHeartState(shouldBeFull);
        }
    }

    private void CreateHeart()
    {
        GameObject newHeart = Instantiate(heartPrefab, container);
        HeartIcon icon = newHeart.GetComponent<HeartIcon>();
        
        // Pass the sprites to the icon so it knows how to animate itself
        icon.Setup(heartSprites);
        
        hearts.Add(icon);
    }
}
