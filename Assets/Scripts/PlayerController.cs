using UnityEngine;

public class PlayerController : Entity
{
    private enum PlayerStateEnum
    {
        Idle,
        Dashing,
        Attacking,
        Parrying,
        HitStun,
        Dead
    }
    private PlayerStateEnum state;

    [Header("References")]
    [SerializeField] private CapsuleCollider capsuleCollider;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform model;
    [SerializeField] private Hitbox heavyAttack;
    [Space(10)]
    [SerializeField] private UICanvas uiCanvas;
    [SerializeField] private StaminaSlider staminaSlider;
    [SerializeField] private SpeedSlider speedSlider;
    [Space(5)]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private Transform camTransform;

    [Header("Parameters")]
    [SerializeField] private float maxCamDistance;
    [SerializeField] private float minCamDistance;
    private float currMaxCamDistance;
    [SerializeField] private float camMoveSpeedY;
    [SerializeField] private float camMoveSpeedX;
    [SerializeField] private float modelYRotSpeed;
    [Space(10)]
    [SerializeField] private float groundCheckDist;
    [SerializeField] private float slopeShoveSpeed;
    [SerializeField] private float maxSlopeAngle = 45;
    [Space(5)]
    [SerializeField] private float gravSpeed;
    [SerializeField] private float jumpSpeed;
    [Space(5)]
    [SerializeField] private float accelSpeed_walk;
    [SerializeField] private float maxSpeed_walk;
    [SerializeField] private float accelSpeed_sprint;
    [SerializeField] private float maxSpeed_sprint;
    [SerializeField] private float dashVelocityRotationSpeed;
    [SerializeField] private float frictionSpeed;
    [SerializeField] private float dashSpeed;
    [SerializeField] private float maxSpeed_dashing;
    [SerializeField] private AnimationCurve fovChangeForSpeed;
    [SerializeField] private float minFOV;
    [SerializeField] private float maxFOV;
    [SerializeField] private float fovChangePerSec;
    [SerializeField] private float[] speedStateThresholds;
    [Space(5)]
    [SerializeField] private float m_maxStamina;
    [SerializeField] private float m_staminaRechargePerSec;
    [SerializeField] private float m_staminaChargeDelay_dash;
    [SerializeField] private float m_sprintCostPerSec;
    [SerializeField] private float m_dashCost;
    [Space(5)]
    [SerializeField] private float m_sprintCostPerSpeedState;
    [SerializeField] private float m_speedLossPerSec;
    [Space(5)]
    [SerializeField] private float m_dashDuration;
    [SerializeField] private float m_waitTimeAfterDash;
    [Space(5)]
    [SerializeField] private float m_heavyAttackDuration;
    [Space(5)]
    [SerializeField] private float m_parryWalkSpeedMultiplier;

    //Attack
    private float attackStartTime;
    private float t_attackScootEndTime;
    private float t_attackEndTime;
    private float currAttackScootingSpeed;
    private Vector2 attackInputDirection;

    //Stamina
    private float t_nextStaminaRechargeTime;
    private float currStamina;

    //Sprint/Speed
    private float t_nextSpeedDecreaseTime;
    private float currSpeedState = 0;
    private float speedGainPerSec;

    //Parry
    private bool isParrying;

    //Movement
    private Vector3 currVelocity;
    private bool isGrounded;
    private Vector3 groundNormal;

