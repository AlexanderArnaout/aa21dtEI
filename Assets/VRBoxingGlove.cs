using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(Rigidbody))]
public class VRBoxingGlove : MonoBehaviour
{
    [Header("Punch Detection")]
    [SerializeField] private float minPunchVelocity = 1.5f;
    [SerializeField] private float maxPunchVelocity = 8.0f;
    [SerializeField] private LayerMask hitLayerMask;
    [SerializeField] private Transform punchImpactPoint;
    
    [Header("Haptic Feedback")]
    [SerializeField] private XRBaseController xrController;
    [SerializeField] private float hapticAmplitude = 0.7f;
    [SerializeField] private float hapticDuration = 0.1f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] swingSounds;
    [SerializeField] private AudioClip[] impactSounds;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject swingTrailEffect;
    [SerializeField] private ParticleSystem impactParticles;
    
    [Header("Settings")]
    [SerializeField] private string gloveHand = "Right"; // "Right" or "Left"
    [SerializeField] private float punchCooldown = 0.1f;
    
    // Internal variables
    private Rigidbody rb;
    private Vector3 previousPosition;
    private Vector3 currentVelocity;
    private bool isPunching = false;
    private float cooldownTimer = 0f;
    private bool isColliding = false;
    private List<Collider> ignoredColliders = new List<Collider>();
    
    // For calculating punch force
    private List<Vector3> velocityHistory = new List<Vector3>();
    private int velocityHistorySize = 5;
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true; // Let the XR system handle the movement
        previousPosition = transform.position;
        
        // Configure the collider
        Collider gloveCollider = GetComponent<Collider>();
        if (gloveCollider != null)
        {
            gloveCollider.isTrigger = true;
        }
        
        // Initialize audio source if needed
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f;
        }
        
        // Turn off trail effect at start
        if (swingTrailEffect != null)
        {
            swingTrailEffect.SetActive(false);
        }
        
        // Subscribe to the haptic feedback event
        BoxingHitDetection.OnHapticFeedback += ReceiveHapticFeedback;
        
        // Initialize velocity history
        for (int i = 0; i < velocityHistorySize; i++)
        {
            velocityHistory.Add(Vector3.zero);
        }
    }
    
    void OnDestroy()
    {
        // Unsubscribe from haptic event
        BoxingHitDetection.OnHapticFeedback -= ReceiveHapticFeedback;
    }
    
    void Update()
    {
        // Calculate velocity
        currentVelocity = (transform.position - previousPosition) / Time.deltaTime;
        previousPosition = transform.position;
        
        // Update velocity history
        velocityHistory.RemoveAt(0);
        velocityHistory.Add(currentVelocity);
        
        // Calculate the average velocity
        Vector3 averageVelocity = Vector3.zero;
        foreach (var velocity in velocityHistory)
        {
            averageVelocity += velocity;
        }
        averageVelocity /= velocityHistory.Count;
        
        // Check for punch velocity threshold
        float speed = averageVelocity.magnitude;
        
        // Handle cooldown timer
        if (cooldownTimer > 0)
        {
            cooldownTimer -= Time.deltaTime;
        }
        
        // Check if we're starting a punch motion
        if (speed > minPunchVelocity && !isPunching && cooldownTimer <= 0)
        {
            StartPunch();
        }
        
        // Update trail effect
        if (swingTrailEffect != null)
        {
            swingTrailEffect.SetActive(isPunching);
        }
    }
    
    void StartPunch()
    {
        isPunching = true;
        isColliding = false;
        
        // Play swing sound
        if (swingSounds.Length > 0 && audioSource != null)
        {
            AudioClip swingSound = swingSounds[Random.Range(0, swingSounds.Length)];
            audioSource.PlayOneShot(swingSound, 0.7f);
        }
        
        // Reset the list of ignored colliders
        ignoredColliders.Clear();
    }
    
    void EndPunch()
    {
        isPunching = false;
        cooldownTimer = punchCooldown;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Check if we're actively punching
        if (!isPunching || isColliding) return;
        
        // Check if we already hit this collider during this punch
        if (ignoredColliders.Contains(other)) return;
        
        // Check if this is something we can hit
        if (!IsHittable(other)) return;
        
        // Add to ignored colliders to prevent multiple hits in same punch
        ignoredColliders.Add(other);
        
        // Calculate impact force based on velocity
        float impactSpeed = currentVelocity.magnitude;
        float impactForce = CalculateImpactForce(impactSpeed);
        
        // Find impact point
        Vector3 impactPoint = punchImpactPoint != null ? 
            punchImpactPoint.position : 
            other.ClosestPoint(transform.position);
        
        // Get impact direction
        Vector3 impactDirection = currentVelocity.normalized;
        
        // Play impact sound
        PlayImpactSound(impactForce);
        
        // Spawn impact effects
        ShowImpactEffects(impactPoint, impactDirection);
        
        // Apply haptic feedback
        ApplyHapticFeedback(impactForce);
        
        // Get the enemy component
        BoxingHitDetection enemyHitSystem = other.GetComponentInParent<BoxingHitDetection>();
        if (enemyHitSystem != null)
        {
            // Register the hit with the enemy
            enemyHitSystem.RegisterPunchHit(impactPoint, impactDirection, impactForce, gloveHand);
            
            // Flag as colliding to prevent multiple hits in the same frame
            isColliding = true;
            
            // End punch after successful hit
            EndPunch();
        }
    }
    
    private bool IsHittable(Collider other)
    {
        // Check layer
        if (!hitLayerMask.Contains(other.gameObject.layer))
            return false;
            
        // Additional checks as needed (e.g., tag checks)
        return true;
    }
    
    private float CalculateImpactForce(float speed)
    {
        // Calculate impact force based on punch speed, clamping between min and max values
        float normalizedSpeed = Mathf.Clamp01((speed - minPunchVelocity) / (maxPunchVelocity - minPunchVelocity));
        return Mathf.Lerp(1f, 10f, normalizedSpeed); // Map to a 1-10 force scale
    }
    
    private void PlayImpactSound(float impactForce)
    {
        if (impactSounds.Length == 0 || audioSource == null) return;
        
        AudioClip impactSound = impactSounds[Random.Range(0, impactSounds.Length)];
        float volume = Mathf.Clamp01(impactForce / 10f);
        audioSource.PlayOneShot(impactSound, volume);
    }
    
    private void ShowImpactEffects(Vector3 position, Vector3 direction)
    {
        // Show impact particles
        if (impactParticles != null)
        {
            impactParticles.transform.position = position;
            impactParticles.transform.rotation = Quaternion.LookRotation(direction);
            impactParticles.Play();
        }
    }
    
    private void ApplyHapticFeedback(float intensity)
    {
        if (xrController == null) return;
        
        float hapticIntensity = hapticAmplitude * Mathf.Clamp01(intensity / 10f);
        xrController.SendHapticImpulse(hapticIntensity, hapticDuration);
    }
    
    private void ReceiveHapticFeedback(float intensity, float duration)
    {
        if (xrController == null) return;
        xrController.SendHapticImpulse(intensity, duration);
    }
    
    // Method to be called at the end of punch animation or after timeout
    private void OnPunchCompleted()
    {
        if (isPunching)
        {
            EndPunch();
        }
    }
    
    // Called via animation event or after a certain time
    public void ResetPunch()
    {
        isPunching = false;
        isColliding = false;
        ignoredColliders.Clear();
    }
    
    // Add a method to make the enemy AI script accessible to your BoxingEnemyAI script
    private void NotifyEnemyOfPlayerAttack()
    {
        // Find all enemies in the scene
        BoxingEnemyAI[] enemies = FindObjectsOfType<BoxingEnemyAI>();
        foreach (var enemy in enemies)
        {
            // Notify each enemy that the player is attacking
            enemy.NotifyPlayerAttacking();
        }
    }
}