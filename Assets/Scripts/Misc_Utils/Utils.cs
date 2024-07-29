using System;

public static class Utils
{
    /// <summary>
    /// Remaps a value in one range to another, keeping the same percentage between the given min and max
    /// </summary>
    /// <param name="_value"></param>
    /// <param name="_currentMin"></param>
    /// <param name="_currentMax"></param>
    /// <param name="_targetMin"></param>
    /// <param name="_targetMax"></param>
    /// <returns></returns>
    public static float Remap(float _value, float _currentMin, float _currentMax, float _targetMin, float _targetMax)
    {
        float currRange = _currentMax - _currentMin;
        float currAmountFromMin = _value - _currentMin;
        float percentFromMin = currAmountFromMin / currRange;

        float targRange = _targetMax - _targetMin;
        return _targetMin + percentFromMin * targRange;
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
}
