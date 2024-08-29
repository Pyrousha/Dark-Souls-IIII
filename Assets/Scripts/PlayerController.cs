using UnityEngine;

public class PlayerController : Entity
{
    private enum PlayerStateEnum
    {
        Idle,
        Dashing,
        Attacking
    }
    private PlayerStateEnum state;

    [System.Serializable]
    private class Speedblock
    {
        [field: SerializeField] public float AccelSpeed_ground { get; private set; } = 100;
        [field: SerializeField] public float AccelSpeed_air { get; private set; } = 50;
        [field: SerializeField] public float FrictionSpeed_ground { get; private set; } = 50;
        [field: SerializeField] public float FrictionSpeed_air { get; private set; } = 25;
        [field: SerializeField] public float MaxSpeed { get; private set; } = 10;
        [field: SerializeField] public float DashStartSpeed { get; private set; } = 100;
        [field: SerializeField] public float DashEndSpeed { get; private set; } = 500;
        [field: SerializeField] public int HeavyAttackDamage { get; private set; } = 100;

        public void SetValues_Lerp(Speedblock block0, Speedblock block1, float t)
        {
            AccelSpeed_ground = Mathf.Lerp(block0.AccelSpeed_ground, block1.AccelSpeed_ground, t);
            AccelSpeed_air = Mathf.Lerp(block0.AccelSpeed_air, block1.AccelSpeed_air, t);
            FrictionSpeed_ground = Mathf.Lerp(block0.FrictionSpeed_ground, block1.FrictionSpeed_ground, t);
            FrictionSpeed_air = Mathf.Lerp(block0.FrictionSpeed_air, block1.FrictionSpeed_air, t);
            MaxSpeed = Mathf.Lerp(block0.MaxSpeed, block1.MaxSpeed, t);
            DashStartSpeed = Mathf.Lerp(block0.DashStartSpeed, block1.DashStartSpeed, t);
            DashEndSpeed = Mathf.Lerp(block0.DashEndSpeed, block1.DashEndSpeed, t);
            HeavyAttackDamage = (int)Mathf.Lerp(block0.HeavyAttackDamage, block1.HeavyAttackDamage, t);
        }
    }

    [Header("References")]
    [SerializeField] private CharacterController controller;
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
    [SerializeField] private float camMoveSpeedY;
    [SerializeField] private float camMoveSpeedX;
    [SerializeField] private float modelYRotSpeed;
    [Space(10)]
    [SerializeField] private float groundCheckDist_grounded = 2f;
    [SerializeField] private float groundCheckDist_air = 0.7f;
    [SerializeField] private float slopeShoveSpeed;
    [Space(5)]
    [SerializeField] private float gravSpeed;
    [SerializeField] private float jumpSpeed;
    [Space(5)]
    [SerializeField] private Speedblock lowSpeedblock;
    [SerializeField] private Speedblock highSpeedblock;
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

    //Attack
    private float attackStartTime;
    private float t_attackScootEndTime;
    private float t_attackEndTime;
    Speedblock heavyAttackSpeedblock = new Speedblock();
    private float currAttackScootingSpeed;
    private Vector2 attackInputDirection;

    //Stamina
    private float t_nextStaminaRechargeTime;
    private float currStamina;

    //Sprint/Speed
    private float t_nextSpeedDecreaseTime;
    private bool isSprinting = false;
    private float currSpeedState = 0;
    private float maxSpeedState = 3.5f;
    private Speedblock sprintSpeedblock = new Speedblock();
    private float speedGainPerSec;

    //Dash
    private float dashStartTime;
    private float t_dashEndTime;
    Speedblock dashSpeedblock = new Speedblock();
    private float currDashingSpeed;
    private Vector2 dashInputDirection;

    //Movement
    private Vector3 currVelocity;
    private bool isGrounded;
    private float nextGroundCheckTime;
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

