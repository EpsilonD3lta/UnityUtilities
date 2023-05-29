using System;
using UnityEngine.Events;

/// <summary>
/// Expose value in inspector and allow other scripts to subscribe for change notifications.
/// Instance should be readonly to only change <see cref="Value"/> and never the instance itself.
/// </summary>
[Serializable]
public class NotifyingField<T>
{
    private T _value;

    public UnityEvent changed;

    public T Value
    {
        get => _value;
        set
        {
            if (!_value.Equals(value))
            {
                _value = value;
                changed?.Invoke();
            }
        }
    }
    public NotifyingField() { }

    public NotifyingField(T _value)
    {
        this._value = _value;
    }

    public static implicit operator T(NotifyingField<T> w) => w.Value;

    public void SetWithoutNotify(T value)
    {
        _value = value;
    }
}