using Unity.XR.CoreUtils;
using UnityEngine;

public class XRStartAligner : MonoBehaviour
{
    public XROrigin xrOrigin;
    public Transform startPoint;

    void Start()
    {
        if (xrOrigin == null || startPoint == null)
        {
            Debug.LogWarning("[XRStartAligner] xrOrigin or startPoint is not assigned.", this);
            return;
        }

        xrOrigin.MoveCameraToWorldLocation(startPoint.position);
        xrOrigin.MatchOriginUpCameraForward(startPoint.forward, Vector3.up);
    }
}