using UnityEngine;

// Temporary debug aid: wire this object's Signal Receiver to call Ping() for whichever
// Signal Asset you're trying to confirm actually fires. Delete once confirmed.
public class SignalPing : MonoBehaviour
{
    public void Ping()
    {
        Debug.Log($"[SignalPing] signal received on {name} at t={Time.time:F2}", this);
    }
}