    private void Update()
    {
        GroundCheck();

        //Start Heavy Attack
        if (isGrounded && InputHandler.Instance.HeavyAttack.Down && currSpeedState >= 1
            && (state == PlayerStateEnum.Idle || state == PlayerStateEnum.Dashing))
        {
            //Set velocity
            float statePercent = ((int)currSpeedState) / 3f;
            heavyAttackSpeedblock.SetValues_Lerp(lowSpeedblock, highSpeedblock, statePercent);
            if (InputHandler.Instance.PressingDirection && !uiCanvas.IsLockedOn)
                attackInputDirection = InputHandler.Instance.MoveXZ.normalized;
            else
                attackInputDirection = Vector2.up;
            currAttackScootingSpeed = heavyAttackSpeedblock.DashStartSpeed;
            currVelocity = ConvertInputDirToWorldDir(attackInputDirection) * heavyAttackSpeedblock.DashStartSpeed;

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
                    float statePercent = Mathf.Clamp(currSpeedState / 3f, 0, 1);
                    dashSpeedblock.SetValues_Lerp(lowSpeedblock, highSpeedblock, statePercent);
                    dashInputDirection = InputHandler.Instance.MoveXZ.normalized;
                    currSpeedState = Mathf.Clamp(currSpeedState, 0, maxSpeedState);
                    currDashingSpeed = dashSpeedblock.DashStartSpeed;
                    currVelocity = ConvertInputDirToWorldDir(dashInputDirection) * dashSpeedblock.DashStartSpeed;

                    //Set state vars and timers
                    dashStartTime = Time.time;
                    state = PlayerStateEnum.Dashing;
                    currSpeedState++;
                    t_dashEndTime = Time.time + m_dashDuration;
                    t_nextSpeedDecreaseTime = Mathf.Max(t_nextSpeedDecreaseTime, t_dashEndTime + m_waitTimeAfterDash);
                    t_nextStaminaRechargeTime = Mathf.Max(t_nextStaminaRechargeTime, t_dashEndTime + m_waitTimeAfterDash);

                    //Set anim params
                    c_modelAnimator.SetTrigger(ANIM_PARAM_DASH_TRIGGER);
                    if (uiCanvas.IsLockedOn)
                    {
                        c_modelAnimator.SetFloat(ANIM_PARAM_DASH_DIR_X, dashInputDirection.x);
                        c_modelAnimator.SetFloat(ANIM_PARAM_DASH_DIR_Y, dashInputDirection.y);
                    }
                    else
                    {
                        c_modelAnimator.SetFloat(ANIM_PARAM_DASH_DIR_X, 0);
                        c_modelAnimator.SetFloat(ANIM_PARAM_DASH_DIR_Y, 1);
                    }
                }
                break;

            case PlayerStateEnum.Dashing:
                if (Time.time >= t_dashEndTime)
                {
                    //End Dash
                    state = PlayerStateEnum.Idle;
                }
                else
                {
                    //Decelerate
                    currDashingSpeed = Mathf.Lerp(dashSpeedblock.DashStartSpeed, dashSpeedblock.DashEndSpeed,
                        (Time.time - dashStartTime) / m_dashDuration);
                    currVelocity = ConvertInputDirToWorldDir(dashInputDirection) * currDashingSpeed;
                }
                break;

            case PlayerStateEnum.Attacking:
                if (Time.time >= t_attackEndTime)
                {
                    //End Attack
                    state = PlayerStateEnum.Idle;
                    break;
                }

                if (Time.time <= t_attackScootEndTime)
                {
                    //Decelerate
                    currAttackScootingSpeed = Mathf.Lerp(heavyAttackSpeedblock.DashStartSpeed, heavyAttackSpeedblock.DashEndSpeed,
                        (Time.time - attackStartTime) / m_dashDuration);
                    currVelocity = ConvertInputDirToWorldDir(attackInputDirection) * currAttackScootingSpeed;
                }
                break;
        }

        isSprinting = ((InputHandler.Instance.Sprint.Holding || InputHandler.Instance.Dash.Holding) && currStamina > 0
            && isGrounded && state == PlayerStateEnum.Idle && InputHandler.Instance.PressingDirection);
        if (isSprinting)
        {
            //nextStaminaRechargeTime = Time.time + m_staminaRechargeDelay;
            currStamina -= m_sprintCostPerSec * Time.deltaTime;
        }

