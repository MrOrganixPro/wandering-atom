using UnityEngine;

/// <summary>Runs a hazard's Insight cue after GameManager has moved the camera this frame
/// (without changing when the hazard's own physics update runs).</summary>
[DefaultExecutionOrder(100)]
public class HazardCue : MonoBehaviour
{
    public Hazard hazard;
    void LateUpdate() { if (hazard != null) hazard.UpdateCue(); }
}