    [Header("Misc")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask cameraWallLayer;

    //Anim parameters
    private float lastSpeedStatePercent;
    private int ANIM_PARAM_GROUNDED = Animator.StringToHash("Grounded");
    private int ANIM_PARAM_SPRINTING = Animator.StringToHash("Sprinting");
    private int ANIM_PARAM_SPEED_STATE_PERCENT = Animator.StringToHash("SpeedState_Percent");
    private int ANIM_PARAM_DASH_TRIGGER = Animator.StringToHash("Dash");
    private int ANIM_PARAM_DASH_DIR_X = Animator.StringToHash("DashDirX");
    private int ANIM_PARAM_DASH_DIR_Y = Animator.StringToHash("DashDirY");
    private int ANIM_PARAM_HEAVY_ATTACK = Animator.StringToHash("HeavyAttack");
    private int ANIM_PARAM_PARRY = Animator.StringToHash("Parry");

    private new void Awake()
    {
        base.Awake();

        currStamina = m_maxStamina / 2f;
        speedGainPerSec = (m_sprintCostPerSec / m_sprintCostPerSpeedState);
    }

    private new void Start()
    {
        base.Start();

        Cursor.lockState = CursorLockMode.Locked;
    }

    public Vector3 ConvertInputDirToWorldDir(Vector2 _inputDir)
    {
        Vector3 forwardDir = cameraPivot.forward;
        forwardDir.y = 0;
        forwardDir.Normalize();

        Vector3 rightDir = cameraPivot.right.normalized;

        return _inputDir.y * forwardDir + _inputDir.x * rightDir;
    }

    public void OnAnimEnded()
    {
        state = PlayerStateEnum.Idle;
        isParrying = false;
    }

    private void Update()
    {
        GroundCheck();

        #region Player Movement
        if (state == PlayerStateEnum.Idle || state == PlayerStateEnum.Parrying)
        {
            Vector3 worldInput;
            if (InputHandler.Instance.PressingDirection)
            {
                Vector3 forwardDir = cameraPivot.forward;
                forwardDir.y = 0;
                forwardDir.Normalize();

                Vector3 rightDir = cameraPivot.right;
                rightDir.y = 0;
                rightDir.Normalize();

                worldInput = InputHandler.Instance.MoveXZ.y * forwardDir + InputHandler.Instance.MoveXZ.x * rightDir;
            }
            else
                worldInput = Vector3.zero;

            if (state == PlayerStateEnum.Parrying)
                HandleMovement_Walk(worldInput * m_parryWalkSpeedMultiplier);
            else
                HandleMovement_Walk(worldInput);
        }
        else if (state == PlayerStateEnum.Dashing)
        {
            Vector3 worldInput;
            if (InputHandler.Instance.PressingDirection)
            {
                Vector3 forwardDir = cameraPivot.forward;
                forwardDir.y = 0;
                forwardDir.Normalize();

                Vector3 rightDir = cameraPivot.right;
                rightDir.y = 0;
                rightDir.Normalize();

                worldInput = InputHandler.Instance.MoveXZ.y * forwardDir + InputHandler.Instance.MoveXZ.x * rightDir;
            }
            else
            {
                worldInput = model.forward;
                worldInput.y = 0;
            }

            worldInput.Normalize();

            HandleMovement_Sprint(worldInput);
        }
        #endregion

        if (state == PlayerStateEnum.Idle || state == PlayerStateEnum.Dashing && isGrounded)
        {
            if (InputHandler.Instance.Parry.Down && false)
            {
                state = PlayerStateEnum.Parrying;
                isParrying = true;

                c_modelAnimator.SetTrigger(ANIM_PARAM_PARRY);
            }

            //Start Heavy Attack
            else if (InputHandler.Instance.HeavyAttack.Down && currSpeedState >= 1 && false)
            {
                //Set velocity
                float statePercent = ((int)currSpeedState) / 3f;
                //FIXME
                //heavyAttackSpeedblock.SetValues_Lerp(lowSpeedblock, highSpeedblock, statePercent);
                if (InputHandler.Instance.PressingDirection && !uiCanvas.IsLockedOn)
                    attackInputDirection = InputHandler.Instance.MoveXZ.normalized;
                else
                    attackInputDirection = Vector2.up;
                //FIXME
                //currAttackScootingSpeed = heavyAttackSpeedblock.DashStartSpeed;
                //currVelocity = ConvertInputDirToWorldDir(attackInputDirection) * heavyAttackSpeedblock.DashStartSpeed;

                //Set state vars and timers
                attackStartTime = Time.time;
                state = PlayerStateEnum.Attacking;
                t_attackScootEndTime = Time.time + m_dashDuration;
                t_attackEndTime = Time.time + m_heavyAttackDuration;
                t_nextSpeedDecreaseTime = Mathf.Max(t_nextSpeedDecreaseTime, t_attackEndTime + m_waitTimeAfterDash);
                t_nextStaminaRechargeTime = Mathf.Max(t_nextStaminaRechargeTime, t_attackEndTime + m_waitTimeAfterDash);

                //Set anim
                c_modelAnimator.SetTrigger(ANIM_PARAM_HEAVY_ATTACK);
            }
        }

        switch (state)
        {
            case PlayerStateEnum.Idle:
                //Start Dash
                if (isGrounded && InputHandler.Instance.Dash.Down && InputHandler.Instance.PressingDirection && currStamina > 0)
                {
                    //Consume stamina
                    currStamina -= m_dashCost;
                    staminaSlider.Slider.value = (currStamina / m_maxStamina);

                    //Set velocity
                    Vector3 forwardDir = cameraPivot.forward;
                    forwardDir.y = 0;
                    forwardDir.Normalize();

                    Vector3 rightDir = cameraPivot.right;
                    rightDir.y = 0;
                    rightDir.Normalize();

                    Vector3 worldInput;
                    if (InputHandler.Instance.PressingDirection)
                        worldInput = InputHandler.Instance.MoveXZ.y * forwardDir + InputHandler.Instance.MoveXZ.x * rightDir;
                    else
                        worldInput = Vector3.zero;

                    float currSpeed = Vector3.Dot(currVelocity, worldInput);
                    float newSpeed = Mathf.Max(dashSpeed, Mathf.Min(maxSpeed_dashing, currSpeed + dashSpeed));

                    currVelocity = worldInput.normalized * newSpeed;

                    //Set state vars and timers
                    state = PlayerStateEnum.Dashing;

                    //Set anim params
                    c_modelAnimator.SetTrigger(ANIM_PARAM_DASH_TRIGGER);
                    if (uiCanvas.IsLockedOn)
                    {
                        c_modelAnimator.SetFloat(ANIM_PARAM_DASH_DIR_X, InputHandler.Instance.MoveXZ.x);
                        c_modelAnimator.SetFloat(ANIM_PARAM_DASH_DIR_Y, InputHandler.Instance.MoveXZ.y);
                    }
                    else
                    {
                        c_modelAnimator.SetFloat(ANIM_PARAM_DASH_DIR_X, 0);
                        c_modelAnimator.SetFloat(ANIM_PARAM_DASH_DIR_Y, 1);
                    }
                }
                break;

            case PlayerStateEnum.Dashing:
                currStamina -= m_sprintCostPerSec * Time.deltaTime;
                staminaSlider.Slider.value = (currStamina / m_maxStamina);

                Vector3 currDashVelocity = currVelocity;
                currDashVelocity.y = 0;
                if (!InputHandler.Instance.Dash.Holding || currStamina <= 0)
                {
                    //End dash
                    state = PlayerStateEnum.Idle;

                    t_nextStaminaRechargeTime = Time.time + m_staminaChargeDelay_dash;
                }
                break;

            case PlayerStateEnum.Attacking:
                if (Time.time <= t_attackScootEndTime)
                {
                    //Decelerate

                    //FIXME
                    //currAttackScootingSpeed = Mathf.Lerp(heavyAttackSpeedblock.DashStartSpeed, heavyAttackSpeedblock.DashEndSpeed,
                    //(Time.time - attackStartTime) / m_dashDuration);
                    this.currVelocity = ConvertInputDirToWorldDir(attackInputDirection) * currAttackScootingSpeed;
                }
                break;
        }

        if (state == PlayerStateEnum.Idle)
        {
            //Passive Regen
            if (Time.time >= t_nextStaminaRechargeTime)
            {
                currStamina = Mathf.Min(currStamina + m_staminaRechargePerSec * Time.deltaTime, m_maxStamina);
            }

            //Update sprint slider ui
            staminaSlider.Slider.value = (currStamina / m_maxStamina);
        }

        //Update anim parameters
        //lastSpeedStatePercent = Utils.MoveTowardsValue(lastSpeedStatePercent, speedPercent, Time.deltaTime * maxSpeedState);
        //c_modelAnimator.SetFloat(ANIM_PARAM_SPEED_STATE_PERCENT, lastSpeedStatePercent);


        #region Camera Rotation
        if (uiCanvas.LockonState == UICanvas.LockonStateEnum.Unlocked)
        {
            float camXRot = cameraPivot.eulerAngles.x - InputHandler.Instance.Look.y * camMoveSpeedY * Time.deltaTime;

            if (camXRot > 180)
                camXRot -= 360;
            camXRot = Mathf.Clamp(camXRot, -60f, 89.5f);

            float newYRot = cameraPivot.eulerAngles.y + InputHandler.Instance.Look.x * camMoveSpeedX * Time.deltaTime;

            cameraPivot.eulerAngles = new Vector3(camXRot, newYRot, 0);
        }
        #endregion

        c_modelAnimator.SetBool(ANIM_PARAM_GROUNDED, isGrounded);
        c_modelAnimator.SetBool(ANIM_PARAM_SPRINTING, state == PlayerStateEnum.Dashing);

        if (isGrounded)
        {
            currVelocity.y = 0;
        }
        else
        {
            currVelocity.y -= gravSpeed * Time.deltaTime;
        }

        //Set FOV based on speed
        Vector3 noGravVelocity = currVelocity;
        noGravVelocity.y = 0;
        float speedPercent = fovChangeForSpeed.Evaluate(noGravVelocity.magnitude / maxSpeed_dashing);
        float targFOV = Mathf.Lerp(minFOV, maxFOV, speedPercent);

        //Update speed slider ui
        speedSlider.SetSliderVisualState_Percent(speedPercent);

        Camera.main.fieldOfView = Utils.MoveTowardsValue(Camera.main.fieldOfView, targFOV, fovChangePerSec * Time.deltaTime);
        currMaxCamDistance = Utils.Remap(Camera.main.fieldOfView, minFOV, maxFOV, maxCamDistance, minCamDistance);

        if (isGrounded)
            DebugCanvas.Instance.ShowText("grounded\n" + currVelocity.ToString());
        else
            DebugCanvas.Instance.ShowText("air\n" + currVelocity.ToString());

        if (isGrounded && (groundNormal - Vector3.up).sqrMagnitude > 0.05f)
        {
            //On a slope, rotate velocity
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, groundNormal);
            rb.velocity = rot * currVelocity;
        }
        else
        {
            rb.velocity = currVelocity;
        }

        #region Update facing direciton of player model
        Vector3 newForward = model.forward;
        Enemy lockedOnEnemy = uiCanvas.LockedonEnemy;
        if (lockedOnEnemy == null || state == PlayerStateEnum.Attacking)
        {
            Vector3 gravitylessVelocity = currVelocity;
            gravitylessVelocity.y = 0;

            if (gravitylessVelocity.sqrMagnitude > 0.1f)
                newForward = gravitylessVelocity.normalized;
        }
        else
        {
            Vector3 toEnemy = lockedOnEnemy.transform.position - transform.position;
            toEnemy.y = 0;
            newForward = toEnemy.normalized;
        }

        float targYRot = Mathf.Atan2(newForward.x, newForward.z) * Mathf.Rad2Deg;
        Vector3 modelRot = model.eulerAngles;
        modelRot.y = Utils.MoveTowardsRotation_Degrees(modelRot.y, targYRot, modelYRotSpeed * Time.deltaTime);
        model.eulerAngles = modelRot;
        #endregion

        MoveCameraOutOfWall();
    }

