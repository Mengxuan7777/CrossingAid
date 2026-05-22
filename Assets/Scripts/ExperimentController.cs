using Unity.XR.CoreUtils;
using UnityEngine;

public class ExperimentController : MonoBehaviour
{
    [Header("Core References")]
    public EyeTrackingLogger logger;
    public Transform playerRoot;       // Assign XR Origin here
    public Transform trialStartPose;   // Empty GameObject in scene

    [Header("Trial Metadata")]
    public string participantId = "P001";
    public string sessionId = "S001";
    public string conditionName = "TestCondition";
    public string distractionType = "None";
    public int trialNumber = 1;

    [Header("Options")]
    public bool autoStartOnPlay = false;
    public bool resetPlayerOnStartTrial = true;

    [Header("Debug Keys")]
    public bool enableDebugKeys = true;
    public KeyCode startTrialKey = KeyCode.S;
    public KeyCode endTrialKey = KeyCode.E;
    public KeyCode resetPlayerKey = KeyCode.R;
    public KeyCode dontWalkKey = KeyCode.Alpha1;
    public KeyCode walkKey = KeyCode.Alpha2;
    public KeyCode gateClosedKey = KeyCode.Alpha3;
    public KeyCode gateOpenKey = KeyCode.Alpha4;

    private bool trialStarted = false;

    private void Start()
    {
        if (autoStartOnPlay)
        {
            StartTrial();
        }
    }

    private void Update()
    {
        if (!enableDebugKeys)
        {
            return;
        }

        if (Input.GetKeyDown(startTrialKey))
        {
            StartTrial();
        }

        if (Input.GetKeyDown(endTrialKey))
        {
            EndTrial("ManualEnd");
        }

        if (Input.GetKeyDown(resetPlayerKey))
        {
            ResetPlayerToStart();
        }

        if (Input.GetKeyDown(dontWalkKey))
        {
            SetDontWalk();
        }

        if (Input.GetKeyDown(walkKey))
        {
            SetWalk();
        }

        if (Input.GetKeyDown(gateClosedKey))
        {
            SetGateClosed();
        }

        if (Input.GetKeyDown(gateOpenKey))
        {
            SetGateOpen();
        }
    }

    public void StartTrial()
    {
        if (logger == null)
        {
            Debug.LogWarning("ExperimentController: Logger is not assigned.");
            return;
        }

        if (trialStarted && logger.TrialActive)
        {
            Debug.LogWarning("ExperimentController: Trial already running.");
            return;
        }

        if (resetPlayerOnStartTrial)
        {
            ResetPlayerToStart();
        }

        logger.ConfigureTrial(
            participantId,
            sessionId,
            conditionName,
            distractionType,
            trialNumber
        );

        logger.StartTrial();
        trialStarted = true;

        Debug.Log("Trial started.");
    }

    public void EndTrial(string reason)
    {
        if (logger == null)
        {
            Debug.LogWarning("ExperimentController: Logger is not assigned.");
            return;
        }

        if (!logger.TrialActive)
        {
            Debug.LogWarning("ExperimentController: No active trial to end.");
            return;
        }

        logger.EndTrial(reason);
        trialStarted = false;

        Debug.Log("Trial ended. Reason: " + reason);
    }

    public void ResetPlayerToStart()
    {
        if (playerRoot == null || trialStartPose == null)
        {
            Debug.LogWarning("ExperimentController: playerRoot or trialStartPose is missing.");
            return;
        }

        XROrigin xrOrigin = playerRoot.GetComponent<XROrigin>();
        if (xrOrigin == null)
        {
            Debug.LogWarning("ExperimentController: No XROrigin found on playerRoot.");
            return;
        }

        CharacterController cc = playerRoot.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        Rigidbody rb = playerRoot.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        xrOrigin.MoveCameraToWorldLocation(trialStartPose.position);
        xrOrigin.MatchOriginUpCameraForward(trialStartPose.forward, Vector3.up);

        if (cc != null) cc.enabled = true;

        Debug.Log("Player reset to start pose.");
        Debug.Log("XROrigin position: " + playerRoot.position);
        Debug.Log("Camera position: " + xrOrigin.Camera.transform.position);
    }

    public void SetDontWalk()
    {
        if (logger != null)
        {
            logger.SetSignalState("DONT_WALK");
        }
    }

    public void SetWalk()
    {
        if (logger != null)
        {
            logger.SetSignalState("WALK");
        }
    }

    public void SetGateClosed()
    {
        if (logger != null)
        {
            logger.SetGateState("CLOSED");
        }
    }

    public void SetGateOpen()
    {
        if (logger != null)
        {
            logger.SetGateState("OPEN");
        }
    }

    public void WriteCustomEvent(string eventName, string eventValue)
    {
        if (logger != null)
        {
            logger.WriteCustomEvent(eventName, eventValue);
        }
    }
}