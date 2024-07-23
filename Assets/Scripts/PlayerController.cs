using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterController controller;
    [SerializeField] private Transform model;
    [SerializeField] private Animator anim;
    [SerializeField] private Transform groundPivot;
    [SerializeField] private Transform spherePivot;
    [Space(10)]
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
    [SerializeField] private float sprintSpeedMultiplier;

    private Vector3 currVelocity;
    private bool grounded;
    private float nextGroundCheckTime;
    private Vector3 groundNormal;

    private int ANIM_PARAM_GROUNDED = Animator.StringToHash("Grounded");
    private int ANIM_PARAM_SPRINTING = Animator.StringToHash("Sprinting");

    [Header("Misc")]
    [SerializeField] private LayerMask groundLayer;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Update()
    {
        #region Camera Rotation
        float camXRot = cameraPivot.eulerAngles.x - InputHandler.Instance.Look.y * camMoveSpeedY * Time.deltaTime;

        if (camXRot > 180)
            camXRot -= 360;
        camXRot = Mathf.Clamp(camXRot, -60f, 89.5f);

        float camYRot = cameraPivot.eulerAngles.y + InputHandler.Instance.Look.x * camMoveSpeedX * Time.deltaTime;
        cameraPivot.eulerAngles = new Vector3(camXRot, camYRot, 0);
        #endregion

        #region Player Movement
        //if (InputHandler.Instance.MoveXZ.sqrMagnitude > 0.1f)
        //{
        Vector3 forwardDir = cameraPivot.forward;
        forwardDir.y = 0;
        forwardDir.Normalize();

        Vector3 rightDir = cameraPivot.right.normalized;

        Vector3 worldInput = InputHandler.Instance.MoveXZ.y * forwardDir + InputHandler.Instance.MoveXZ.x * rightDir;

        HandleInput(worldInput);

        ////TODO: apply acceleration
        //float currYVelocity = currVelocity.y;
        //currVelocity = maxSpeed * worldInput;
        //if (InputHandler.Instance.Sprint.Holding)
        //    currVelocity *= 2;
        //currVelocity.y = currYVelocity;
        //}
        //else
        //{
        //    //TODO: apply deceleration
        //    currVelocity.x = 0;
        //    currVelocity.z = 0;
        //}
        #endregion

        //bool wasGrouned = grounded;

        GroundCheck();

        anim.SetBool(ANIM_PARAM_GROUNDED, grounded);
        anim.SetBool(ANIM_PARAM_SPRINTING, InputHandler.Instance.Sprint.Holding);

        if (grounded)
        {
            currVelocity.y = 0;

            if (InputHandler.Instance.Jump.Down)
            {
                currVelocity.y = jumpSpeed;
                nextGroundCheckTime = Time.time + 0.25f;
            }
        }
        else
        {
            currVelocity.y -= gravSpeed * Time.deltaTime;
        }

        if (grounded)
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



    void HandleInput(Vector3 _worldInput)
    {
        float currYVelocity = currVelocity.y;

        Vector3 modifiedVelocity_noGrav = currVelocity;
        modifiedVelocity_noGrav.y = 0;

        //float frictionSpeed = (grounded ? frictionSpeed_ground : frictionSpeed_air) * Time.deltaTime;
        //Vector3 frictionToApply;

        bool isSprinting = InputHandler.Instance.Sprint.Holding;

        Vector3 updatedVelocity;

        float maxSpeed = m_maxSpeed;
        if (isSprinting)
            maxSpeed *= 2;

        #region Acceleration
        if (_worldInput.magnitude > 1f)
            _worldInput.Normalize();
        if (grounded)
        {
            float frictionSpeed = m_frictionSpeed_ground * Time.deltaTime;
            float accelSpeed = m_accelSpeed_ground * Time.deltaTime;
            if (isSprinting)
                accelSpeed *= sprintSpeedMultiplier;

            //Apply ground fricion
            Vector3 velocity_friction = modifiedVelocity_noGrav.normalized * Mathf.Max(0, modifiedVelocity_noGrav.magnitude - frictionSpeed);

            updatedVelocity = velocity_friction;

            if (_worldInput.magnitude > 0.05f) //Pressing something, try to accelerate
            {
                Vector3 velocity_local_input = velocity_friction + _worldInput * accelSpeed;

                if (velocity_friction.magnitude <= maxSpeed)
                {
                    //under max speed, accelerate towards max speed
                    updatedVelocity = velocity_local_input.normalized * Mathf.Min(maxSpeed, velocity_local_input.magnitude);
                }
                else
                {
                    //over max speed
                    if (velocity_local_input.magnitude <= maxSpeed) //Use new direction, would go less than max speed
                    {
                        updatedVelocity = velocity_local_input;
                    }
                    else //Would stay over max speed, use vector with smaller magnitude
                    {
                        //Would accelerate more, so don't user player input
                        if (velocity_local_input.magnitude > velocity_friction.magnitude)
                            updatedVelocity = velocity_friction;
                        else
                            //Would accelerate less, user player input (input moves velocity more to 0,0 than just friciton)
                            updatedVelocity = velocity_local_input;
                    }
                }
            }
        }
        else
        {
            float frictionSpeed = m_frictionSpeed_air * Time.deltaTime;

            float accelSpeed = m_accelSpeed_air * Time.deltaTime;
            if (isSprinting)
                accelSpeed *= sprintSpeedMultiplier;

            //Apply air fricion
            Vector3 velocity_local_friction = modifiedVelocity_noGrav.normalized * Mathf.Max(0, modifiedVelocity_noGrav.magnitude - frictionSpeed);

            updatedVelocity = velocity_local_friction;

            if (_worldInput.magnitude > 0.05f) //Pressing something, try to accelerate
            {
                Vector3 inputVect = _worldInput * accelSpeed;

                Vector3 velocity_local_with_input = velocity_local_friction + inputVect;

                if (velocity_local_friction.magnitude <= maxSpeed)
                {
                    //under max speed, accelerate towards max speed
                    updatedVelocity = velocity_local_with_input.normalized * Mathf.Min(maxSpeed, velocity_local_with_input.magnitude);
                }
                else
                {
                    float velocityOntoInput = Vector3.Project(velocity_local_with_input, inputVect).magnitude;
                    if (Vector3.Dot(velocity_local_with_input, inputVect) < 0)
                        velocityOntoInput *= -1;

                    if (velocityOntoInput <= maxSpeed)
                    {
                        //Speed in direction of input lower than maxSpeed
                        updatedVelocity = velocity_local_with_input;
                    }
                    else
                    {
                        //Would accelerate more, so don't user player input directly

                        Vector3 velocityOntoFriction = Vector3.Project(velocity_local_friction, inputVect);

                        Vector3 perp = velocity_local_friction - velocityOntoFriction;

                        //Accelerate towards max speed
                        float amountToAdd = Mathf.Max(0, Mathf.Min(maxSpeed - velocityOntoFriction.magnitude, inputVect.magnitude));
                        float perpAmountToSubtract = Mathf.Max(0, Mathf.Min(accelSpeed - amountToAdd, perp.magnitude));

                        perp = perp.normalized * perpAmountToSubtract;

                        updatedVelocity = velocity_local_friction + amountToAdd * inputVect.normalized - perp;
                    }
                }
            }
        }
        #endregion








        //if (_worldInput.magnitude > 0.05)
        //{
        //    float accelSpeed = (grounded ? accelSpeed_ground : accelSpeed_air) * Time.deltaTime;
        //    Vector3 accelToApply = _worldInput * accelSpeed;

        //    Vector3 directAppliedVelocity = modifiedVelocity_noGrav + accelToApply;
        //    Vector3 projectedVelocity = Vector3.Project(modifiedVelocity_noGrav, _worldInput.normalized);
        //    float projVelocityStrength = Vector3.Dot(modifiedVelocity_noGrav, projectedVelocity);
        //    Vector3 perpendicularVelocity = modifiedVelocity_noGrav - projectedVelocity;

        //    if (directAppliedVelocity.magnitude <= maxSpeed)
        //    {
        //        //Can just use input directly
        //        modifiedVelocity_noGrav = directAppliedVelocity;

        //        //Apply perpendicular friction
        //        float leftoverFriction = Mathf.Min(frictionSpeed, perpendicularVelocity.magnitude);
        //        frictionToApply = perpendicularVelocity.normalized * leftoverFriction;
        //    }
        //    else
        //    {
        //        //Velocity already over max speed without needing input
        //        if (projVelocityStrength >= maxSpeed)
        //        {
        //            //How much faster the player is going in the input direction over max speed
        //            float speedOverMax = projVelocityStrength - maxSpeed;

        //            if (speedOverMax >= frictionSpeed)
        //            {
        //                //Just applying friction directly to velocity "uses it all"
        //                frictionToApply = -modifiedVelocity_noGrav.normalized * frictionSpeed;
        //            }
        //            else
        //            {
        //                //Apply friction mainly to taking projected velocity <= maxSpeed
        //                frictionToApply = -modifiedVelocity_noGrav.normalized * speedOverMax;

        //                //Need to also apply perpendicular friction to "use it all"
        //                float leftoverFriction = frictionSpeed - speedOverMax; //always will be positive
        //                leftoverFriction = Mathf.Min(leftoverFriction, perpendicularVelocity.magnitude);
        //                frictionToApply -= perpendicularVelocity.normalized * leftoverFriction;
        //            }
        //        }
        //        else
        //        {
        //            //Apply acceleration up to max speed
        //            float speedToAdd = Mathf.Min(maxSpeed - projVelocityStrength, accelSpeed);
        //            modifiedVelocity_noGrav += _worldInput.normalized * speedToAdd;

        //            //Apply perpendicular friction
        //            perpendicularVelocity = modifiedVelocity_noGrav - projectedVelocity;
        //            float leftoverFriction = Mathf.Min(frictionSpeed, perpendicularVelocity.magnitude);
        //            frictionToApply = perpendicularVelocity.normalized * leftoverFriction;
        //        }
        //    }
        //}
        //else
        //{
        //    frictionToApply = -modifiedVelocity_noGrav * Mathf.Max(0, frictionSpeed);
        //}

        //currVelocity = modifiedVelocity_noGrav + frictionToApply;
        currVelocity = updatedVelocity;

        if (currVelocity.magnitude > 0.1f)
            model.forward = currVelocity.normalized;

        currVelocity.y = currYVelocity;

        //model.forward = modifiedVelocity_noGrav.normalized;
    }


    /// <summary>
    /// Sets the state of "grounded", and also handles snapping to the ground when going down slopes
    /// </summary>
    void GroundCheck()
    {
        // Make sure that the ground check distance while already in air is very small, to prevent suddenly snapping to ground
        float chosenGroundCheckDistance =
            grounded ? (controller.skinWidth + groundCheckDist_grounded) : groundCheckDist_air;

        // reset values before the ground check
        grounded = false;
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
                    grounded = true;

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

        if (Physics.SphereCast(cameraPivot.position, controller.radius / 2f, -cameraPivot.forward, out hit, maxCamDistance, groundLayer, QueryTriggerInteraction.Ignore))
        {
            //Camera hit a wall, move to prevent clipping
            camTransform.position = hit.point + hit.normal * controller.radius;
            return;
        }

        if (Physics.Raycast(cameraPivot.position, -cameraPivot.forward, out hit, maxCamDistance * 1.5f, groundLayer, QueryTriggerInteraction.Ignore))
        {
            //Camera hit a wall, move to prevent clipping
            camTransform.position = hit.point + hit.normal * controller.radius;
            return;
        }

        //No walls hit
        camTransform.position = cameraPivot.position - cameraPivot.forward * maxCamDistance;
    }
}
