using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Animator))]
public class BoxingEnemyAI : MonoBehaviour
{
    // References
    private Animator animator;
    private Transform player;
    private NavMeshAgent navMeshAgent;
    
    // AI State Variables
    [Header("AI Behavior Settings")]
    [SerializeField] private float minAttackDistance = 1.5f;
    [SerializeField] private float maxAttackDistance = 2.5f;
    [SerializeField] private float optimalDistance = 2.0f;
    [SerializeField] private float circlingSpeed = 1.0f;
    [SerializeField] private float aggressiveness = 0.5f; // 0 to 1
    [SerializeField] private float defensiveness = 0.5f; // 0 to 1
    [SerializeField] private float reactionTime = 0.2f; // seconds
    [SerializeField] private float difficultyLevel = 0.5f; // 0 to 1
    
    // Combat Stats
    [Header("Combat Stats")]
    [SerializeField] private int health = 100;
    [SerializeField] private int stamina = 100;
    [SerializeField] private float staminaRegenRate = 5f;
    [SerializeField] private float punchForce = 10f;
    
    // Punch Data
    [System.Serializable]
    public class PunchData
    {
        public string animationTrigger;
        public int damageAmount;
        public float staminaCost;
        public float cooldown;
        public float range;
    }
    
    [Header("Punch Configuration")]
    [SerializeField] private PunchData jab;
    [SerializeField] private PunchData cross;
    [SerializeField] private PunchData hook;
    [SerializeField] private PunchData uppercut;
    [SerializeField] private List<PunchData> combos = new List<PunchData>();
    
    // Private state variables
    private float lastPunchTime = 0f;
    private float currentCooldown = 0f;
    private bool isBlocking = false;
    private bool isStunned = false;
    private bool isDodging = false;
    private BoxingState currentState = BoxingState.Circling;
    private Vector3 circlingPoint;
    private float circlingAngle = 0f;
    private Coroutine currentActionCoroutine;
    private float timeToNextDecision = 0f;
    private bool playerInVulnerableState = false;
    private bool playerAttacking = false;
    
    // Enum for state machine
    private enum BoxingState
    {
        Circling,
        Approaching,
        Retreating,
        Attacking,
        Blocking,
        Stunned,
        Dodging
    }
    
    void Start()
    {
        animator = GetComponent<Animator>();
        navMeshAgent = GetComponent<NavMeshAgent>();
        player = GameObject.FindGameObjectWithTag("Player").transform;
        
        // Initialize punch data if null
        if (jab == null) jab = new PunchData { animationTrigger = "Jab", damageAmount = 5, staminaCost = 5, cooldown = 0.5f, range = 1.8f };
        if (cross == null) cross = new PunchData { animationTrigger = "Cross", damageAmount = 10, staminaCost = 10, cooldown = 0.8f, range = 2.0f };
        if (hook == null) hook = new PunchData { animationTrigger = "Hook", damageAmount = 15, staminaCost = 15, cooldown = 1.0f, range = 1.6f };
        if (uppercut == null) uppercut = new PunchData { animationTrigger = "Uppercut", damageAmount = 20, staminaCost = 20, cooldown = 1.2f, range = 1.4f };
        
        // Set navmesh agent properties
        navMeshAgent.speed = 3.0f;
        navMeshAgent.stoppingDistance = optimalDistance;
        navMeshAgent.updateRotation = false;
        
        // Start thinking process
        StartCoroutine(RegenerateStamina());
    }
    
    void Update()
    {
        if (player == null) return;
        
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        
        // Always look at player
        LookAtPlayer();
        
        // Update cooldowns
        if (Time.time > timeToNextDecision)
        {
            DecideNextAction(distanceToPlayer);
        }
        
        // Execute current state behavior
        ExecuteStateAction(distanceToPlayer);
    }
    
