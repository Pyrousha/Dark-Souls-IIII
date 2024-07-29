using UnityEngine;

public class PlayerController : MonoBehaviour
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

        public void SetValues_Lerp(Speedblock block0, Speedblock block1, float t)
        {
            AccelSpeed_ground = Mathf.Lerp(block0.AccelSpeed_ground, block1.AccelSpeed_ground, t);
            AccelSpeed_air = Mathf.Lerp(block0.AccelSpeed_air, block1.AccelSpeed_air, t);
            FrictionSpeed_ground = Mathf.Lerp(block0.FrictionSpeed_ground, block1.FrictionSpeed_ground, t);
            FrictionSpeed_air = Mathf.Lerp(block0.FrictionSpeed_air, block1.FrictionSpeed_air, t);
            MaxSpeed = Mathf.Lerp(block0.MaxSpeed, block1.MaxSpeed, t);
            DashStartSpeed = Mathf.Lerp(block0.DashStartSpeed, block1.DashStartSpeed, t);
            DashEndSpeed = Mathf.Lerp(block0.DashEndSpeed, block1.DashEndSpeed, t);
        }
    }

    [Header("References")]
    [SerializeField] private CharacterController controller;
    [SerializeField] private Transform model;
    [SerializeField] private Animator anim;
    [SerializeField] private Transform groundPivot;
    [SerializeField] private Transform spherePivot;
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

    //Stamina
    private float t_nextStaminaRechargeTime;
    private float currStamina;

    //Sprint/Speed
    private float t_nextSpeedDecreaseTime;
    private bool isSprinting = false;
    private float currSpeedState = 0;
    private float maxSpeedState = 3.5f;
    private Speedblock currSpeedblock = new Speedblock();
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

    private void Awake()
    {
        currStamina = m_maxStamina / 2f;
        speedGainPerSec = (m_sprintCostPerSec / m_sprintCostPerSpeedState);
    }

    private void Start()
    {
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

        #region Stamina
        {
            switch (state)
            {
                case PlayerStateEnum.Idle:
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
                        currVelocity = ConvertInputDirToWorldDir(dashInputDirection) * dashSpeedblock.DashStartSpeed;
                        currDashingSpeed = dashSpeedblock.DashStartSpeed;

                        //Set state vars and timers
                        dashStartTime = Time.time;
                        state = PlayerStateEnum.Dashing;
                        currSpeedState++;
                        t_dashEndTime = Time.time + m_dashDuration;
                        t_nextSpeedDecreaseTime = Mathf.Max(t_nextSpeedDecreaseTime, t_dashEndTime + m_waitTimeAfterDash);
                        t_nextStaminaRechargeTime = Mathf.Max(t_nextStaminaRechargeTime, t_dashEndTime + m_waitTimeAfterDash);

                        //Set anim params
                        anim.SetTrigger(ANIM_PARAM_DASH_TRIGGER);
                        if (uiCanvas.IsLockedOn)
                        {
                            anim.SetFloat(ANIM_PARAM_DASH_DIR_X, dashInputDirection.x);
                            anim.SetFloat(ANIM_PARAM_DASH_DIR_Y, dashInputDirection.y);
                        }
                        else
                        {
                            anim.SetFloat(ANIM_PARAM_DASH_DIR_X, 0);
                            anim.SetFloat(ANIM_PARAM_DASH_DIR_Y, 1);
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
                        currDashingSpeed = Mathf.Lerp(dashSpeedblock.DashStartSpeed, dashSpeedblock.DashEndSpeed, (Time.time - dashStartTime) / m_dashDuration);
                        currVelocity = ConvertInputDirToWorldDir(dashInputDirection) * currDashingSpeed;
                    }
                    break;

                case PlayerStateEnum.Attacking:
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

            currSpeedblock.SetValues_Lerp(lowSpeedblock, highSpeedblock, speedPercent);

            //Update anim parameters
            lastSpeedStatePercent = Utils.MoveTowardsValue(lastSpeedStatePercent, speedPercent, Time.deltaTime * maxSpeedState);
            anim.SetFloat(ANIM_PARAM_SPEED_STATE_PERCENT, lastSpeedStatePercent);


            //Update speed slider ui
            speedSlider.SetSliderVisualState(currSpeedState);
        }
        #endregion

        #region Camera Rotation
        if (uiCanvas.LockonState == UICanvas.LockonStateEnum.Unlocked)
        {
            float camXRot = cameraPivot.eulerAngles.x - InputHandler.Instance.Look.y * camMoveSpeedY * Time.deltaTime;

            if (camXRot > 180)
                camXRot -= 360;
            camXRot = Mathf.Clamp(camXRot, -60f, 89.5f);

            float camYRot = cameraPivot.eulerAngles.y + InputHandler.Instance.Look.x * camMoveSpeedX * Time.deltaTime;
            cameraPivot.eulerAngles = new Vector3(camXRot, camYRot, 0);
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

        anim.SetBool(ANIM_PARAM_GROUNDED, isGrounded);
        anim.SetBool(ANIM_PARAM_SPRINTING, isSprinting);

        if (isGrounded)
        {
            currVelocity.y = 0;

            //if (InputHandler.Instance.Jump.Down)
            //{
            //    currVelocity.y = jumpSpeed;
            //    nextGroundCheckTime = Time.time + 0.25f;
            //}
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
        Enemy lockedOnEnemy = uiCanvas.LockedonEnemy;
        if (lockedOnEnemy == null || isSprinting)
        {
            Vector3 gravitylessVelocity = currVelocity;
            gravitylessVelocity.y = 0;

            if (gravitylessVelocity.sqrMagnitude > 0.1f)
                model.forward = gravitylessVelocity.normalized;
        }
        else
        {
            Vector3 toEnemy = lockedOnEnemy.transform.position - transform.position;
            toEnemy.y = 0;
            model.forward = toEnemy.normalized;
        }
        #endregion

        MoveCameraOutOfWall();
    }

    void HandleMovement(Vector3 _worldInput)
    {
        float currYVelocity = currVelocity.y;

        Vector3 modifiedVelocity_noGrav = currVelocity;
        modifiedVelocity_noGrav.y = 0;


        Vector3 targetVelocity = _worldInput * currSpeedblock.MaxSpeed;

        float frictionSpeed = (isGrounded ? currSpeedblock.FrictionSpeed_ground : currSpeedblock.FrictionSpeed_air) * Time.deltaTime;
        float accelSpeed = (isGrounded ? currSpeedblock.AccelSpeed_ground : currSpeedblock.AccelSpeed_air) * Time.deltaTime;

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

    //void HandleCharacterMovement()
    //{
    //    // horizontal character rotation
    //    {
    //        // rotate the transform with the input speed around its local Y axis
    //        transform.Rotate(
    //            new Vector3(0f, (m_InputHandler.GetLookInputsHorizontal() * RotationSpeed * RotationMultiplier),
    //                0f), Space.Self);
    //    }

    //    // vertical camera rotation
    //    {
    //        // add vertical inputs to the camera's vertical angle
    //        m_CameraVerticalAngle += m_InputHandler.GetLookInputsVertical() * RotationSpeed * RotationMultiplier;

    //        // limit the camera's vertical angle to min/max
    //        m_CameraVerticalAngle = Mathf.Clamp(m_CameraVerticalAngle, -89f, 89f);

    //        // apply the vertical angle as a local rotation to the camera transform along its right axis (makes it pivot up and down)
    //        PlayerCamera.transform.localEulerAngles = new Vector3(m_CameraVerticalAngle, 0, 0);
    //    }

    //    // character movement handling
    //    bool isSprinting = m_InputHandler.GetSprintInputHeld();
    //    {
    //        if (isSprinting)
    //        {
    //            isSprinting = SetCrouchingState(false, false);
    //        }

    //        float speedModifier = isSprinting ? SprintSpeedModifier : 1f;

    //        // converts move input to a worldspace vector based on our character's transform orientation
    //        Vector3 worldspaceMoveInput = transform.TransformVector(m_InputHandler.GetMoveInput());

    //        // handle grounded movement
    //        if (IsGrounded)
    //        {
    //            // calculate the desired velocity from inputs, max speed, and current slope
    //            Vector3 targetVelocity = worldspaceMoveInput * MaxSpeedOnGround * speedModifier;
    //            // reduce speed if crouching by crouch speed ratio
    //            if (IsCrouching)
    //                targetVelocity *= MaxSpeedCrouchedRatio;
    //            targetVelocity = GetDirectionReorientedOnSlope(targetVelocity.normalized, m_GroundNormal) *
    //                             targetVelocity.magnitude;

    //            // smoothly interpolate between our current velocity and the target velocity based on acceleration speed
    //            CharacterVelocity = Vector3.Lerp(CharacterVelocity, targetVelocity,
    //                MovementSharpnessOnGround * Time.deltaTime);

    //            // jumping
    //            if (IsGrounded && m_InputHandler.GetJumpInputDown())
    //            {
    //                // force the crouch state to false
    //                if (SetCrouchingState(false, false))
    //                {
    //                    // start by canceling out the vertical component of our velocity
    //                    CharacterVelocity = new Vector3(CharacterVelocity.x, 0f, CharacterVelocity.z);

    //                    // then, add the jumpSpeed value upwards
    //                    CharacterVelocity += Vector3.up * JumpForce;

    //                    // play sound
    //                    AudioSource.PlayOneShot(JumpSfx);

    //                    // remember last time we jumped because we need to prevent snapping to ground for a short time
    //                    m_LastTimeJumped = Time.time;
    //                    HasJumpedThisFrame = true;

    //                    // Force grounding to false
    //                    IsGrounded = false;
    //                    m_GroundNormal = Vector3.up;
    //                }
    //            }

    //            // footsteps sound
    //            float chosenFootstepSfxFrequency =
    //                (isSprinting ? FootstepSfxFrequencyWhileSprinting : FootstepSfxFrequency);
    //            if (m_FootstepDistanceCounter >= 1f / chosenFootstepSfxFrequency)
    //            {
    //                m_FootstepDistanceCounter = 0f;
    //                AudioSource.PlayOneShot(FootstepSfx);
    //            }

    //            // keep track of distance traveled for footsteps sound
    //            m_FootstepDistanceCounter += CharacterVelocity.magnitude * Time.deltaTime;
    //        }
    //        // handle air movement
    //        else
    //        {
    //            // add air acceleration
    //            CharacterVelocity += worldspaceMoveInput * AccelerationSpeedInAir * Time.deltaTime;

    //            // limit air speed to a maximum, but only horizontally
    //            float verticalVelocity = CharacterVelocity.y;
    //            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(CharacterVelocity, Vector3.up);
    //            horizontalVelocity = Vector3.ClampMagnitude(horizontalVelocity, MaxSpeedInAir * speedModifier);
    //            CharacterVelocity = horizontalVelocity + (Vector3.up * verticalVelocity);

    //            // apply the gravity to the velocity
    //            CharacterVelocity += GravityDownForce * Time.deltaTime * Vector3.down;
    //        }
    //    }

    //    // apply the final calculated velocity value as a character movement
    //    Vector3 capsuleBottomBeforeMove = GetCapsuleBottomHemisphere();
    //    Vector3 capsuleTopBeforeMove = GetCapsuleTopHemisphere(controller.height);
    //    controller.Move(CharacterVelocity * Time.deltaTime);

    //    // detect obstructions to adjust velocity accordingly
    //    m_LatestImpactSpeed = Vector3.zero;
    //    if (Physics.CapsuleCast(capsuleBottomBeforeMove, capsuleTopBeforeMove, controller.radius,
    //        CharacterVelocity.normalized, out RaycastHit hit, CharacterVelocity.magnitude * Time.deltaTime, -1,
    //        QueryTriggerInteraction.Ignore))
    //    {
    //        // We remember the last impact speed because the fall damage logic might need it
    //        m_LatestImpactSpeed = CharacterVelocity;

    //        CharacterVelocity = Vector3.ProjectOnPlane(CharacterVelocity, hit.normal);
    //    }
    //}












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
}