        if (state == PlayerStateEnum.Idle)
        {
            //Passive Regen
            if (Time.time >= t_nextStaminaRechargeTime && !isSprinting && state != PlayerStateEnum.Dashing)
            {
                currStamina = Mathf.Min(currStamina + m_staminaRechargePerSec * Time.deltaTime, m_maxStamina);
            }

            //Update sprint slider ui
            staminaSlider.Slider.value = (currStamina / m_maxStamina);


            //Speed state gain
            if (isSprinting)
                currSpeedState += speedGainPerSec * Time.deltaTime;
            else if (Time.time >= t_nextSpeedDecreaseTime)
                currSpeedState = Mathf.Max(0, currSpeedState - m_speedLossPerSec * Time.deltaTime);
        }

        currSpeedState = Mathf.Clamp(currSpeedState, 0, maxSpeedState);

        float speedPercent = Mathf.Floor(currSpeedState);
        if (isSprinting)
            speedPercent += (1 - speedPercent / 3);
        speedPercent = Mathf.Clamp(speedPercent / 3f, 0, 1);

        sprintSpeedblock.SetValues_Lerp(lowSpeedblock, highSpeedblock, speedPercent);

        //Update anim parameters
        lastSpeedStatePercent = Utils.MoveTowardsValue(lastSpeedStatePercent, speedPercent, Time.deltaTime * maxSpeedState);
        c_modelAnimator.SetFloat(ANIM_PARAM_SPEED_STATE_PERCENT, lastSpeedStatePercent);


        //Update speed slider ui
        speedSlider.SetSliderVisualState(currSpeedState);

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

        #region Player Movement
        if (state == PlayerStateEnum.Idle)
        {
            Vector3 forwardDir = cameraPivot.forward;
            forwardDir.y = 0;
            forwardDir.Normalize();

            Vector3 rightDir = cameraPivot.right.normalized;

            Vector3 worldInput;
            if (InputHandler.Instance.PressingDirection)
                worldInput = InputHandler.Instance.MoveXZ.y * forwardDir + InputHandler.Instance.MoveXZ.x * rightDir;
            else
                worldInput = Vector3.zero;

            HandleMovement(worldInput);
        }
        #endregion

        c_modelAnimator.SetBool(ANIM_PARAM_GROUNDED, isGrounded);
        c_modelAnimator.SetBool(ANIM_PARAM_SPRINTING, isSprinting);

        if (isGrounded)
        {
            currVelocity.y = 0;
        }
        else
        {
            currVelocity.y -= gravSpeed * Time.deltaTime;
        }

        if (isGrounded)
            DebugCanvas.Instance.ShowText("grounded\n" + currVelocity.ToString());
        else
            DebugCanvas.Instance.ShowText("air\n" + currVelocity.ToString());

        // apply the final calculated velocity value as a character movement
        Vector3 capsuleBottomBeforeMove = GetCapsuleBottomHemisphere();
        Vector3 capsuleTopBeforeMove = GetCapsuleTopHemisphere(controller.height);
        controller.Move(currVelocity * Time.deltaTime);


        // detect obstructions to adjust velocity accordingly
        if (Physics.CapsuleCast(capsuleBottomBeforeMove, capsuleTopBeforeMove, controller.radius,
            currVelocity.normalized, out RaycastHit hit, currVelocity.magnitude * Time.deltaTime, -1,
            QueryTriggerInteraction.Ignore))
        {
            currVelocity = Vector3.ProjectOnPlane(currVelocity, hit.normal);
        }

        #region Update facing direciton of player model
        Vector3 newForward = model.forward;
        Enemy lockedOnEnemy = uiCanvas.LockedonEnemy;
        if (lockedOnEnemy == null || isSprinting || state == PlayerStateEnum.Attacking)
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
        modelRot.y = Utils.MoveTowardsRotation(modelRot.y, targYRot, modelYRotSpeed * Time.deltaTime);
        model.eulerAngles = modelRot;
        #endregion