    private void DecideNextAction(float distanceToPlayer)
    {
        // Don't make new decisions if stunned
        if (isStunned)
        {
            currentState = BoxingState.Stunned;
            return;
        }
        
        // Reset time to next decision
        timeToNextDecision = Time.time + reactionTime * (1 + Random.Range(-0.3f, 0.3f));
        
        // If player is attacking and we have enough reaction time based on difficulty
        if (playerAttacking && Random.value < difficultyLevel)
        {
            // Decide to block or dodge
            if (Random.value < defensiveness)
            {
                if (Random.value > 0.3f)
                {
                    currentState = BoxingState.Blocking;
                }
                else
                {
                    currentState = BoxingState.Dodging;
                    StartCoroutine(PerformDodge());
                }
                return;
            }
        }
        
        // If the player is vulnerable and we're skilled enough to notice
        if (playerInVulnerableState && Random.value < difficultyLevel)
        {
            // Aggressive counter attack
            if (distanceToPlayer <= maxAttackDistance && stamina >= 10)
            {
                currentState = BoxingState.Attacking;
                AttackPlayer();
                return;
            }
        }
        
        // Normal decision making
        float decisionValue = Random.value;
        
        if (distanceToPlayer > maxAttackDistance)
        {
            // Too far, need to get closer
            currentState = BoxingState.Approaching;
        }
        else if (distanceToPlayer < minAttackDistance)
        {
            // Too close, retreat or attack
            if (decisionValue < 0.7f)
            {
                currentState = BoxingState.Retreating;
            }
            else if (stamina >= 10 && Time.time > lastPunchTime + currentCooldown)
            {
                currentState = BoxingState.Attacking;
                AttackPlayer();
            }
        }
        else
        {
            // In optimal range
            if (decisionValue < aggressiveness && stamina >= 10 && Time.time > lastPunchTime + currentCooldown)
            {
                // Attack if aggressive enough and have stamina
                currentState = BoxingState.Attacking;
                AttackPlayer();
            }
            else if (decisionValue < aggressiveness + 0.3f)
            {
                // Keep approaching to pressure
                currentState = BoxingState.Approaching;
            }
            else
            {
                // Circle around
                currentState = BoxingState.Circling;
                UpdateCirclingPoint();
            }
        }
    }
    
    private void ExecuteStateAction(float distanceToPlayer)
    {
        switch (currentState)
        {
            case BoxingState.Circling:
                CircleAroundPlayer();
                break;
                
            case BoxingState.Approaching:
                ApproachPlayer();
                break;
                
            case BoxingState.Retreating:
                RetreatFromPlayer();
                break;
                
            case BoxingState.Blocking:
                Block();
                break;
                
            case BoxingState.Stunned:
                // Do nothing, waiting for stun to wear off
                break;
                
            case BoxingState.Dodging:
                // Handled by coroutine
                break;
                
            case BoxingState.Attacking:
                // Handled by AttackPlayer method
                break;
        }
        
        // Update animator
        animator.SetFloat("DistanceToPlayer", distanceToPlayer);
        animator.SetBool("IsBlocking", isBlocking);
        animator.SetBool("IsMoving", currentState == BoxingState.Approaching || 
                                   currentState == BoxingState.Retreating || 
                                   currentState == BoxingState.Circling);
    }
    
