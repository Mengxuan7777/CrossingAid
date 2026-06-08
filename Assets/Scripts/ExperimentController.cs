using UnityEngine;

public class ExperimentController : MonoBehaviour
{
    [Header("Core References")]
    public EyeTrackingLogger logger;
    public Transform playerRoot;       // Assign XR Origin here
    public Transform trialStartPose;   // Empty GameObject at floor level — XR Origin teleports here

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

    private bool trialStarted = false;

    private void Start()
    {
        if (autoStartOnPlay)
            StartTrial();
    }

    private void Update()
    {
        if (!enableDebugKeys) return;

        if (Input.GetKeyDown(startTrialKey))   StartTrial();
        if (Input.GetKeyDown(endTrialKey))     EndTrial("ManualEnd");
        if (Input.GetKeyDown(resetPlayerKey))  ResetPlayerToStart();
        if (Input.GetKeyDown(dontWalkKey))     SetDontWalk();
        if (Input.GetKeyDown(walkKey))         SetWalk();
    }

    public void StartTrial()
    {
        if (logger == null)
        {
            Debug.LogError("[ExperimentController] StartTrial() aborted — logger is null. Assign EyeTrackingLogger in the Inspector.");
            return;
        }

        if (trialStarted && logger.TrialActive)
        {
            Debug.LogWarning($"[ExperimentController] StartTrial() blocked — trial already active. Call EndTrial() first.");
            return;
        }

        if (resetPlayerOnStartTrial)
            ResetPlayerToStart();

        logger.ConfigureTrial(participantId, sessionId, conditionName, distractionType, trialNumber);
        logger.StartTrial();
        trialStarted = true;
        Debug.Log($"[ExperimentController] Trial {trialNumber} started — condition='{conditionName}' distraction='{distractionType}'");
    }

    public void EndTrial(string reason)
    {
        if (logger == null || !logger.TrialActive)
        {
            Debug.LogWarning($"[ExperimentController] EndTrial('{reason}') skipped — logger={(logger == null ? "null" : "ok")}, TrialActive={logger?.TrialActive}");
            return;
        }

        logger.EndTrial(reason);
        trialStarted = false;
        Debug.Log($"[ExperimentController] Trial ended — reason='{reason}'");
    }

    public void ResetPlayerToStart()
    {
        if (playerRoot == null || trialStartPose == null) return;

        CharacterController cc = playerRoot.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        Rigidbody rb = playerRoot.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Place XR Origin at trialStartPose (floor level).
        // Subtract any XZ camera drift so the head centers on the start XZ position.
        Camera cam = playerRoot.GetComponentInChildren<Camera>();
        if (cam != null)
        {
            Vector3 drift = cam.transform.position - playerRoot.position;
            drift.y = 0f;
            playerRoot.position = trialStartPose.position - drift;
        }
        else
        {
            playerRoot.position = trialStartPose.position;
        }

        if (cc != null) cc.enabled = true;

        XRPlayerMover mover = playerRoot.GetComponent<XRPlayerMover>();
        if (mover != null) mover.ResetVelocity();
    }

    public void SetDontWalk()
    {
        logger?.SetSignalState("DONT_WALK");
    }

    public void SetWalk()
    {
        logger?.SetSignalState("WALK");
    }

    public void WriteCustomEvent(string eventName, string eventValue)
    {
        logger?.WriteCustomEvent(eventName, eventValue);
    }
}