        MoveCameraOutOfWall();
    }

    public void SpawnHeavyHitbox()
    {
        heavyAttack.EnableHitbox(t_attackEndTime - Time.time, heavyAttackSpeedblock.HeavyAttackDamage);

        currSpeedState = 0;
        speedSlider.SetSliderVisualState(currSpeedState);
    }

    void HandleMovement(Vector3 _worldInput)
    {
        float currYVelocity = currVelocity.y;

        Vector3 modifiedVelocity_noGrav = currVelocity;
        modifiedVelocity_noGrav.y = 0;


        Vector3 targetVelocity = _worldInput * sprintSpeedblock.MaxSpeed;

        float frictionSpeed = (isGrounded ? sprintSpeedblock.FrictionSpeed_ground : sprintSpeedblock.FrictionSpeed_air) * Time.deltaTime;
        float accelSpeed = (isGrounded ? sprintSpeedblock.AccelSpeed_ground : sprintSpeedblock.AccelSpeed_air) * Time.deltaTime;

        //Apply friction
        modifiedVelocity_noGrav = modifiedVelocity_noGrav.normalized * Mathf.Max(0, modifiedVelocity_noGrav.magnitude - frictionSpeed);

        //Apply acceleration
        Vector3 fromCurrVeloticyToTarg = targetVelocity - modifiedVelocity_noGrav;
        modifiedVelocity_noGrav += fromCurrVeloticyToTarg.normalized * Mathf.Min(accelSpeed, fromCurrVeloticyToTarg.magnitude);

        //Apply new velocity;
        currVelocity = modifiedVelocity_noGrav;
        currVelocity.y = currYVelocity;
    }


    /// <summary>
    /// Sets the state of "grounded", and also handles snapping to the ground when going down slopes
    /// </summary>
    void GroundCheck()
    {
        // Make sure that the ground check distance while already in air is very small, to prevent suddenly snapping to ground
        float chosenGroundCheckDistance =
            isGrounded ? (controller.skinWidth + groundCheckDist_grounded) : groundCheckDist_air;

        // reset values before the ground check
        isGrounded = false;
        groundNormal = Vector3.up;

        // only try to detect ground if it's been a short amount of time since last jump; otherwise we may snap to the ground instantly after we try jumping
        if (Time.time >= nextGroundCheckTime)
        {
            // if we're grounded, collect info about the ground normal with a downward capsule cast representing our character capsule
            if (Physics.CapsuleCast(GetCapsuleBottomHemisphere(), GetCapsuleTopHemisphere(controller.height),
                controller.radius, Vector3.down, out RaycastHit hit, chosenGroundCheckDistance, groundLayer,
                QueryTriggerInteraction.Ignore))
            {
                // storing the upward direction for the surface found
                groundNormal = hit.normal;

                // Only consider this a valid ground hit if the ground normal goes in the same direction as the character up
                // and if the slope angle is lower than the character controller's limit
                if (Vector3.Dot(hit.normal, transform.up) > 0f && IsNormalUnderSlopeLimit(groundNormal))
                {
                    isGrounded = true;

                    // handle snapping to the ground
                    if (hit.distance > controller.skinWidth)
                    {
                        controller.Move(Vector3.down * hit.distance);
                    }
                }
            }
        }
    }

    // Returns true if the slope angle represented by the given normal is under the slope angle limit of the character controller
    bool IsNormalUnderSlopeLimit(Vector3 normal)
    {
        return Vector3.Angle(transform.up, normal) <= controller.slopeLimit;
    }

    // Gets the center point of the bottom hemisphere of the character controller capsule    
    Vector3 GetCapsuleBottomHemisphere()
    {
        return transform.position + (transform.up * controller.radius);
    }

    // Gets the center point of the top hemisphere of the character controller capsule    
    Vector3 GetCapsuleTopHemisphere(float atHeight)
    {
        return transform.position + (transform.up * (atHeight - controller.radius));
    }

    private void MoveCameraOutOfWall()
    {
        RaycastHit hit;

        if (Physics.SphereCast(cameraPivot.position, controller.radius / 2f, -cameraPivot.forward, out hit, maxCamDistance, cameraWallLayer, QueryTriggerInteraction.Ignore))
        {
            //Camera hit a wall, move to prevent clipping
            camTransform.position = hit.point + hit.normal * controller.radius;
            return;
        }

        if (Physics.Raycast(cameraPivot.position, -cameraPivot.forward, out hit, maxCamDistance * 1.5f, cameraWallLayer, QueryTriggerInteraction.Ignore))
        {
            //Camera hit a wall, move to prevent clipping
            camTransform.position = hit.point + hit.normal * controller.radius;
            return;
        }

        //No walls hit
        camTransform.position = cameraPivot.position - cameraPivot.forward * maxCamDistance;
    }

    protected override void OnDie()
    {
        Debug.Log("YOU DIED");
    }
}
