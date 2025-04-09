using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VRHandCollider : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private string handType = "Right"; // "Right" or "Left"
    [SerializeField] private float punchForceThreshold = 1.5f; // Minimum velocity to register as a punch
    [SerializeField] private float punchCooldown = 0.3f; // Time between punches
    
    [Header("References")]
    [SerializeField] private Transform trackedObject; // VR controller/hand transform
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    
    // Internal tracking
    private Vector3 previousPosition;
    private Vector3 velocity;
    private float cooldownTimer = 0f;
    private bool canPunch = true;
    
    // Cached components
    private Collider handCollider;
    
    void Start()
    {
        // Cache components
        handCollider = GetComponent<Collider>();
        if (handCollider == null)
        {
            // Add a sphere collider if there isn't one
            handCollider = gameObject.AddComponent<SphereCollider>();
            ((SphereCollider)handCollider).radius = 0.08f; // Adjust size as needed
        }
        
        // Set to trigger so it doesn't push objects
        handCollider.isTrigger = true;
        
        // Initialize position tracking
        if (trackedObject == null)
        {
            trackedObject = transform;
        }
        previousPosition = trackedObject.position;
        
        // Log setup info
        if (showDebugInfo)
        {
            Debug.Log($"VR {handType} Hand Collider initialized");
        }
    }
    
    void Update()
    {
        // Track velocity
        velocity = (trackedObject.position - previousPosition) / Time.deltaTime;
        previousPosition = trackedObject.position;
        
        // Handle cooldown
        if (!canPunch)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0)
            {
                canPunch = true;
            }
        }
        
        // Debug info
        if (showDebugInfo)
        {
            float speed = velocity.magnitude;
            if (speed > punchForceThreshold)
            {
                Debug.Log($"{handType} hand velocity: {speed}");
            }
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        // Only register hits when we can punch
        if (!canPunch) return;
        
        // Check punch velocity
        float punchForce = velocity.magnitude;
        if (punchForce < punchForceThreshold) return;
        
        // Check if we hit something with a damage receiver
        DamageReceiver damageReceiver = other.GetComponentInParent<DamageReceiver>();
        if (damageReceiver != null)
        {
            // Calculate damage based on velocity
            float rawDamage = punchForce * 10f; // Adjust multiplier as needed
            int damage = Mathf.RoundToInt(rawDamage);
            
            // Cap damage at a reasonable amount
            damage = Mathf.Min(damage, 30);
            
            // Create hit info
            HitInfo hitInfo = new HitInfo
            {
                damage = damage,
                hitPosition = other.ClosestPoint(transform.position),
                hitDirection = velocity.normalized,
                force = punchForce,
                handType = handType
            };
            
            // Apply damage
            damageReceiver.TakeDamage(hitInfo);
            
            // Debug output
            if (showDebugInfo)
            {
                Debug.Log($"{handType} hand hit {other.gameObject.name} for {damage} damage!");
            }
            
            // Start cooldown
            cooldownTimer = punchCooldown;
            canPunch = false;
        }
    }
    
    // Helper method to visualize the collider
    void OnDrawGizmos()
    {
        if (!showDebugInfo) return;
        
        Gizmos.color = handType == "Right" ? Color.blue : Color.green;
        
        // Draw different gizmo based on collider type
        if (handCollider is SphereCollider)
        {
            SphereCollider sphere = handCollider as SphereCollider;
            Gizmos.DrawWireSphere(transform.position, sphere.radius);
        }
        else if (handCollider is BoxCollider)
        {
            BoxCollider box = handCollider as BoxCollider;
            Gizmos.DrawWireCube(transform.position, box.size);
        }
    }
}

// Hit information struct
[System.Serializable]
public struct HitInfo
{
    public int damage;
    public Vector3 hitPosition;
    public Vector3 hitDirection;
    public float force;
    public string handType;
}