﻿using UnityEngine;
using BehaviorTree;

[System.Obsolete("Use EnemyController instead.")]
public class Enemy : MonoBehaviour {
    public BNode lastNode;

    [HideInInspector]
    public GameObject target;

    [Range(0, 8)]
    public float moveSpeed = 1;
    public float jumpForce = 4;
    public float maxJumpHeight = 4;
    [Range(0, 90)]
    public float maxSlopeAngle = 45;
    private float dir = 0, targetDir = 1;

    public float targetRange = 10;
    public float nearDistance = 3;
    private float baseNear;

    protected BNode behaviorTree;
    protected Rigidbody rb;
    public GameObject player, playerLightPoint;

    private float scareDelay = 0;

    public Behavior behavior;

    private Animator animator;
    private bool grounded;
    [SerializeField] float groundCheckDistance = 0.02f;
    [SerializeField] LayerMask groundLayers = 0;
    [SerializeField] PhysicMaterial groundedMaterial = null, airborneMaterial = null;
    [SerializeField] float grabRange = 0.5f;
    private bool hurtBeforeStateChange;
    private CapsuleCollider enemyCollider;

    public Transform rightHand, leftHand;

    [System.Serializable]
    public enum Behavior {
        MOTH, VAMPIRE
    }
    
    public float remainingDistance { get; set; }

    void Awake() {
        animator = GetComponent<Animator>();
        enemyCollider = GetComponent<CapsuleCollider>();
        StartAI();
    }

    protected void StartAI() {
        baseNear = nearDistance;
        rb = GetComponent<Rigidbody>();
        switch(behavior) {
            default:
            case Behavior.MOTH:
                behaviorTree = new BehaviorTree.Composite.BSequence(new BNode[]{
                        new BAction(this, CheckTarget), 
                        new BAction(this, Walk),
                        new BAction(this, Attack),
                        });
                break;
            case Behavior.VAMPIRE:
                behaviorTree = new BehaviorTree.Composite.BSequence(new BNode[]{
                        new BAction(this, CheckTarget),
                        new BAction(this, Walk),
                        new BAction(this, Attack),
                        });
                break;
        }
    }

    protected BNode.NodeState Walk() {
        if (target != null) MoveToTarget(target);
        else dir = Mathf.MoveTowards(dir, 0, Time.deltaTime * 5);
        return BNode.NodeState.SUCCESS;
    }

    protected BNode.NodeState CheckTarget() {
        float lightDist = Vector3.Distance(transform.position, playerLightPoint.transform.position);
        float playerDist = Vector3.Distance(transform.position, player.transform.position);
        switch(behavior) {
            default:
            case Behavior.MOTH:
                if(lightDist < targetRange) target = playerLightPoint;
                else {
                    if(playerDist < targetRange) target = player;
                    else target = null;
                }
                break;
            case Behavior.VAMPIRE:
                if(playerDist < targetRange) target = player;
                else {
                    target = null;
                    hurtBeforeStateChange = false;
                }
                break;
        }
        return BNode.NodeState.SUCCESS;
    }

    protected BNode.NodeState Attack() {
        if (target && target.GetComponent<PlayerController>().dead) return BNode.NodeState.SUCCESS;
        if (scareDelay > 0) return BNode.NodeState.FAIL;
        float playerDist = Vector3.Distance(transform.position, player.transform.position);
        switch(behavior)
        {
            case Behavior.MOTH:
                break;
            case Behavior.VAMPIRE:
                if (playerDist < nearDistance * 1.5f)
                {
                    if (animator != null) animator.SetTrigger("Attack");
                    hurtBeforeStateChange = false;
                }
                break;
        }
        return BNode.NodeState.SUCCESS;
    }

    public void Scare(Vector3 origin) {
        if (!hurtBeforeStateChange) {
            if(animator != null && target && !target.GetComponent<PlayerController>().dead) animator.SetTrigger("Hurt");
            hurtBeforeStateChange = true;
        }
        if(scareDelay > 0) return;
        scareDelay = 2;
        float posX = transform.position.x;  
    }

    void FixedUpdate() {
        behaviorTree.Run();
        if(scareDelay > 0) {
            scareDelay -= Time.deltaTime;
            targetDir = Mathf.Lerp(targetDir, -1, Time.deltaTime * 2);
        }
        else targetDir = 1;

        CheckGroundedState();
    }

    void Update()
    {
        Animate();
    }

