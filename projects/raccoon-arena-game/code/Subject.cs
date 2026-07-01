using System.Collections.Generic;
using UnityEngine;

public abstract class Subject<TEvent> : MonoBehaviour
{
    // Avoid duplicated observers
    private readonly HashSet<IObserver<TEvent>> _observers = new HashSet<IObserver<TEvent>>();

    public void AddObserver(IObserver<TEvent> observer)
    {
        if (observer != null) _observers.Add(observer);
    }

    public void RemoveObserver(IObserver<TEvent> observer)
    {
        if (observer != null) _observers.Remove(observer);
    }

    public void Clear()
    {
        _observers.Clear();
    }

    protected void Notify(TEvent evt, object data=null)
    {
        // Create a snapshot to be safe if observers change during notification.
        var snapshot = _observers.Count > 0 ? new IObserver<TEvent>[_observers.Count] : null;
        if (snapshot == null) return;

        _observers.CopyTo(snapshot);
        for (int i = 0; i < snapshot.Length; i++)
        {
            var obs = snapshot[i];
            if (obs != null) obs.OnNotify(evt, data);
        }
    }
}