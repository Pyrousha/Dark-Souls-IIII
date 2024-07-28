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
    [SerializeField] private float lockonDuration;
    [SerializeField] private Transform cameraPivot;
    [Space(10)]
    [SerializeField] private float sqrMagnitudeForLockon;
    [SerializeField] private LayerMask layerToBlockLockon;

    private Enemy lockedonEnemy;

    public enum LockonStateEnum
    {
        Unlocked,
        LockingOn,
        Locked
    }

    public LockonStateEnum LockonState { get; private set; } = LockonStateEnum.Unlocked;
    private Vector3 lockedOnCameraAngle;

    private Routine lockonRoutine;

    private void Start()
    {
        c_RectTransform = GetComponent<RectTransform>();

        lockedOnCameraAngle = cameraPivot.forward;
        Debug.Log(lockedOnCameraAngle);

        lockonRect.gameObject.SetActive(LockonState != LockonStateEnum.Unlocked);
    }

    IEnumerator StartLockon()
    {
        Vector3 startingForward = cameraPivot.forward;
        float startingAlpha = lockonImage.color.a;

        yield return Tween.Float(0, 1, (t) =>
        {
            //Set color
            Color newColor = lockonImage.color;
            newColor.a = Mathf.Lerp(startingAlpha, 1, t);
            lockonImage.color = newColor;

            cameraPivot.forward = Vector3.Lerp(startingForward, GetCamForward(), t);
        }, lockonDuration).Ease(Curve.CubeInOut);

        LockonState = LockonStateEnum.Locked;
    }

    /// <summary>
    /// Gets the position of the current enemy's locked-on pivor
    /// </summary>
    /// <param name="pos"></param>
    /// <returns>If there is a current enemy locked onto</returns>
    private bool GetEnemyLockonPivotPos(out Vector3 pos)
    {
        if (lockedonEnemy == null)
        {
            pos = Vector3.zero;
            return false;
        }

        pos = lockedonEnemy.LockonPivot.position;
        return true;
    }

    private Vector3 GetCamForward()
    {
        if (lockedonEnemy == null)
            return cameraPivot.forward;

        //Change camera angle
        Vector3 newCamForward = (lockedonEnemy.HPBarPivot.position - cameraPivot.position);
        newCamForward.y = 0;
        newCamForward.Normalize();

        newCamForward *= (1 - lockedOnCameraAngle.y);
        newCamForward.y = lockedOnCameraAngle.y;

        return newCamForward;
    }

    IEnumerator EndLockon()
    {
        float startingAlpha = lockonImage.color.a;

        if (startingAlpha > 0)
        {
            yield return Tween.Float(startingAlpha, 0, (t) =>
            {
                //Set color
                Color newColor = lockonImage.color;
                newColor.a = t;
                lockonImage.color = newColor;
            }, lockonDuration * (startingAlpha)).Ease(Curve.CubeInOut);
        }
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

                        lockedonEnemy = closestEnemy;
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
        if (LockonState == LockonStateEnum.LockingOn)
        {
            if (GetEnemyLockonPivotPos(out Vector3 pos))
                SetUIPositionToWorldPosition(lockonRect, pos);
            else
                Helper_EndLockon();
        }
        else if (LockonState == LockonStateEnum.Locked)
        {
            cameraPivot.forward = GetCamForward();

            if (GetEnemyLockonPivotPos(out Vector3 pos))
                SetUIPositionToWorldPosition(lockonRect, pos);
            else
                Helper_EndLockon();
        }

        //Set position of healthbars
        foreach (Enemy enemy in EnemyManager.Instance.AliveEnemies)
        {
            //Don't show healthbars for enemies that are full health and not locked-on
            if (enemy.IsHpMax && (lockedonEnemy == null || enemy != lockedonEnemy))
            {
                if (enemy.Healthbar.gameObject.activeSelf)
                    enemy.Healthbar.gameObject.SetActive(false);

                continue;
            }

            SetUIPositionToWorldPosition(enemy.Healthbar.C_RectTransform, enemy.HPBarPivot.position, true);
        }

        void Helper_EndLockon()
        {
            lockonRoutine.Stop();
            lockonRoutine = Routine.Start(this, EndLockon());
            LockonState = LockonStateEnum.Unlocked;
            lockedonEnemy = null;
        }
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
}
