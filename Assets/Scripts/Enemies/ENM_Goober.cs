using Unity.Netcode;
using UnityEngine;
using System.Collections;

// Made By: Jason Lodge.
// Summary: Enemy values and attack logic for goober.
// Server runs all real logic, clients receive synced values through network vars.

public class ENM_Goober : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private BD_Goober boid;
    [SerializeField] private Rigidbody rb;

    [Header("Values")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float shipDamage = 10f;

    [Header("Targeting")]
    [SerializeField] private float playerSearchRadius = 8f;
    [SerializeField] private float attackRange = 5f;
    [SerializeField] private float targetRefreshRate = 0.25f;
    [SerializeField] private LayerMask playerMask = ~0;

    [Header("Ship Attack")]
    [SerializeField] private float shipRayDistance = 5f;
    [SerializeField] private string shipTag = "Rocket";

    [Header("Attack")]
    [SerializeField] private float attackChargeTime = 1f;
    [SerializeField] private float attackForce = 20f;
    [SerializeField] private float attackDuration = 0.5f;
    [SerializeField] private float attackCooldown = 1f;

    [Header("Stun")]
    [SerializeField] private float stunDelay = 1f;
    [SerializeField] private float attackStunPushForce = 5f;
    [SerializeField] private float attackStunTorqueForce = 5f;

    private NetworkVariable<float> health = new NetworkVariable<float>(0f);
    private NetworkVariable<bool> isDead = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> isChargingAttack = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> isAttacking = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> isStunned = new NetworkVariable<bool>(false);

    private Transform playerTarget;
    private Transform attackTarget;
    private bool attackShip;

    private Coroutine attackRoutine;
    private Coroutine stunRoutine;

    private float targetRefreshTimer;
    private float attackCooldownTimer;
    private bool hasHitDuringAttack;

    public float Health => health.Value;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead.Value;
    public bool IsChargingAttack => isChargingAttack.Value;
    public bool IsAttacking => isAttacking.Value;
    public bool IsStunned => isStunned.Value;

    private void Awake()
    {
        if (boid == null)
        {
            boid = GetComponent<BD_Goober>();
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
    }

    // setup network state once the object has spawned.
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer)
        {
            return;
        }

        health.Value = maxHealth;
        isDead.Value = false;
        isChargingAttack.Value = false;
        isAttacking.Value = false;
        isStunned.Value = false;

        RefreshTarget_Server();
    }

    private void Update()
    {
        if (!IsServer)
        {
            return;
        }

        if (isDead.Value)
        {
            return;
        }

        attackCooldownTimer -= Time.deltaTime;
        attackCooldownTimer = Mathf.Max(0f, attackCooldownTimer);

        TickTargeting_Server();
        TickMovementTarget_Server();
        TryStartAttack_Server();
    }

    private void TickTargeting_Server()
    {
        targetRefreshTimer -= Time.deltaTime;

        if (targetRefreshTimer > 0f)
        {
            return;
        }

        targetRefreshTimer = targetRefreshRate;
        RefreshTarget_Server();
    }

    private void RefreshTarget_Server()
    {
        playerTarget = FindClosestPlayer_Server();
    }

    private void TickMovementTarget_Server()
    {
        if (boid == null)
        {
            return;
        }

        if (isChargingAttack.Value || isAttacking.Value || isStunned.Value)
        {
            boid.enabled = false;
            return;
        }

        boid.enabled = true;

        // if we have a player, chase them.
        boid.target = playerTarget;
    }

    private void TryStartAttack_Server()
    {
        if (attackRoutine != null)
        {
            return;
        }

        if (isStunned.Value)
        {
            return;
        }

        if (isChargingAttack.Value || isAttacking.Value)
        {
            return;
        }

        if (attackCooldownTimer > 0f)
        {
            return;
        }

        attackTarget = null;
        attackShip = false;

        if (playerTarget != null)
        {
            float distance = Vector3.Distance(transform.position, playerTarget.position);

            if (distance <= attackRange)
            {
                attackTarget = playerTarget;
                attackShip = false;
                attackRoutine = StartCoroutine(AttackRoutine_Server(playerTarget.position));
                return;
            }
        }

        if (CanAttackShip_Server(out Vector3 shipHitPosition))
        {
            attackShip = true;
            attackRoutine = StartCoroutine(AttackRoutine_Server(shipHitPosition));
        }
    }

    private bool CanAttackShip_Server(out Vector3 shipHitPosition)
    {
        shipHitPosition = Vector3.zero;

        Vector3 rayDirection = Vector3.zero - transform.position;

        if (rayDirection == Vector3.zero)
        {
            rayDirection = transform.forward;
        }

        rayDirection.Normalize();

        if (Physics.Raycast(transform.position, rayDirection, out RaycastHit hit, shipRayDistance))
        {
            if (hit.collider.CompareTag(shipTag) || hit.collider.GetComponentInParent<Transform>().CompareTag(shipTag))
            {
                shipHitPosition = hit.point;
                return true;
            }
        }

        return false;
    }

    private IEnumerator AttackRoutine_Server(Vector3 attackPosition)
    {
        isChargingAttack.Value = true;
        isAttacking.Value = false;
        hasHitDuringAttack = false;

        if (boid != null)
        {
            boid.enabled = false;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        yield return new WaitForSeconds(attackChargeTime);

        if (isDead.Value)
        {
            attackRoutine = null;
            yield break;
        }

        if (isStunned.Value)
        {
            attackRoutine = null;
            yield break;
        }

        Vector3 attackDirection = attackPosition - transform.position;

        if (attackDirection == Vector3.zero)
        {
            attackDirection = transform.forward;
        }

        attackDirection.Normalize();

        isChargingAttack.Value = false;
        isAttacking.Value = true;

        if (rb != null)
        {
            rb.AddForce(attackDirection * attackForce, ForceMode.VelocityChange);
        }

        yield return new WaitForSeconds(attackDuration);

        isAttacking.Value = false;
        attackCooldownTimer = attackCooldown;

        StunAfterAttack_Server(-attackDirection);

        attackRoutine = null;
    }

    private void StunAfterAttack_Server(Vector3 pushDirection)
    {
        if (stunRoutine != null)
        {
            StopCoroutine(stunRoutine);
        }

        if (rb != null)
        {
            rb.AddForce(pushDirection.normalized * attackStunPushForce, ForceMode.VelocityChange);
            rb.AddTorque(Random.insideUnitSphere * attackStunTorqueForce, ForceMode.VelocityChange);
        }

        stunRoutine = StartCoroutine(StunRoutine_Server());
    }

    private IEnumerator StunRoutine_Server()
    {
        isStunned.Value = true;

        if (boid != null)
        {
            boid.enabled = false;
        }

        yield return new WaitForSeconds(stunDelay);

        isStunned.Value = false;

        if (boid != null && !isDead.Value)
        {
            boid.enabled = true;
        }

        stunRoutine = null;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer)
        {
            return;
        }

        if (!isAttacking.Value)
        {
            return;
        }

        if (hasHitDuringAttack)
        {
            return;
        }

        TryDamageCollision_Server(collision.collider);
    }

    private void TryDamageCollision_Server(Collider col)
    {
        if (col == null)
        {
            return;
        }

        CC_CharacterValues values = col.GetComponentInParent<CC_CharacterValues>();

        if (values != null)
        {
            values.RemoveHealth(damage);
            hasHitDuringAttack = true;
            return;
        }

        if (col.CompareTag(shipTag) || col.GetComponentInParent<Transform>().CompareTag(shipTag))
        {
            RS_Move ship = FindFirstObjectByType<RS_Move>();

            if (ship != null)
            {
                ship.health.Value -= Mathf.Abs(shipDamage);
                ship.health.Value = Mathf.Max(0f, ship.health.Value);
                hasHitDuringAttack = true;
            }

            return;
        }
    }

    private Transform FindClosestPlayer_Server()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, playerSearchRadius, playerMask);

        Transform closestTarget = null;
        float closestDistance = Mathf.Infinity;

        foreach (Collider col in colliders)
        {
            CC_CharacterValues values = col.GetComponentInParent<CC_CharacterValues>();

            if (values == null)
            {
                continue;
            }

            if (values.isDead)
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, values.transform.position);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTarget = values.transform;
            }
        }

        return closestTarget;
    }

    public void Damage(float damageToTake)
    {
        if (!IsServer)
        {
            return;
        }

        Damage_Server(damageToTake);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void DamageRpc(float damageToTake)
    {
        Damage_Server(damageToTake);
    }

    private void Damage_Server(float damageToTake)
    {
        if (isDead.Value)
        {
            return;
        }

        health.Value -= Mathf.Abs(damageToTake);
        health.Value = Mathf.Clamp(health.Value, 0f, maxHealth);

        StunWithoutPush_Server();

        if (health.Value <= 0f)
        {
            Die_Server();
        }
    }

    private void StunWithoutPush_Server()
    {
        if (stunRoutine != null)
        {
            StopCoroutine(stunRoutine);
        }

        isChargingAttack.Value = false;
        isAttacking.Value = false;

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        stunRoutine = StartCoroutine(StunRoutine_Server());
    }

    private void Die_Server()
    {
        if (isDead.Value)
        {
            return;
        }

        isDead.Value = true;
        isChargingAttack.Value = false;
        isAttacking.Value = false;
        isStunned.Value = false;

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        if (stunRoutine != null)
        {
            StopCoroutine(stunRoutine);
            stunRoutine = null;
        }

        if (boid != null)
        {
            boid.enabled = false;
        }

        NetworkObject netObj = GetComponent<NetworkObject>();

        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, playerSearchRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.cyan;

        Vector3 rayDirection = Vector3.zero - transform.position;

        if (rayDirection == Vector3.zero)
        {
            rayDirection = transform.forward;
        }

        rayDirection.Normalize();

        Gizmos.DrawRay(transform.position, rayDirection * shipRayDistance);
    }
}