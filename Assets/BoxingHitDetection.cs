using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoxingHitDetection : MonoBehaviour
{
    [Header("Hit Detection")]
    [SerializeField] private Transform headTransform;
    [SerializeField] private Transform bodyTransform;
    [SerializeField] private float headHitRadius = 0.15f;
    [SerializeField] private float bodyHitRadius = 0.3f;
    
    [Header("Hit Reaction")]
    [SerializeField] private float minImpactForce = 1.0f;
    [SerializeField] private float knockbackMultiplier = 0.1f;
    [SerializeField] private float heavyHitThreshold = 5.0f;
    [SerializeField] private float criticalHitThreshold = 10.0f;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private GameObject sweatEffectPrefab;
    [SerializeField] private GameObject knockoutStarsPrefab;
    
    // Reference to the main AI script
    private BoxingEnemyAI enemyAI;
    private Animator animator;
    private Rigidbody rb;
    
    // Hit zones as separate colliders for precise hit detection
    [System.Serializable]
    public class HitZone
    {
        public string zoneName;
        public Transform zoneTransform;
        public float damageMultiplier = 1.0f;
        public string hitReactionTrigger;
        public int scoreValue = 1;
    }
    
    [Header("Hit Zones")]
    [SerializeField] private HitZone[] hitZones;
    
    // Track hit cooldowns to prevent multiple rapid hits on the same zone
    private Dictionary<string, float> hitCooldowns = new Dictionary<string, float>();
    private const float HIT_COOLDOWN_TIME = 0.2f;
    
    // Physics materials for different body parts
    [Header("Physics Materials")]
    [SerializeField] private PhysicMaterial headPhysicsMaterial;
    [SerializeField] private PhysicMaterial bodyPhysicsMaterial;
    [SerializeField] private PhysicMaterial guardPhysicsMaterial;
    
    // Audio clips for different hit types
    [Header("Audio")]
    [SerializeField] private AudioClip[] headHitSounds;
    [SerializeField] private AudioClip[] bodyHitSounds;
    [SerializeField] private AudioClip[] blockedHitSounds;
    [SerializeField] private AudioClip[] guardBreakSounds;
    private AudioSource audioSource;
    
    // Haptic feedback events (these would connect to your VR controllers)
    public delegate void HapticFeedbackEvent(float intensity, float duration);
    public static event HapticFeedbackEvent OnHapticFeedback;
    
    // Scoring system
    private int comboCounter = 0;
    private float lastHitTime = 0f;
    private const float COMBO_TIME_WINDOW = 2.5f;
    
    void Start()
    {
        enemyAI = GetComponent<BoxingEnemyAI>();
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Initialize hit cooldowns
        if (hitZones != null)
        {
            foreach (var zone in hitZones)
            {
                hitCooldowns[zone.zoneName] = 0f;
            }
        }
    }
    
    // This method would be called from the player's VR controller collision
    public void RegisterPunchHit(Vector3 hitPoint, Vector3 hitDirection, float impactForce, string playerHand)
    {
        // Verify minimum force threshold to avoid registering light touches
        if (impactForce < minImpactForce)
            return;
            
        // Find which hit zone was struck
        HitZone hitZone = DetermineHitZone(hitPoint);
        
        if (hitZone == null)
            return;
            
        // Check hit cooldown for this zone
        if (Time.time < hitCooldowns[hitZone.zoneName])
            return;
            
        // Update cooldown
        hitCooldowns[hitZone.zoneName] = Time.time + HIT_COOLDOWN_TIME;
        
        // Check if the enemy is blocking
        bool isBlocked = CheckIfBlocked(hitPoint, hitDirection);
        
        // Calculate final damage based on impact force, zone multiplier, and blocking
        int baseDamage = Mathf.RoundToInt(impactForce);
        int finalDamage;
        
        if (isBlocked)
        {
            // Reduced damage if blocked
            finalDamage = Mathf.RoundToInt(baseDamage * hitZone.damageMultiplier * 0.2f);
            
            // Guard break check
            if (impactForce > heavyHitThreshold)
            {
                // Heavy hit can partially break guard
                ProcessGuardBreak(hitPoint, hitDirection, impactForce);
            }
            else
            {
                // Regular blocked hit
                ProcessBlockedHit(hitPoint, hitDirection, impactForce);
            }
        }
        else
        {
            // Full damage if not blocked
            finalDamage = Mathf.RoundToInt(baseDamage * hitZone.damageMultiplier);
            
            // Process the hit based on type
            ProcessCleanHit(hitZone, hitPoint, hitDirection, impactForce, finalDamage);
        }
        
        // Update combo system
        UpdateCombo();
        
        // Apply haptic feedback to player's controllers
        if (OnHapticFeedback != null)
        {
            float hapticIntensity = isBlocked ? impactForce * 0.5f : impactForce;
            OnHapticFeedback.Invoke(hapticIntensity / 10f, 0.1f);
        }
    }
    
    private HitZone DetermineHitZone(Vector3 hitPoint)
    {
        HitZone closestZone = null;
        float closestDistance = float.MaxValue;
        
        foreach (var zone in hitZones)
        {
            if (zone.zoneTransform == null) continue;
            
            float distance = Vector3.Distance(hitPoint, zone.zoneTransform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestZone = zone;
            }
        }
        
        // Check if the hit point is close enough to the zone
        // This depends on your character scale
        float detectionThreshold = 0.3f; // Adjust based on your character scale
        if (closestDistance <= detectionThreshold)
        {
            return closestZone;
        }
        
        return null;
    }
    
    private bool CheckIfBlocked(Vector3 hitPoint, Vector3 hitDirection)
    {
        if (enemyAI == null) return false;
        
        // Access isBlocking state from the BoxingEnemyAI
        // This is a simplification - you might need to adjust how you access this information
        bool isBlocking = animator.GetBool("IsBlocking");
        
        if (!isBlocking) return false;
        
        // Check if the hit is coming from the front where the guard is effective
        Vector3 toHit = (hitPoint - transform.position).normalized;
        float blockAngle = Vector3.Angle(transform.forward, toHit);
        
        // If the hit is coming from a direction the boxer is facing (with some margin)
        return blockAngle < 60f;
    }
    
    private void ProcessBlockedHit(Vector3 hitPoint, Vector3 hitDirection, float impactForce)
    {
        // Play blocked hit animation
        animator.SetTrigger("BlockedHit");
        
        // Play sound effect
        if (blockedHitSounds.Length > 0)
        {
            AudioClip sound = blockedHitSounds[Random.Range(0, blockedHitSounds.Length)];
            audioSource.PlayOneShot(sound, Mathf.Clamp01(impactForce / 10f));
        }
        
        // Spawn visual effect
        if (hitEffectPrefab != null)
        {
            GameObject effect = Instantiate(hitEffectPrefab, hitPoint, Quaternion.LookRotation(hitDirection));
            Destroy(effect, 1f);
        }
        
        // Apply slight knockback even when blocked
        if (rb != null)
        {
            rb.AddForce(hitDirection * impactForce * knockbackMultiplier * 0.3f, ForceMode.Impulse);
        }
    }
    
    private void ProcessGuardBreak(Vector3 hitPoint, Vector3 hitDirection, float impactForce)
    {
        // Play guard break animation
        animator.SetTrigger("GuardBreak");
        
        // Play sound effect
        if (guardBreakSounds.Length > 0)
        {
            AudioClip sound = guardBreakSounds[Random.Range(0, guardBreakSounds.Length)];
            audioSource.PlayOneShot(sound, Mathf.Clamp01(impactForce / 10f));
        }
        
        // Spawn visual effect
        if (hitEffectPrefab != null)
        {
            GameObject effect = Instantiate(hitEffectPrefab, hitPoint, Quaternion.LookRotation(hitDirection));
            Destroy(effect, 1f);
        }
        
        // Apply stronger knockback for guard break
        if (rb != null)
        {
            rb.AddForce(hitDirection * impactForce * knockbackMultiplier * 0.7f, ForceMode.Impulse);
        }
        
        // Make the enemy vulnerable for a short time
        StartCoroutine(TemporaryVulnerability(0.8f));
    }
    
    private void ProcessCleanHit(HitZone zone, Vector3 hitPoint, Vector3 hitDirection, float impactForce, int damage)
    {
        // Send damage to the BoxingEnemyAI
        if (enemyAI != null)
        {
            bool isCounterHit = animator.GetCurrentAnimatorStateInfo(0).IsName("Attacking");
            enemyAI.TakeDamage(damage, hitDirection, isCounterHit);
        }
        
        // Determine hit type
        HitType hitType = DetermineHitType(impactForce);
        
        // Trigger appropriate hit reaction animation
        TriggerHitAnimation(zone, hitType);
        
        // Play appropriate sound effect
        PlayHitSound(zone, hitType, impactForce);
        
        // Spawn visual effect
        SpawnHitEffect(zone, hitPoint, hitDirection, hitType);
        
        // Apply physics force for knockback
        ApplyKnockbackForce(hitDirection, impactForce, hitType);
        
        // Handle critical hits
        if (hitType == HitType.Critical)
        {
            // Possible stun or knockdown
            if (Random.value < 0.3f)
            {
                StartCoroutine(KnockdownSequence(hitDirection));
            }
        }
    }
    
    private enum HitType
    {
        Light,
        Medium,
        Heavy,
        Critical
    }
    
    private HitType DetermineHitType(float impactForce)
    {
        if (impactForce >= criticalHitThreshold)
            return HitType.Critical;
        else if (impactForce >= heavyHitThreshold)
            return HitType.Heavy;
        else if (impactForce >= minImpactForce * 2)
            return HitType.Medium;
        else
            return HitType.Light;
    }
    
    private void TriggerHitAnimation(HitZone zone, HitType hitType)
    {
        if (animator == null) return;
        
        // If the zone has a specific hit reaction trigger, use that
        if (!string.IsNullOrEmpty(zone.hitReactionTrigger))
        {
            animator.SetTrigger(zone.hitReactionTrigger);
            return;
        }
        
        // Otherwise fall back to generic hit reactions based on hit type and zone
        string triggerName = "TakeHit";
        
        if (zone.zoneName.Contains("Head"))
        {
            if (hitType == HitType.Critical || hitType == HitType.Heavy)
                triggerName = "HeavyHeadHit";
            else
                triggerName = "LightHeadHit";
        }
        else if (zone.zoneName.Contains("Body"))
        {
            if (hitType == HitType.Critical || hitType == HitType.Heavy)
                triggerName = "HeavyBodyHit";
            else
                triggerName = "LightBodyHit";
        }
        
        animator.SetTrigger(triggerName);
    }
    
    private void PlayHitSound(HitZone zone, HitType hitType, float impactForce)
    {
        if (audioSource == null) return;
        
        AudioClip[] soundArray;
        
        // Choose sound array based on hit zone
        if (zone.zoneName.Contains("Head"))
            soundArray = headHitSounds;
        else
            soundArray = bodyHitSounds;
        
        // If we have sounds to play
        if (soundArray != null && soundArray.Length > 0)
        {
            AudioClip sound = soundArray[Random.Range(0, soundArray.Length)];
            float volume = Mathf.Clamp01(impactForce / 10f);
            audioSource.PlayOneShot(sound, volume);
        }
    }
    
    private void SpawnHitEffect(HitZone zone, Vector3 hitPoint, Vector3 hitDirection, HitType hitType)
    {
        if (hitEffectPrefab == null) return;
        
        // Create base hit effect
        GameObject effect = Instantiate(hitEffectPrefab, hitPoint, Quaternion.LookRotation(hitDirection));
        
        // Scale effect based on hit type
        float scaleMultiplier = 1.0f;
        switch (hitType)
        {
            case HitType.Light:
                scaleMultiplier = 0.7f;
                break;
            case HitType.Medium:
                scaleMultiplier = 1.0f;
                break;
            case HitType.Heavy:
                scaleMultiplier = 1.5f;
                break;
            case HitType.Critical:
                scaleMultiplier = 2.0f;
                // Add sweat/blood effect for critical hits
                if (sweatEffectPrefab != null)
                {
                    GameObject sweatEffect = Instantiate(sweatEffectPrefab, hitPoint, Quaternion.LookRotation(hitDirection));
                    Destroy(sweatEffect, 1.5f);
                }
                break;
        }
        
        effect.transform.localScale *= scaleMultiplier;
        Destroy(effect, 1f);
    }
    
    private void ApplyKnockbackForce(Vector3 hitDirection, float impactForce, HitType hitType)
    {
        if (rb == null) return;
        
        // Calculate knockback force based on hit type
        float forceMultiplier = knockbackMultiplier;
        switch (hitType)
        {
            case HitType.Light:
                forceMultiplier *= 0.5f;
                break;
            case HitType.Medium:
                forceMultiplier *= 1.0f;
                break;
            case HitType.Heavy:
                forceMultiplier *= 2.0f;
                break;
            case HitType.Critical:
                forceMultiplier *= 3.0f;
                break;
        }
        
        // Apply the force
        rb.AddForce(hitDirection * impactForce * forceMultiplier, ForceMode.Impulse);
    }
    
    private IEnumerator TemporaryVulnerability(float duration)
    {
        // Notify AI that it's vulnerable
        if (enemyAI != null)
        {
            // You'll need to add this method to your BoxingEnemyAI class
            enemyAI.NotifyVulnerable(duration);
        }
        
        yield return new WaitForSeconds(duration);
    }
    
    private IEnumerator KnockdownSequence(Vector3 hitDirection)
    {
        // Trigger knockdown animation
        animator.SetTrigger("Knockdown");
        
        // Disable AI temporarily
        if (enemyAI != null)
        {
            enemyAI.enabled = false;
        }
        
        // Apply large knockback force
        if (rb != null)
        {
            rb.AddForce(hitDirection * criticalHitThreshold * knockbackMultiplier * 2f, ForceMode.Impulse);
        }
        
        // Show stars effect if available
        if (knockoutStarsPrefab != null && headTransform != null)
        {
            GameObject stars = Instantiate(knockoutStarsPrefab, headTransform.position + Vector3.up * 0.5f, Quaternion.identity);
            stars.transform.SetParent(headTransform);
            Destroy(stars, 3f);
        }
        
        // Wait for fighter to get back up
        float recoveryTime = Random.Range(2.5f, 4.0f);
        yield return new WaitForSeconds(recoveryTime);
        
        // Trigger get up animation
        animator.SetTrigger("GetUp");
        
        yield return new WaitForSeconds(1.5f);
        
        // Re-enable AI
        if (enemyAI != null)
        {
            enemyAI.enabled = true;
            
            // Make sure AI retreats after getting up
            enemyAI.ForceRetreat();
        }
    }
    
    private void UpdateCombo()
    {
        // Check if this hit is within the combo window
        if (Time.time - lastHitTime <= COMBO_TIME_WINDOW)
        {
            comboCounter++;
            
            // Trigger combo counter UI update here - you would need to implement this
            if (comboCounter >= 3)
            {
                // You could send an event to update UI
                Debug.Log($"Combo x{comboCounter}!");
            }
        }
        else
        {
            // Reset combo
            comboCounter = 1;
        }
        
        // Update last hit time
        lastHitTime = Time.time;
    }
    
    // This method is called from Unity Editor to visualize hit zones
    private void OnDrawGizmosSelected()
    {
        // Draw hit zones
        if (hitZones != null)
        {
            foreach (var zone in hitZones)
            {
                if (zone.zoneTransform == null) continue;
                
                // Draw different colors for different zone types
                if (zone.zoneName.Contains("Head"))
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(zone.zoneTransform.position, headHitRadius);
                }
                else
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(zone.zoneTransform.position, bodyHitRadius);
                }
            }
        }
    }
}