using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Rigidbody))]
public class PlayerVRBoxing : MonoBehaviour
{
    [Header("Player Stats")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth;
    [SerializeField] private int maxStamina = 100;
    [SerializeField] private int currentStamina;
    [SerializeField] private float staminaRegenRate = 5f;
    [SerializeField] private float staminaRegenDelay = 1.5f;
    private float lastStaminaUseTime;

    [Header("Movement")]
    [SerializeField] private float movementSpeed = 1.0f;
    [SerializeField] private Transform playerCamera;
    [SerializeField] private XRNode movementSource = XRNode.LeftHand;
    private CharacterController characterController;
    private bool isMoving = false;

    [Header("Guard")]
    [SerializeField] private Transform leftGuardPosition;
    [SerializeField] private Transform rightGuardPosition;
    [SerializeField] private float guardEffectiveDistance = 0.2f;
    [SerializeField] private GameObject guardVisualEffect;
    private bool isBlocking = false;
    
    [Header("Combat Stats")]
    [SerializeField] private float vulnerabilityDuration = 0.5f;
    [SerializeField] private float knockbackResistance = 0.7f;
    private bool isVulnerable = false;
    private bool isStunned = false;
    private List<BoxingEnemyAI> activeEnemies = new List<BoxingEnemyAI>();

    [Header("UI References")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI staminaText;
    [SerializeField] private Image damageVignette;
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] hitSounds;
    [SerializeField] private AudioClip[] blockSounds;
    [SerializeField] private AudioClip heavyDamageSound;
    [SerializeField] private AudioClip knockoutSound;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem damageParticles;
    [SerializeField] private GameObject knockdownEffect;
    
    // VR Tracking
    private InputDevice leftHandDevice;
    private InputDevice rightHandDevice;
    private List<InputDevice> devices = new List<InputDevice>();
    private Vector2 inputAxis;
    
    // Referenced components
    private Rigidbody rb;
    private Animator animator;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
        
        if (characterController == null)
        {
            characterController = gameObject.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.3f;
        }
        
        // Initialize stats
        currentHealth = maxHealth;
        currentStamina = maxStamina;
    }
    
    void Start()
    {
        // Initialize UI if assigned
        if (healthSlider != null) healthSlider.maxValue = maxHealth;
        if (staminaSlider != null) staminaSlider.maxValue = maxStamina;
        UpdateUI();
        
        // Find audioSource if not assigned
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        // Start stamina regen
        StartCoroutine(RegenerateStamina());
        
        // Find all enemies in the scene
        FindEnemies();
        
        // Reset damage vignette
        if (damageVignette != null)
        {
            Color c = damageVignette.color;
            c.a = 0;
            damageVignette.color = c;
        }
    }
    
    void Update()
    {
        // Get VR input devices
        GetDevices();
        
        // Handle movement
        HandleMovement();
        
        // Check for guard position
        CheckGuardPosition();
    }
    
    void GetDevices()
    {
        // Get input devices if not already acquired
        if (!leftHandDevice.isValid || !rightHandDevice.isValid)
        {
            InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, devices);
            if (devices.Count > 0) leftHandDevice = devices[0];
            
            InputDevices.GetDevicesAtXRNode(XRNode.RightHand, devices);
            if (devices.Count > 0) rightHandDevice = devices[0];
        }
        
