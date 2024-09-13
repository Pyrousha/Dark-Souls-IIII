using BeauRoutine;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UICanvas : MonoBehaviour
{
    private RectTransform c_RectTransform;

    [SerializeField] private RectTransform lockonRect;
    [SerializeField] private Image lockonImage;
    [Space(10)]
    [SerializeField] private float lockonCamSpinSpeed;
    [SerializeField] private float lockonDuration;
    [SerializeField] private Transform cameraPivot;
    [Space(10)]
    [SerializeField] private float sqrMagnitudeForLockon;
    [SerializeField] private LayerMask layerToBlockLockon;

    public Enemy LockedonEnemy { get; private set; }
    public bool IsLockedOn => LockedonEnemy != null;

    public enum LockonStateEnum
    {
        Unlocked,
        LockingOn,
        Locked
    }
    public LockonStateEnum LockonState { get; private set; } = LockonStateEnum.Unlocked;
    private float lockedOnCameraAngleX;

    private Routine lockonRoutine;

    private Vector3 lastLockedonEnemyPosition;

    private void Start()
    {
        c_RectTransform = GetComponent<RectTransform>();

        lockedOnCameraAngleX = cameraPivot.eulerAngles.x;

        Color newColor = lockonImage.color;
        newColor.a = 0;
        lockonImage.color = newColor;
    }

    IEnumerator StartLockon()
    {
        float startingCamRotX = cameraPivot.eulerAngles.x;
        float startingCamRotY = cameraPivot.eulerAngles.y;
        float startingAlpha = lockonImage.color.a;
        Vector3 newIconRot = lockonImage.transform.rotation.eulerAngles;

        yield return Tween.Float(0, 1, (t) =>
        {
            //Set color
            Color newColor = lockonImage.color;
            newColor.a = Mathf.Lerp(startingAlpha, 1, t);
            lockonImage.color = newColor;

            //Set rotation and scale
            lockonImage.transform.localScale = Vector3.one * t;
            newIconRot.z = Mathf.Lerp(0, -360, t);
            lockonImage.transform.eulerAngles = newIconRot;

            Vector3 cameraRotation = cameraPivot.eulerAngles;
            cameraRotation.x = Utils.LerpDegrees(startingCamRotX, lockedOnCameraAngleX, t);
            cameraRotation.y = Utils.LerpDegrees(startingCamRotY, GetCamYRot(), t);
            cameraPivot.eulerAngles = cameraRotation;
        }, lockonDuration * (1 - startingAlpha)).Ease(Curve.CubeInOut);

        LockonState = LockonStateEnum.Locked;
    }

    IEnumerator EndLockon()
    {
        float startingAlpha = lockonImage.color.a;
        Vector3 newIconRot = lockonImage.transform.rotation.eulerAngles;

        if (startingAlpha > 0)
        {
            yield return Tween.Float(startingAlpha, 0, (t) =>
            {
                //Update position
                SetUIPositionToWorldPosition(lockonRect, lastLockedonEnemyPosition);

                //Set color
                Color newColor = lockonImage.color;
                newColor.a = t;
                lockonImage.color = newColor;

                //Set rotation and scale
                lockonImage.transform.localScale = Vector3.one * t;
                newIconRot.z = Mathf.Lerp(0, -360, t);
                lockonImage.transform.eulerAngles = newIconRot;
            }, lockonDuration * (startingAlpha)).Ease(Curve.CubeInOut);
        }
    }

    /// <summary>
    /// Gets the position of the current enemy's locked-on pivot
    /// </summary>
    /// <param name="pos"></param>
    /// <returns>If there is a current enemy locked onto</returns>
    private bool GetEnemyLockonPivotPos(out Vector3 pos)
    {
        if (LockedonEnemy == null || LockedonEnemy.IsDead)
        {
            pos = Vector3.zero;
            return false;
        }

        pos = LockedonEnemy.LockonPivot.position;
        return true;
    }

    private float GetCamYRot(bool _interpolate = false)
    {
        if (LockedonEnemy == null || LockedonEnemy.IsDead)
            return cameraPivot.eulerAngles.y;

        //Change camera angle
        Vector3 newCamForward = (LockedonEnemy.HPBarPivot.position - cameraPivot.position);

        float newAngle = Mathf.Atan2(newCamForward.x, newCamForward.z) * Mathf.Rad2Deg;

        if (!_interpolate)
            return newAngle;

        float currAngle = cameraPivot.eulerAngles.y;
        return Utils.MoveTowardsRotation_Degrees(currAngle, newAngle, Time.deltaTime * lockonCamSpinSpeed);
    }

    // Update is called once per frame
    void Update()
    {
        if (InputHandler.Instance.LockOn.Down)
        {
            switch (LockonState)
            {
                case LockonStateEnum.Unlocked:
                    //Get enemy to lock-on
                    Enemy closestEnemy = null;
                    float closestDist = sqrMagnitudeForLockon;
                    foreach (Enemy enemy in EnemyManager.Instance.AliveEnemies)
                    {
                        //If camera is facing towards enemy
                        Vector3 screenPos = Camera.main.WorldToViewportPoint(enemy.LockonPivot.position);
                        if (screenPos.z < 0)
                            continue;

                        //Don't lock-on to an enemy that's blocked by terrain
                        if (Physics.Linecast(Camera.main.transform.position, enemy.LockonPivot.position, layerToBlockLockon))
                            continue;

                        //Check distance between lockon position and center of camera
                        screenPos.z = 0;
                        screenPos -= new Vector3(0.5f, 0.5f);
                        float dist = screenPos.sqrMagnitude;
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            closestEnemy = enemy;
                        }
                    }

                    if (closestEnemy != null)
                    {
                        lockonRoutine.Stop();
                        lockonRoutine = Routine.Start(this, StartLockon());
                        LockonState = LockonStateEnum.LockingOn;

                        LockedonEnemy = closestEnemy;
                    }

                    break;

                case LockonStateEnum.LockingOn:
                    Helper_EndLockon();
                    break;

                case LockonStateEnum.Locked:
                    Helper_EndLockon();
                    break;
            }
        }

        //Set position of lockon reticle
        switch (LockonState)
        {
            case LockonStateEnum.LockingOn:
                SetUIPositionToWorldPosition(lockonRect, LockedonEnemy.LockonPivot.position);
                break;

            case LockonStateEnum.Locked:
                Vector3 cameraAngles = cameraPivot.eulerAngles;
                cameraAngles.y = GetCamYRot(true);
                cameraPivot.eulerAngles = cameraAngles;

                SetUIPositionToWorldPosition(lockonRect, LockedonEnemy.LockonPivot.position);
                break;
        }

        //Set position of healthbars
        foreach (Enemy enemy in EnemyManager.Instance.AliveEnemies)
        {
            //Don't show healthbars for enemies that are full health and not locked-on
            if (enemy.IsDead || (enemy.IsHpMax && enemy != LockedonEnemy))
            {
                if (enemy.EnemyHealthbar.gameObject.activeSelf)
                    enemy.EnemyHealthbar.gameObject.SetActive(false);

                continue;
            }

            SetUIPositionToWorldPosition(enemy.EnemyHealthbar.C_RectTransform, enemy.HPBarPivot.position, true);
        }
    }

    private void Helper_EndLockon()
    {
        lockonRoutine.Stop();
        LockonState = LockonStateEnum.Unlocked;
        lastLockedonEnemyPosition = LockedonEnemy.LockonPivot.position;
        LockedonEnemy = null;

        lockonRoutine = Routine.Start(this, EndLockon());
    }

    private void SetUIPositionToWorldPosition(RectTransform _uiObject, Vector3 _worldObjectPos, bool disableIfBlockedByTerrain = false)
    {
        bool blockedByTerrain = false;
        if (disableIfBlockedByTerrain)
            blockedByTerrain = Physics.Linecast(Camera.main.transform.position, _worldObjectPos, layerToBlockLockon);

        Vector3 ViewportPosition = Camera.main.WorldToViewportPoint(_worldObjectPos);

        if (blockedByTerrain || ViewportPosition.z < 0)
        {
            //Camera is not facing towards object
            _uiObject.gameObject.SetActive(false);
            return;
        }

        if (!_uiObject.gameObject.activeSelf)
            _uiObject.gameObject.SetActive(true);

        ViewportPosition.z = 0;
        Vector2 WorldObject_ScreenPosition = new Vector2(
        (ViewportPosition.x * c_RectTransform.sizeDelta.x) - (c_RectTransform.sizeDelta.x * 0.5f),
        (ViewportPosition.y * c_RectTransform.sizeDelta.y) - (c_RectTransform.sizeDelta.y * 0.5f));

        //now you can set the position of the ui element
        _uiObject.anchoredPosition = WorldObject_ScreenPosition;
    }

    public void OnEnemyKilled(Enemy enemy)
    {
        if (LockedonEnemy == enemy)
        {
            //Locked-on enemy was just killed, end lockon
            Helper_EndLockon();
        }
    }
}
