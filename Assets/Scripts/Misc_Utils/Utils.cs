using System;
using UnityEngine;

public static class Utils
{
    /// <summary>
    /// Remaps a value in one range to another, keeping the same percentage between the given min and max
    /// </summary>
    /// <param name="_value"></param>
    /// <param name="_currentMin"></param>
    /// <param name="_currentMax"></param>
    /// <param name="_newMin"></param>
    /// <param name="_newMax"></param>
    /// <returns></returns>
    public static float Remap(float _value, float _currentMin, float _currentMax, float _newMin, float _newMax)
    {
        float currRange = _currentMax - _currentMin;
        float currAmountFromMin = _value - _currentMin;
        float percentFromMin = currAmountFromMin / currRange;

        float targRange = _newMax - _newMin;
        return _newMin + percentFromMin * targRange;
    }

    public static float MoveTowardsValue(float _currValue, float _targetValue, float _amountToMoveThisIteration)
    {
        float dist = MathF.Abs(_targetValue - _currValue);
        if (dist <= _amountToMoveThisIteration)
            return _targetValue;

        if (_currValue < _targetValue)
            return _currValue + _amountToMoveThisIteration;

        //_currvalue is > targetValue
        return _currValue - _amountToMoveThisIteration;
    }

    public static float LerpDegrees(float start, float end, float amount)
    {
        float difference = Math.Abs(end - start);
        if (difference > 180)
        {
            // We need to add on to one of the values.
            if (end > start)
            {
                // We'll add it on to start...
                start += 360;
            }
            else
            {
                // Add it on to end.
                end += 360;
            }
        }

        // Interpolate it.
        float value = (start + ((end - start) * amount));

        // Wrap it..
        float rangeZero = 360;

        if (value >= 0 && value <= 360)
            return value;

        return (value % rangeZero);
    }

    public static float MoveTowardsRotation(float start, float end, float degrees)
    {
        while (start < 0)
            start += 360;
        start %= 360;

        while (end < 0)
            end += 360;
        end %= 360;

        float difference = Math.Abs(end - start);
        if (difference > 180)
        {
            // We need to add on to one of the values.
            if (end > start)
            {
                // We'll add it on to start...
                start += 360;
            }
            else
            {
                // Add it on to end.
                end += 360;
            }
        }

        // Interpolate it.
        float value = MoveTowardsValue(start, end, degrees);

        // Wrap it..
        float rangeZero = 360;

        if (value >= 0 && value <= 360)
            return value;

        return (value % rangeZero);
    }

    public static Vector3 MoveTowardsVector(Vector3 start, Vector3 end, float distanceToMove)
    {
        float dist = (end - start).magnitude;

        if (dist <= distanceToMove)
            return end;

        return Vector3.Lerp(start, end, distanceToMove / dist);
    }
}
