using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterController controller;
    [SerializeField] private Transform model;
    [Space(10)]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private Transform camTransform;

    [Header("Parameters")]
    [SerializeField] private float maxCamDistance;
    [SerializeField] private float camMoveSpeedY;
    [SerializeField] private float camMoveSpeedX;
    [Space(10)]
    [SerializeField] private float accelSpeed;
    [SerializeField] private float frictionSpeed;
    [SerializeField] private float moveSpeed;

    private Vector3 currVelocity;

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
        if (InputHandler.Instance.MoveXZ.sqrMagnitude > 0.1f)
        {
            Vector3 forwardDir = cameraPivot.forward;
            forwardDir.y = 0;
            forwardDir.Normalize();

            Vector3 rightDir = cameraPivot.right;

            Vector3 worldInput = InputHandler.Instance.MoveXZ.y * forwardDir + InputHandler.Instance.MoveXZ.x * rightDir;

            controller.Move(moveSpeed * Time.deltaTime * worldInput);

            model.forward = worldInput;
        }
        #endregion

        MoveCameraOutOfWall();
    }

    private void MoveCameraOutOfWall()
    {
        RaycastHit hit;

        if (Physics.SphereCast(cameraPivot.position, controller.radius / 2f, -cameraPivot.forward, out hit, maxCamDistance, groundLayer, QueryTriggerInteraction.Ignore))
        {
            //Camera hit a wall, move to prevent clipping
            camTransform.position = hit.point + hit.normal * controller.radius;
            DebugCanvas.Instance.ShowText("<color=red>Hit!</color>");
            return;
        }

        if (Physics.Raycast(cameraPivot.position, -cameraPivot.forward, out hit, maxCamDistance * 1.5f, groundLayer, QueryTriggerInteraction.Ignore))
        {
            //Camera hit a wall, move to prevent clipping
            camTransform.position = hit.point + hit.normal * controller.radius;
            DebugCanvas.Instance.ShowText("<color=red>Hit!</color>");
            return;
        }

        //No walls hit
        camTransform.position = cameraPivot.position - cameraPivot.forward * maxCamDistance;
        DebugCanvas.Instance.ShowText("No Hit");
    }
}
