using UnityEngine;

public class XRStartAligner : MonoBehaviour
{
    public Transform xrOrigin;
    public Transform xrCamera;
    public Transform startPoint;

    void Start()
    {
        Vector3 cameraOffset = xrCamera.position - xrOrigin.position;
        cameraOffset.y = 0f;

        xrOrigin.position = startPoint.position - cameraOffset;
        xrOrigin.rotation = startPoint.rotation;
    }
}