    public void OnParryEnded()
    {
        isParrying = false;
    }

    public void SpawnHeavyHitbox()
    {
        //FIXME
        //heavyAttack.EnableHitbox(t_attackEndTime - Time.time, heavyAttackSpeedblock.HeavyAttackDamage);

        currSpeedState = 0;
        speedSlider.SetSliderVisualState(currSpeedState);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="_worldInput">vector of input, y is always 0</param>
    void HandleMovement_Walk(Vector3 _worldInput)
    {
        if (isGrounded && (groundNormal - Vector3.up).sqrMagnitude > 0.05f)
        {
            //On a slope, rotate velocity
            Quaternion rot = Quaternion.FromToRotation(groundNormal, Vector3.up);
            currVelocity = rot * rb.velocity;
        }
        else
        {
            currVelocity = rb.velocity;
        }

        float currYVelocity = currVelocity.y;

        Vector3 modifiedVelocity_noGrav = currVelocity;
        modifiedVelocity_noGrav.y = 0;

        Vector3 targetVelocity = _worldInput * maxSpeed_walk;
        float accelSpeed = accelSpeed_walk * Time.deltaTime;

        if (targetVelocity.magnitude < modifiedVelocity_noGrav.magnitude)
        {
            //Check if targ velocity is within 45 degrees of curr velocity
            float currAngle = Mathf.Atan2(modifiedVelocity_noGrav.z, modifiedVelocity_noGrav.x);
            float targAngle;
            if (targetVelocity.magnitude == 0)
                targAngle = currAngle;
            else
                targAngle = Mathf.Atan2(targetVelocity.z, targetVelocity.x);

            if (Mathf.Abs(currAngle - targAngle) < Mathf.PI / 4)
            {
                //Use friction speed instead of accel speed
                accelSpeed = frictionSpeed * Time.deltaTime;
            }
        }

        //Apply acceleration
        Vector3 fromCurrVeloticyToTarg = targetVelocity - modifiedVelocity_noGrav;
        modifiedVelocity_noGrav += fromCurrVeloticyToTarg.normalized * Mathf.Min(accelSpeed, fromCurrVeloticyToTarg.magnitude);

        //Apply new velocity;
        currVelocity = modifiedVelocity_noGrav;

        currVelocity.y = currYVelocity;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="_worldInput">Normalized vector of input, y is always 0</param>
    void HandleMovement_Sprint(Vector3 _worldInput)
    {
        bool loseSpeedWhenHittingWalls = false;
        if (loseSpeedWhenHittingWalls)
        {
            if (isGrounded && (groundNormal - Vector3.up).sqrMagnitude > 0.05f)
            {
                //On a slope, rotate velocity
                Quaternion rot = Quaternion.FromToRotation(groundNormal, Vector3.up);
                currVelocity = rot * rb.velocity;
            }
            else
            {
                currVelocity = rb.velocity;
            }
        }

        float currYVelocity = currVelocity.y;

        Vector3 modifiedVelocity_noGrav = currVelocity;
        modifiedVelocity_noGrav.y = 0;

        //Apply acceleration
        float newSpeed = Mathf.Min(modifiedVelocity_noGrav.magnitude + (accelSpeed_sprint * Time.deltaTime), maxSpeed_sprint);

        //Rotate velocity
        float currAngle = Mathf.Atan2(modifiedVelocity_noGrav.z, modifiedVelocity_noGrav.x);
        float targAngle = Mathf.Atan2(_worldInput.z, _worldInput.x);
        float newAngle = Utils.MoveTowardsRotation_Radians(currAngle, targAngle, dashVelocityRotationSpeed * Mathf.Deg2Rad * Time.deltaTime);

        float newSpeedX = Mathf.Cos(newAngle);
        float newSpeedZ = Mathf.Sin(newAngle);

        Vector3 newVelocity = new Vector3(newSpeedX, 0, newSpeedZ) * newSpeed;

        //Apply new velocity;
        currVelocity = newVelocity;

        currVelocity.y = currYVelocity;
    }


    /// <summary>
    /// Sets the state of "grounded", and also handles snapping to the ground when going down slopes
    /// </summary>
    void GroundCheck()
    {
        // reset values before the ground check
        isGrounded = false;
        groundNormal = Vector3.up;

        // if we're grounded, collect info about the ground normal with a downward capsule cast representing our character capsule
        Vector3 sphereCastStartPos = model.position + new Vector3(0, capsuleCollider.radius + groundCheckDist, 0);
        if (Physics.SphereCast(sphereCastStartPos, capsuleCollider.radius, Vector3.down, out RaycastHit hit, groundCheckDist * 2, groundLayer))
        {
            // storing the upward direction for the surface found
            groundNormal = hit.normal;

            // Only consider this a valid ground hit if the ground normal goes in the same direction as the character up
            // and if the slope angle is lower than the character controller's limit
            if (Vector3.Dot(hit.normal, transform.up) > 0f && IsNormalUnderSlopeLimit(groundNormal))
            {
                isGrounded = true;
            }
        }

        ////Rotate model
        //Vector3 newPlayerForward = (model.forward - Vector3.Project(model.forward, groundNormal)).normalized;
        //Quaternion targRotation = Quaternion.LookRotation(newPlayerForward, groundNormal);
        //model.rotation = targRotation;
    }

    // Returns true if the slope angle represented by the given normal is under the slope angle limit of the character controller
    bool IsNormalUnderSlopeLimit(Vector3 normal)
    {
        return Vector3.Angle(transform.up, normal) <= maxSlopeAngle;
    }

    // Gets the center point of the bottom hemisphere of the character controller capsule    
    Vector3 GetCapsuleBottomHemisphere()
    {
        return transform.position + (transform.up * capsuleCollider.radius);
    }

    // Gets the center point of the top hemisphere of the character controller capsule    
    Vector3 GetCapsuleTopHemisphere(float atHeight)
    {
        return transform.position + (transform.up * (atHeight - capsuleCollider.radius));
    }

    private void MoveCameraOutOfWall()
    {
        RaycastHit hit;

        if (Physics.SphereCast(cameraPivot.position, capsuleCollider.radius / 2f, -cameraPivot.forward, out hit, currMaxCamDistance, cameraWallLayer, QueryTriggerInteraction.Ignore))
        {
            //Camera hit a wall, move to prevent clipping
            camTransform.position = hit.point + hit.normal * capsuleCollider.radius;
            return;
        }

        if (Physics.Raycast(cameraPivot.position, -cameraPivot.forward, out hit, currMaxCamDistance * 1.5f, cameraWallLayer, QueryTriggerInteraction.Ignore))
        {
            //Camera hit a wall, move to prevent clipping
            camTransform.position = hit.point + hit.normal * capsuleCollider.radius;
            return;
        }

        //No walls hit
        camTransform.position = cameraPivot.position - cameraPivot.forward * currMaxCamDistance;
    }

    public override void TakeDamage(int _damageToTake)
    {
        if (IsDead)
            return;

        if (isParrying && state == PlayerStateEnum.Parrying)
        {
            //Do parry instead of dealing damage
            SFXManager.Instance.Play(SFXManager.AudioTypeEnum.Player_Parry);
        }
        else
        {
            base.TakeDamage(_damageToTake);
            SFXManager.Instance.Play(SFXManager.AudioTypeEnum.Player_OnHit);
        }
    }

    protected override void OnDie()
    {
        Debug.Log("YOU DIED");
        state = PlayerStateEnum.Dead;
    }
}
