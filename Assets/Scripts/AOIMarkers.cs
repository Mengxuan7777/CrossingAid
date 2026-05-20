using UnityEngine;

[DisallowMultipleComponent]
public class AOIMarker : MonoBehaviour
{
    [Tooltip("Unique AOI label written to the CSV.")]
    public string aoiId = "Gate";

    [Tooltip("Optional analysis group, e.g. SafetyCue, Distraction, Road.")]
    public string aoiGroup = "SafetyCue";
}