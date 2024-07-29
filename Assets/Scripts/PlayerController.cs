using BeauRoutine;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public enum PlayerStateEnum
    {
        Idle,
        Dashing,
        Attacking
    }
    private PlayerStateEnum state;

    [Header("References")]
    [SerializeField] private CharacterController controller;
    [SerializeField] private Transform model;
    [SerializeField] private Animator anim;
    [SerializeField] private Transform groundPivot;
    [SerializeField] private Transform spherePivot;
    [Space(10)]
    [SerializeField] private UICanvas uiCanvas;
    [SerializeField] private StaminaSlider staminaSlider;
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
    [SerializeField] private float m_accelSpeed_ground;
    [SerializeField] private float m_accelSpeed_air;
    [SerializeField] private float m_frictionSpeed_ground;
    [SerializeField] private float m_frictionSpeed_air;
    [SerializeField] private float m_maxSpeed;
    [SerializeField] private float m_sprintSpeedMultiplier;
    [Space(5)]
    [SerializeField] private float m_maxStamina;
    [SerializeField] private float m_staminaRechargePerSec;
    [SerializeField] private float m_staminaRechargeDelay;
    [SerializeField] private float m_sprintCostPerSec;
    [SerializeField] private float m_dashCost;

    private bool isSprinting = false;
    private float currStamina;
    private float nextStaminaRechargeTime;
    private Routine dashRoutine;

    private Vector3 currVelocity;
    private bool isGrounded;
    private float nextGroundCheckTime;
    private Vector3 groundNormal;

    private int ANIM_PARAM_GROUNDED = Animator.StringToHash("Grounded");
    private int ANIM_PARAM_SPRINTING = Animator.StringToHash("Sprinting");

    [Header("Misc")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask cameraWallLayer;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        currStamina = m_maxSpeed;
    }



    private void Update()
    {
        #region Stamina
        {
            //Passive Regen
            if (Time.time >= nextStaminaRechargeTime && !isSprinting && state != PlayerStateEnum.Dashing)
            {
                currStamina = Mathf.Min(currStamina + m_staminaRechargePerSec * Time.deltaTime, m_maxStamina);
            }

            //Check for sprinting state
            isSprinting = (InputHandler.Instance.Sprint.Holding && currStamina > 0 && state == PlayerStateEnum.Idle);
            if (isSprinting)
            {
                nextStaminaRechargeTime = Time.time + m_staminaRechargeDelay;
                currStamina -= m_sprintCostPerSec * Time.deltaTime;
            }
        }

        //Update UI
        staminaSlider.Slider.value = (currStamina / m_maxStamina);
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

            Vector3 worldInput = InputHandler.Instance.MoveXZ.y * forwardDir + InputHandler.Instance.MoveXZ.x * rightDir;

            HandleMovement(worldInput);
        }
        #endregion

        GroundCheck();

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


        MoveCameraOutOfWall();
    }



    void HandleMovement(Vector3 _worldInput)
    {
        float currYVelocity = currVelocity.y;

        Vector3 modifiedVelocity_noGrav = currVelocity;
        modifiedVelocity_noGrav.y = 0;

        Vector3 targetVelocity = _worldInput * m_maxSpeed;
        if (isSprinting)
            targetVelocity *= m_sprintSpeedMultiplier;

        float frictionSpeed = (isGrounded ? m_frictionSpeed_ground : m_frictionSpeed_air) * Time.deltaTime;
        float accelSpeed = (isGrounded ? m_accelSpeed_ground : m_accelSpeed_air) * Time.deltaTime;

        //Apply friction
        modifiedVelocity_noGrav = modifiedVelocity_noGrav.normalized * Mathf.Max(0, modifiedVelocity_noGrav.magnitude - frictionSpeed);

        //Apply acceleration
        Vector3 fromCurrVeloticyToTarg = targetVelocity - modifiedVelocity_noGrav;
        modifiedVelocity_noGrav += fromCurrVeloticyToTarg.normalized * Mathf.Min(accelSpeed, fromCurrVeloticyToTarg.magnitude);

        //Apply new velocity;
        currVelocity = modifiedVelocity_noGrav;
        currVelocity.y = currYVelocity;

        if (modifiedVelocity_noGrav.magnitude > 0.1f)
            model.forward = modifiedVelocity_noGrav.normalized;
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