    private void LookAtPlayer()
    {
        Vector3 lookDirection = player.position - transform.position;
        lookDirection.y = 0;
        if (lookDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(lookDirection),
                0.15f
            );
        }
    }
    
    private void ApproachPlayer()
    {
        navMeshAgent.isStopped = false;
        navMeshAgent.SetDestination(player.position);
        animator.SetFloat("ForwardMovement", 1.0f);
        animator.SetFloat("SideMovement", 0.0f);
    }
    
    private void RetreatFromPlayer()
    {
        Vector3 retreatDirection = transform.position - player.position;
        retreatDirection.y = 0;
        retreatDirection.Normalize();
        
        Vector3 targetPoint = transform.position + retreatDirection * 3.0f;
        navMeshAgent.isStopped = false;
        navMeshAgent.SetDestination(targetPoint);
        
        animator.SetFloat("ForwardMovement", -1.0f);
        animator.SetFloat("SideMovement", 0.0f);
    }
    
    private void UpdateCirclingPoint()
    {
        // Choose a random direction to circle (left or right)
        circlingAngle = Random.value > 0.5f ? 90 : -90;
    }
    
    private void CircleAroundPlayer()
    {
        // Calculate circle point
        Vector3 directionToPlayer = transform.position - player.position;
        directionToPlayer.y = 0;
        directionToPlayer.Normalize();
        
        // Rotate the direction vector by circlingAngle
        Vector3 perpendicularDirection = Quaternion.Euler(0, circlingAngle, 0) * directionToPlayer;
        
        // Calculate target position
        Vector3 targetPosition = player.position + directionToPlayer * optimalDistance + perpendicularDirection * circlingSpeed;
        
        // Move to the target
        navMeshAgent.isStopped = false;
        navMeshAgent.SetDestination(targetPosition);
        
        // Update animator values for strafing animation
        Vector3 localMoveDirection = transform.InverseTransformDirection(navMeshAgent.desiredVelocity);
        animator.SetFloat("ForwardMovement", localMoveDirection.z);
        animator.SetFloat("SideMovement", localMoveDirection.x);
    }
    
    private void AttackPlayer()
    {
        navMeshAgent.isStopped = true;
        
        // Choose attack type based on distance and available stamina
        float distance = Vector3.Distance(transform.position, player.position);
        
        // Potential attacks based on range
        List<PunchData> possibleAttacks = new List<PunchData>();
        
        // Check if each attack is in range and we have enough stamina
        if (distance <= jab.range && stamina >= jab.staminaCost) possibleAttacks.Add(jab);
        if (distance <= cross.range && stamina >= cross.staminaCost) possibleAttacks.Add(cross);
        if (distance <= hook.range && stamina >= hook.staminaCost) possibleAttacks.Add(hook);
        if (distance <= uppercut.range && stamina >= uppercut.staminaCost) possibleAttacks.Add(uppercut);
        
        // Add combo possibility at higher difficulty and if we have enough stamina
        if (difficultyLevel > 0.6f && Random.value < 0.3f * difficultyLevel)
        {
            foreach (var combo in combos)
            {
                if (distance <= combo.range && stamina >= combo.staminaCost)
                {
                    possibleAttacks.Add(combo);
                }
            }
        }
        
        // Select an attack if possible
        if (possibleAttacks.Count > 0)
        {
            PunchData selectedAttack = possibleAttacks[Random.Range(0, possibleAttacks.Count)];
            ExecuteAttack(selectedAttack);
        }
        else
        {
            // Not enough stamina or no suitable attack, switch to circling
            currentState = BoxingState.Circling;
        }
    }
    
    private void ExecuteAttack(PunchData attack)
    {
        // Execute the attack
        animator.SetTrigger(attack.animationTrigger);
        
        // Update state
        lastPunchTime = Time.time;
        currentCooldown = attack.cooldown;
        stamina -= attack.staminaCost;
        
        // Simulate damage to player in OnAnimationPunchImpact() which would be called via animation event
    }
    
    private void Block()
    {
        navMeshAgent.isStopped = true;
        isBlocking = true;
        animator.SetBool("IsBlocking", true);
        
        // Stop blocking after a random time
        if (currentActionCoroutine != null)
        {
            StopCoroutine(currentActionCoroutine);
        }
        currentActionCoroutine = StartCoroutine(StopBlockingAfterDelay());
    }
    
    private IEnumerator StopBlockingAfterDelay()
    {
        yield return new WaitForSeconds(Random.Range(0.5f, 1.5f));
        isBlocking = false;
        animator.SetBool("IsBlocking", false);
        currentState = BoxingState.Circling;
    }
    
    private IEnumerator PerformDodge()
    {
        navMeshAgent.isStopped = true;
        isDodging = true;
        
        // Choose dodge direction (left, right or back)
        int dodgeDirection = Random.Range(0, 3);
        
        switch (dodgeDirection)
        {
            case 0: // Left
                animator.SetTrigger("DodgeLeft");
                break;
            case 1: // Right
                animator.SetTrigger("DodgeRight");
                break;
            case 2: // Back
                animator.SetTrigger("DodgeBack");
                break;
        }
        
        // Wait for dodge animation
        yield return new WaitForSeconds(0.5f);
        
        isDodging = false;
        currentState = BoxingState.Circling;
    }
    
    // Called via animation event when punch should deal damage
    public void OnAnimationPunchImpact()
    {
        // Raycast forward to see if player is hit
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 1.5f, transform.forward, out hit, 2.0f))
        {
            if (hit.transform.CompareTag("Player"))
            {
                // Notify player of hit - this would interface with your VR player damage system
                PlayerVRBoxing playerScript = hit.transform.GetComponent<PlayerVRBoxing>();
                if (playerScript != null)
                {
                    playerScript.TakeDamage(10, transform.forward * punchForce);
                }
            }
        }
    }
    
    // Function to be called when the AI gets hit
    public void TakeDamage(int damageAmount, Vector3 hitDirection, bool isCounterHit = false)
    {
        // Reduce damage if blocking in the right direction
        if (isBlocking)
        {
            // Calculate if block was successful based on hit direction
            Vector3 hitDirNormalized = hitDirection.normalized;
            float blockEffectiveness = Vector3.Dot(transform.forward, hitDirNormalized);
            
            if (blockEffectiveness > 0.5f)
            {
                // Good block, reduce damage significantly
                damageAmount = Mathf.RoundToInt(damageAmount * 0.2f);
                animator.SetTrigger("BlockedHit");
            }
            else
            {
                // Bad angle block, reduce damage slightly
                damageAmount = Mathf.RoundToInt(damageAmount * 0.7f);
                animator.SetTrigger("PartialBlockedHit");
            }
        }
        else if (!isDodging)
        {
            // Full damage if not blocking or dodging
            animator.SetTrigger("TakeHit");
            
            // Chance to get stunned based on damage
            float stunChance = damageAmount / 100f;
            if (isCounterHit) stunChance *= 1.5f;
            
            if (Random.value < stunChance)
            {
                StartCoroutine(GetStunned(damageAmount / 20f));
            }
        }
        
        // Apply damage
        health -= damageAmount;
        
        // Check for knockout
        if (health <= 0)
        {
            Die();
        }
    }
    
    private IEnumerator GetStunned(float duration)
    {
        isStunned = true;
        currentState = BoxingState.Stunned;
        animator.SetBool("IsStunned", true);
        
        yield return new WaitForSeconds(duration);
        
        isStunned = false;
        animator.SetBool("IsStunned", false);
        currentState = BoxingState.Retreating;
    }
    
    private IEnumerator RegenerateStamina()
    {
        while (true)
        {
            if (stamina < 100)
            {
                stamina = Mathf.Min(100, stamina + Mathf.RoundToInt(staminaRegenRate * Time.deltaTime));
            }
            yield return null;
        }
    }
    
    private void Die()
    {
        // Stop all coroutines
        StopAllCoroutines();
        
        // Play death animation
        animator.SetTrigger("KnockedOut");
        
        // Disable navmesh agent
        navMeshAgent.isStopped = true;
        navMeshAgent.enabled = false;
        
        // Disable this script after a delay
        Invoke("DisableAI", 5.0f);
    }
    
    private void DisableAI()
    {
        this.enabled = false;
    }
    
    // These methods would be called from your player script to inform the AI about player state
    public void NotifyPlayerAttacking()
    {
        playerAttacking = true;
        StartCoroutine(ResetPlayerAttackingState());
    }
    
    private IEnumerator ResetPlayerAttackingState()
    {
        yield return new WaitForSeconds(0.5f);
        playerAttacking = false;
    }
    
    public void NotifyPlayerVulnerable(float duration)
    {
        playerInVulnerableState = true;
        StartCoroutine(ResetPlayerVulnerableState(duration));
    }
    
    private IEnumerator ResetPlayerVulnerableState(float duration)
    {
        yield return new WaitForSeconds(duration);
        playerInVulnerableState = false;
    }
    
    // Debug gizmos to visualize AI state
    private void OnDrawGizmosSelected()
    {
        if (player == null) return;
        
        // Draw attack ranges
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, minAttackDistance);
        
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, optimalDistance);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, maxAttackDistance);
        
        // Draw direction to player
        Gizmos.color = Color.blue;
        if (player != null)
        {
            Gizmos.DrawLine(transform.position + Vector3.up, player.position + Vector3.up);
        }
    }
}