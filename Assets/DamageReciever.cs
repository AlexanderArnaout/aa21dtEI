using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class DamageReceiver : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private int maxHealth = 1;
    [SerializeField] private int currentHealth;
    
    [Header("Hit Zones")]
    [SerializeField] private float headMultiplier = 2.0f;
    [SerializeField] private float bodyMultiplier = 1.0f;
    [SerializeField] private Transform headCenter;
    
    [Header("Visual Feedback")]
    [SerializeField] private Material normalMaterial;
    [SerializeField] private Material hitMaterial;
    [SerializeField] private float hitFlashDuration = 0.1f;
    
    [Header("Events")]
    public UnityEvent<HitInfo> OnHit;
    public UnityEvent OnDefeat;
    
    // Cached components
    private Renderer rend;
    
    void Start()
    {
        // Initialize health
        currentHealth = maxHealth;
        
        // Cache components
        rend = GetComponent<Renderer>();
        
        // Set head center if not assigned
        if (headCenter == null && transform.childCount > 0)
        {
            // Try to find a head object
            foreach (Transform child in transform)
            {
                if (child.name.ToLower().Contains("head"))
                {
                    headCenter = child;
                    break;
                }
            }
            
            // If no head found, use the top of the object
            if (headCenter == null)
            {
                // Create a transform at the approximate head position
                GameObject headObj = new GameObject("HeadCenter");
                headObj.transform.parent = transform;
                
                // Estimate head position based on collider height
                Collider col = GetComponent<Collider>();
                if (col != null)
                {
                    float height = col.bounds.size.y;
                    headObj.transform.localPosition = new Vector3(0, height * 0.8f, 0);
                }
                else
                {
                    // Default position if no collider
                    headObj.transform.localPosition = new Vector3(0, 1.7f, 0);
                }
                
                headCenter = headObj.transform;
            }
        }
    }
    
    public void TakeDamage()
    {
        Debug.Log("took damage");

        // Apply damage multiplier based on hit zone
        //float multiplier = GetHitZoneMultiplier(hitInfo.hitPosition);
        int finalDamage = Mathf.RoundToInt(10);
        
        // Apply damage
        currentHealth = Mathf.Max(0, currentHealth - finalDamage);
        
        // Visual feedback
        if (rend != null && hitMaterial != null)
        {
            StartCoroutine(FlashHitEffect());
        }
        
        // Trigger hit event
        //OnHit?.Invoke();
        
        // Debug output
        Debug.Log($"{gameObject.name} took {finalDamage} damage! Health: {currentHealth}/{maxHealth}");
        
        // Check for defeat
        if (currentHealth <= 0)
        {
            Defeat();
            Debug.Log("dead");
        }
    }
    
    private float GetHitZoneMultiplier(Vector3 hitPosition)
    {
        // Default to body multiplier
        float multiplier = bodyMultiplier;
        
        // Check if hit is near the head
        if (headCenter != null)
        {
            float distanceToHead = Vector3.Distance(hitPosition, headCenter.position);
            if (distanceToHead < 0.2f) // Adjust this threshold based on your character scale
            {
                multiplier = headMultiplier;
                Debug.Log("Headshot!");
            }
        }
        
        return multiplier;
    }
    
    private IEnumerator FlashHitEffect()
    {
        // Store original material
        Material originalMaterial = rend.material;
        
        // Apply hit material
        rend.material = hitMaterial;
        
        // Wait for flash duration
        yield return new WaitForSeconds(hitFlashDuration);
        
        // Restore original material
        rend.material = originalMaterial;
    }
    
    private void Defeat()
    {
        Debug.Log($"{gameObject.name} has been defeated!");
        
        // Trigger defeat event
        OnDefeat?.Invoke();

        
        
        // You can add defeat animation, particle effects, etc. here
    }
    
    // Public method to get current health
    public float GetHealthPercent()
    {
        return (float)currentHealth / maxHealth;
    }
    
    // Method to heal the entity
    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }
    
    // Add a simple visual debug gizmo
    void OnDrawGizmos()
    {
        if (headCenter != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(headCenter.position, 0.1f);
        }
    }
}