    protected void MoveToTarget(GameObject target) {
        Vector3 dest = transform.position - target.transform.position;
        if(dest.x > 0) dir = -1 * targetDir;
        if(dest.x < 0) dir = 1 * targetDir;

        remainingDistance = Mathf.Max(0, Vector3.Distance(transform.position, target.transform.position) - nearDistance);

        if (remainingDistance > 0) Move();

        dir *= Mathf.Min(1, remainingDistance);
    }

    private void Move() {
        if (grounded)
        {
            rb.MovePosition(rb.position + new Vector3((moveSpeed / 10) * dir, 0, 0) / 10f);
            //Check objects at foot level
            if (Physics.Raycast(transform.position + Vector3.up * 0.05f, dir > 0 ? Vector3.right : Vector3.left, 1, groundLayers))
            {
                if (MustJump())
                {
                    //Jump
                    rb.AddForce((Vector3.up + transform.forward.normalized / Mathf.Min(Mathf.Epsilon, rb.velocity.x))* jumpForce, ForceMode.VelocityChange);
                }
            }
        }
    }

    private bool MustJump()
    {
        //If an object is found at foot level, check if its highest surface is reachable with a jump.
        //Do a raycast adjacent to the enemy
        RaycastHit adjacentHit = new RaycastHit();

        Debug.DrawRay(transform.position + new Vector3(dir > 0 ? 1 : -1, 6, 0), Vector3.down * 6);
        if (Physics.Raycast(transform.position + new Vector3(transform.eulerAngles.y == 90 ? 1 : -1, 6, 0), Vector3.down * 6, out adjacentHit, 6, groundLayers))
        {
            //Determine if its angle is not scalable
            Debug.Log(Vector3.Angle(adjacentHit.normal, transform.up));
            float slopeAngle = Vector3.Angle(adjacentHit.normal, transform.up);
            if (slopeAngle < maxSlopeAngle && slopeAngle > 0)
            {
                return false;
            }
            //Return if a jump is not too high.
            float height = adjacentHit.point.y - transform.position.y;
            if (height < 4 && height > 0.01f)
            {
                return true;
            }
        }
        return false;
    }

    private void CheckGroundedState()
    {
        RaycastHit ground = new RaycastHit();
        if(animator == null) return;
        grounded = animator.applyRootMotion = Physics.Raycast(transform.position + Vector3.up * 0.01f, Vector3.down, out ground, groundCheckDistance, groundLayers);
        enemyCollider.material = grounded ? groundedMaterial : airborneMaterial;
    }

    private void Animate()
    {
        if (Mathf.Abs(dir) > 0.05f) transform.eulerAngles = Vector3.Lerp(transform.eulerAngles, Vector3.up * (dir > 0 ? 90 : 270), Time.deltaTime * 20 * Mathf.Abs(dir));
        if(animator == null) return;
        animator.SetBool("Grounded", grounded);
        animator.SetFloat("Speed", Mathf.Abs(dir));
    }

    //Animator Event function
    public void CheckAttackHit(AnimationInitializer anim)
    {
        //Right hand
        Collider[] hits = Physics.OverlapSphere(rightHand.position, grabRange, 1 << LayerMask.NameToLayer("Player"));
        if (hits.Length > 0 && hits[0].GetComponent<PlayerController>())
        {
            AttackHit(anim);
            return;
        }
        //Left hand
        hits = Physics.OverlapSphere(leftHand.position, grabRange, 1 << LayerMask.NameToLayer("Player"));
        if (hits.Length > 0 && hits[0].GetComponent<PlayerController>())
        {
            AttackHit(anim);
            return;
        }

    }

    private void AttackHit(AnimationInitializer anim)
    {
        if (anim) anim.Activate(transform, target.transform);
        else throw new MissingComponentException("No AnimationInitializer set for \"" + animator.GetCurrentAnimatorClipInfo(0)[0].clip.name + "\".");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.11f, 0.1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up, nearDistance);
        Gizmos.DrawWireSphere(transform.position + Vector3.up, targetRange);
        Gizmos.color = new Color(1f, 0.21f, 0.2f, 0.8f);
        Gizmos.DrawRay(transform.position, Vector3.up * maxJumpHeight);
        Gizmos.color = Color.blue;
        if (rightHand)
        {
            Gizmos.DrawWireSphere(rightHand.position, grabRange);
        }
        if (leftHand)
        {
            Gizmos.DrawWireSphere(leftHand.position, grabRange);
        }
    }
}
