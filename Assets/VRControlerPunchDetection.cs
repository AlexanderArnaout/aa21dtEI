using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

public class VRPunchDetector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private XRController controller;
    [SerializeField] private PlayerVRBoxing playerScript;
    
    [Header("Punch Settings")]
    [SerializeField] private float punchThreshold = 2.0f;
    [SerializeField] private int staminaCost = 5;
    [SerializeField] private string handType = "Right"; // "Right" or "Left"
    
    [Header("Effects")]
    [SerializeField] private TrailRenderer punchTrail;
    [SerializeField] private ParticleSystem punchEffect;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] punchSounds;
    
    // Tracking
    private Vector3 previousPosition;
    private Vector3 velocity;
    private bool isPunching = false;
    private float cooldownTimer = 0f;
    private const float PUNCH_COOLDOWN = 0.2f;
    
    // Reference for the hit detection
    private BoxingHitDetection currentTarget;
    
    void Start()
    {
        previousPosition = transform.position;
        
        // Disable trail at start
        if (punchTrail != null)
        {
            punchTrail.emitting = false;
        }
        
        // Create audio source if needed
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }
    
    void Update()
    {
        // Calculate velocity
        velocity = (transform.position - previousPosition) / Time.deltaTime;
        previousPosition = transform.position;
        
        // Handle cooldown
        if (cooldownTimer > 0)
        {
            cooldownTimer -= Time.deltaTime;
        }
        
        // Detect punch motion
        float speed = velocity.magnitude;
        
        // Start punch if speed threshold is met and not in cooldown
        if (speed > punchThreshold && !isPunching && cooldownTimer <= 0)
        {
            StartPunch();
        }
        
        // Update effects
        if (punchTrail != null)
        {
            punchTrail.emitting = isPunching;
        }
    }
    
    private void StartPunch()
    {
        // Check if player has enough stamina
        if (playerScript != null && playerScript.currentStamina < staminaCost)
        {
            // Not enough stamina
            return;
        }
        
        isPunching = true;
        
        // Use stamina
        if (playerScript != null)
        {
            playerScript.UseStamina(staminaCost);
        }
        
        // Play sound
        if (audioSource != null && punchSounds.Length > 0)
        {
            AudioClip sound = punchSounds[Random.Range(0, punchSounds.Length)];
            audioSource.PlayOneShot(sound, 0.7f);
        }
        
        // Play punch effect
        if (punchEffect != null)
        {
            punchEffect.Play();
        }
        
        // Apply haptic feedback
        ApplyHapticFeedback(0.5f, 0.1f);
        
        // End punch after a short time
        StartCoroutine(EndPunchAfterDelay());
    }
    
    private IEnumerator EndPunchAfterDelay()
    {
        yield return new WaitForSeconds(0.2f);
        isPunching = false;
        cooldownTimer = PUNCH_COOLDOWN;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Only register hits when punching
        if (!isPunching) return;
        
        // Check if we hit an enemy with hit detection
        BoxingHitDetection hitSystem = other.GetComponentInParent<BoxingHitDetection>();
        if (hitSystem != null)
        {
            // Calculate hit point
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            
            // Register the hit
            hitSystem.RegisterPunchHit(hitPoint, velocity.normalized, velocity.magnitude, handType);
            
            // Apply haptic feedback
            ApplyHapticFeedback(1.0f, 0.2f);
            
            // End the punch (to prevent multiple hits)
            isPunching = false;
            cooldownTimer = PUNCH_COOLDOWN;
        }
    }
    
    private void ApplyHapticFeedback(float intensity, float duration)
    {
        if (controller != null)
        {
            controller.SendHapticImpulse(intensity, duration);
        }
    }
}