        // Get movement input
        if (movementSource == XRNode.LeftHand && leftHandDevice.isValid)
        {
            leftHandDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out inputAxis);
        }
        else if (movementSource == XRNode.RightHand && rightHandDevice.isValid)
        {
            rightHandDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out inputAxis);
        }
    }
    
    void HandleMovement()
    {
        if (characterController == null || playerCamera == null) return;
        
        // Get movement direction
        Vector3 direction = new Vector3(inputAxis.x, 0, inputAxis.y);
        
        // Check if moving
        isMoving = direction.magnitude > 0.1f;
        
        if (isMoving)
        {
            // Transform direction to world space
            direction = playerCamera.TransformDirection(direction);
            direction.y = 0; // Keep movement on ground plane
            direction.Normalize();
            
            // Move the character
            characterController.Move(direction * movementSpeed * Time.deltaTime);
            
            // Rotate towards movement direction
            if (direction != Vector3.zero)
            {
                transform.forward = Vector3.Slerp(transform.forward, direction, 0.15f);
            }
        }
        
        // Apply gravity
        characterController.Move(Vector3.down * 9.81f * Time.deltaTime);
        
        // Update animator if assigned
        if (animator != null)
        {
            animator.SetBool("IsMoving", isMoving);
        }
    }
    
    void CheckGuardPosition()
    {
        if (leftGuardPosition == null || rightGuardPosition == null) return;
        
        // Calculate distance between hands and guard positions
        float leftDistance = Vector3.Distance(leftGuardPosition.position, leftHandDevice.position);
        float rightDistance = Vector3.Distance(rightGuardPosition.position, rightHandDevice.position);
        
        // Check if both hands are in guard position
        bool guardsUp = leftDistance < guardEffectiveDistance && rightDistance < guardEffectiveDistance;
        
        // If guard state changed
        if (guardsUp != isBlocking)
        {
            isBlocking = guardsUp;
            
            // Update visuals
            if (guardVisualEffect != null)
            {
                guardVisualEffect.SetActive(isBlocking);
            }
            
            // Update animator
            if (animator != null)
            {
                animator.SetBool("IsBlocking", isBlocking);
            }
        }
    }
    
    public void UseStamina(int amount)
    {
        // Record when stamina was used
        lastStaminaUseTime = Time.time;
        
        // Reduce stamina
        currentStamina = Mathf.Max(0, currentStamina - amount);
        
        // Update UI
        UpdateUI();
    }
    
    private IEnumerator RegenerateStamina()
    {
        while (true)
        {
            // Wait for the delay after last stamina use
            if (Time.time > lastStaminaUseTime + staminaRegenDelay && currentStamina < maxStamina)
            {
                // Regenerate stamina
                currentStamina = Mathf.Min(maxStamina, currentStamina + Mathf.RoundToInt(staminaRegenRate * Time.deltaTime));
                
                // Update UI
                UpdateUI();
            }
            
            yield return null;
        }
    }
    
    public void TakeDamage(int damageAmount, Vector3 impactDirection)
    {
        // Check if blocking and calculate block effectiveness
        if (isBlocking && !isVulnerable)
        {
            // Calculate how effective the block is based on direction
            float blockDot = Vector3.Dot(transform.forward, -impactDirection.normalized);
            
            if (blockDot > 0.5f)
            {
                // Good block (from the front)
                damageAmount = Mathf.RoundToInt(damageAmount * 0.2f);
                PlayBlockSound();
            }
            else
            {
                // Partial block (from sides)
                damageAmount = Mathf.RoundToInt(damageAmount * 0.6f);
                PlayHitSound(damageAmount);
            }
        }
        else
        {
            // Not blocking, take full damage
            PlayHitSound(damageAmount);
        }
        
        // Apply damage
        currentHealth = Mathf.Max(0, currentHealth - damageAmount);
        
        // Update UI
        UpdateUI();
        ShowDamageEffect(damageAmount);
        
        // Apply knockback force
        if (rb != null)
        {
            float knockbackForce = damageAmount * (1 - knockbackResistance);
            rb.AddForce(impactDirection.normalized * knockbackForce, ForceMode.Impulse);
        }
        
        // Check for vulnerability
        if (damageAmount > 10 && !isVulnerable)
        {
            StartCoroutine(BecomeVulnerable(vulnerabilityDuration));
        }
        
        // Check for knockout
        if (currentHealth <= 0)
        {
            StartCoroutine(Knockout());
        }
    }
    
    private void PlayHitSound(int damage)
    {
        if (audioSource == null || hitSounds == null || hitSounds.Length == 0) return;
        
        if (damage > 20 && heavyDamageSound != null)
        {
            audioSource.PlayOneShot(heavyDamageSound, 1.0f);
        }
        else
        {
            AudioClip hitSound = hitSounds[Random.Range(0, hitSounds.Length)];
            audioSource.PlayOneShot(hitSound, Mathf.Clamp01(damage / 20f));
        }
    }
    
    private void PlayBlockSound()
    {
        if (audioSource == null || blockSounds == null || blockSounds.Length == 0) return;
        
        AudioClip blockSound = blockSounds[Random.Range(0, blockSounds.Length)];
        audioSource.PlayOneShot(blockSound, 0.7f);
    }
    
    private void UpdateUI()
    {
        // Update health UI
        if (healthSlider != null) healthSlider.value = currentHealth;
        if (healthText != null) healthText.text = currentHealth.ToString();
        
        // Update stamina UI
        if (staminaSlider != null) staminaSlider.value = currentStamina;
        if (staminaText != null) staminaText.text = currentStamina.ToString();
    }
    
    private void ShowDamageEffect(int damageAmount)
    {
        // Show damage particles
        if (damageParticles != null && damageAmount > 5)
        {
            damageParticles.Play();
        }
        
        // Show damage vignette
        if (damageVignette != null)
        {
            StartCoroutine(FlashDamageVignette(damageAmount));
        }
    }
    
    private IEnumerator FlashDamageVignette(int damageAmount)
    {
        // Calculate intensity based on damage
        float intensity = Mathf.Clamp01(damageAmount / 30f);
        
        // Fade in
        float fadeTime = 0.1f;
        float timer = 0;
        Color startColor = damageVignette.color;
        Color targetColor = new Color(startColor.r, startColor.g, startColor.b, intensity);
        
        while (timer < fadeTime)
        {
            timer += Time.deltaTime;
            damageVignette.color = Color.Lerp(startColor, targetColor, timer / fadeTime);
            yield return null;
        }
        
        // Hold for a moment
        yield return new WaitForSeconds(0.2f);
        
        // Fade out
        timer = 0;
        float fadeDuration = 0.5f;
        startColor = damageVignette.color;
        targetColor = new Color(startColor.r, startColor.g, startColor.b, 0);
        
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            damageVignette.color = Color.Lerp(startColor, targetColor, timer / fadeDuration);
            yield return null;
        }
    }
    
    private IEnumerator BecomeVulnerable(float duration)
    {
        isVulnerable = true;
        
        // You could add visual effect to show vulnerability
        
        yield return new WaitForSeconds(duration);
        
        isVulnerable = false;
    }
    
    private IEnumerator Knockout()
    {
        // Player is knocked out
        isStunned = true;
        
        // Play knockout sound
        if (audioSource != null && knockoutSound != null)
        {
            audioSource.PlayOneShot(knockoutSound);
        }
        
        // Show knockout effect
        if (knockdownEffect != null)
        {
            knockdownEffect.SetActive(true);
        }
        
        // Trigger knockout animation
        if (animator != null)
        {
            animator.SetTrigger("Knockout");
        }
        
        // Notify enemies that player is knocked out
        foreach (var enemy in activeEnemies)
        {
            if (enemy != null)
            {
                // You'd need to add this method to your enemy AI
                enemy.PlayerKnockedOut();
            }
        }
        
        // Wait for a moment
        yield return new WaitForSeconds(3.0f);
        
        // Show game over UI or restart prompt
        // GameManager.Instance.ShowGameOver();
        
        // For now, just restore some health to let the player continue
        currentHealth = maxHealth / 2;
        UpdateUI();
        
        // Hide knockout effect
        if (knockdownEffect != null)
        {
            knockdownEffect.SetActive(false);
        }
        
        isStunned = false;
    }
    
    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        UpdateUI();
    }
    
    public void RestoreStamina(int amount)
    {
        currentStamina = Mathf.Min(maxStamina, currentStamina + amount);
        UpdateUI();
    }
    
    public bool IsBlocking()
    {
        return isBlocking;
    }
    
    public bool IsVulnerable()
    {
        return isVulnerable;
    }
    
    public bool IsStunned()
    {
        return isStunned;
    }
    
    private void FindEnemies()
    {
        BoxingEnemyAI[] enemies = FindObjectsOfType<BoxingEnemyAI>();
        activeEnemies.AddRange(enemies);
    }
    
    // Called when a new enemy spawns
    public void RegisterEnemy(BoxingEnemyAI enemy)
    {
        if (!activeEnemies.Contains(enemy))
        {
            activeEnemies.Add(enemy);
        }
    }
    
    // Called when an enemy is defeated
    public void UnregisterEnemy(BoxingEnemyAI enemy)
    {
        if (activeEnemies.Contains(enemy))
        {
            activeEnemies.Remove(enemy);
        }
    }
    
    // Add a method for the enemy to check if the player is in a vulnerable state
    public void MakeVulnerable(float duration)
    {
        StartCoroutine(BecomeVulnerable(duration));
        
        // Notify enemies
        foreach (var enemy in activeEnemies)
        {
            if (enemy != null)
            {
                enemy.NotifyPlayerVulnerable(duration);
            }
        }
    }
}