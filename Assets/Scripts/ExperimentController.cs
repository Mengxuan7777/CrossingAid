using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ExperimentController : MonoBehaviour
{
    public enum DistractionType
    {
        None,
        Conversation,
        TextReading
    }

    [System.Serializable]
    public class TrialCondition
    {
        public string conditionName = "Gate_None";
        public bool gatePresent = true;
        public DistractionType distractionType = DistractionType.None;

        [Tooltip("Seconds from trial start to WALK onset.")]
        public float walkOnsetSec = 3.0f;

        [Tooltip("Maximum trial duration from StartTrial().")]
        public float maxTrialDurationSec = 10.0f;

        [Tooltip("Optional time before StartTrial() after reset.")]
        public float preStartDelaySec = 1.0f;
    }

    [Header("Core References")]
    public EyeTrackingLogger logger;
    public Transform playerRoot;
    public Transform trialStartPose;

    [Header("Session Metadata")]
    public string participantId = "P001";
    public string sessionId = "S001";

    [Header("Trials")]
    public List<TrialCondition> trials = new List<TrialCondition>();
    public bool autoAdvanceToNextTrial = false;
    public float interTrialIntervalSec = 2.0f;

    [Header("Debug Controls")]
    public bool debugKeyboardControls = true;

    [Header("Signal Events")]
    public UnityEvent onDontWalk;
    public UnityEvent onWalk;

    [Header("Gate Events")]
    public UnityEvent onGateClosed;
    public UnityEvent onGateOpen;
    public UnityEvent onGateHidden;

    [Header("Distraction Events")]
    public UnityEvent onNoDistractionStart;
    public UnityEvent onConversationStart;
    public UnityEvent onTextReadingStart;
    public UnityEvent onDistractionStop;

    [Header("Trial Events")]
    public UnityEvent onTrialPrepared;
    public UnityEvent onTrialStarted;
    public UnityEvent onTrialEnded;
    public UnityEvent onSessionStarted;
    public UnityEvent onSessionEnded;

    private int currentTrialIndex = -1;
    private Coroutine runningTrialRoutine;
    private bool sessionActive = false;

    public int CurrentTrialIndex
    {
        get { return currentTrialIndex; }
    }

    public bool TrialRunning
    {
        get { return runningTrialRoutine != null; }
    }

    private void Update()
    {
        if (!debugKeyboardControls)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            StartSession();
        }

        if (Input.GetKeyDown(KeyCode.N))
        {
            StartNextTrial();
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            ForceEndCurrentTrial("ManualEnd");
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetPlayerToStart();
        }
    }

    public void StartSession()
    {
        if (sessionActive)
        {
            Debug.LogWarning("Session already active.");
            return;
        }

        sessionActive = true;
        currentTrialIndex = -1;

        if (logger != null)
        {
            logger.WriteCustomEvent("SessionStart", sessionId);
        }

        onSessionStarted.Invoke();

        Debug.Log("Session started: " + sessionId);
    }

    public void EndSession()
    {
        if (!sessionActive)
        {
            return;
        }

        if (TrialRunning)
        {
            ForceEndCurrentTrial("SessionEnded");
        }

        sessionActive = false;

        if (logger != null)
        {
            logger.WriteCustomEvent("SessionEnd", sessionId);
        }

        onSessionEnded.Invoke();

        Debug.Log("Session ended: " + sessionId);
    }

    public void StartNextTrial()
    {
        if (!sessionActive)
        {
            Debug.LogWarning("StartSession() first.");
            return;
        }

        if (TrialRunning)
        {
            Debug.LogWarning("A trial is already running.");
            return;
        }

        currentTrialIndex += 1;

        if (currentTrialIndex >= trials.Count)
        {
            Debug.Log("All trials completed.");
            EndSession();
            return;
        }

        runningTrialRoutine = StartCoroutine(RunTrial(trials[currentTrialIndex], currentTrialIndex + 1));
    }

    public void ForceEndCurrentTrial(string reason)
    {
        if (!TrialRunning)
        {
            return;
        }

        StopAllCoroutines();

        StopDistraction();

        if (logger != null && logger.TrialActive)
        {
            logger.EndTrial(reason);
        }

        onTrialEnded.Invoke();
        runningTrialRoutine = null;

        Debug.Log("Trial force-ended: " + reason);
    }

    private IEnumerator RunTrial(TrialCondition trial, int trialNumber)
    {
        ResetPlayerToStart();

        if (logger != null)
        {
            logger.ConfigureTrial(
                participantId,
                sessionId,
                trial.conditionName,
                trial.distractionType.ToString(),
                trialNumber
            );
        }

        PrepareInitialTrialState(trial);

        if (logger != null)
        {
            logger.WriteCustomEvent("TrialPrepared", trial.conditionName);
        }

        onTrialPrepared.Invoke();

        if (trial.preStartDelaySec > 0f)
        {
            yield return new WaitForSeconds(trial.preStartDelaySec);
        }

        if (logger != null)
        {
            logger.StartTrial();
        }

        onTrialStarted.Invoke();

        StartDistraction(trial.distractionType);

        float elapsed = 0f;
        bool walkTriggered = false;

        while (elapsed < trial.maxTrialDurationSec)
        {
            elapsed += Time.deltaTime;

            if (!walkTriggered && elapsed >= trial.walkOnsetSec)
            {
                TriggerWalkState(trial);
                walkTriggered = true;
            }

            if (logger != null && logger.CrossingInitiationLogged)
            {
                break;
            }

            yield return null;
        }

        StopDistraction();

        if (logger != null && logger.TrialActive)
        {
            if (logger.CrossingInitiationLogged)
            {
                logger.EndTrial("CrossingInitiated");
            }
            else
            {
                logger.EndTrial("Timeout");
            }
        }

        onTrialEnded.Invoke();

        yield return new WaitForSeconds(interTrialIntervalSec);

        runningTrialRoutine = null;

        if (autoAdvanceToNextTrial)
        {
            StartNextTrial();
        }
    }

    private void PrepareInitialTrialState(TrialCondition trial)
    {
        SetDontWalkState();

        if (trial.gatePresent)
        {
            SetGateClosedState();
        }
        else
        {
            SetGateHiddenState();
        }
    }

    private void TriggerWalkState(TrialCondition trial)
    {
        SetWalkState();

        if (trial.gatePresent)
        {
            SetGateOpenState();
        }
    }

    public void SetDontWalkState()
    {
        if (logger != null)
        {
            logger.SetSignalState("DONT_WALK");
        }

        onDontWalk.Invoke();
    }

    public void SetWalkState()
    {
        if (logger != null)
        {
            logger.SetSignalState("WALK");
        }

        onWalk.Invoke();
    }

    public void SetGateClosedState()
    {
        if (logger != null)
        {
            logger.SetGateState("CLOSED");
        }

        onGateClosed.Invoke();
    }

    public void SetGateOpenState()
    {
        if (logger != null)
        {
            logger.SetGateState("OPEN");
        }

        onGateOpen.Invoke();
    }

    public void SetGateHiddenState()
    {
        if (logger != null)
        {
            logger.SetGateState("ABSENT");
        }

        onGateHidden.Invoke();
    }

    private void StartDistraction(DistractionType distractionType)
    {
        if (logger != null)
        {
            logger.WriteCustomEvent("DistractionStart", distractionType.ToString());
        }

        if (distractionType == DistractionType.None)
        {
            onNoDistractionStart.Invoke();
        }
        else if (distractionType == DistractionType.Conversation)
        {
            onConversationStart.Invoke();
        }
        else if (distractionType == DistractionType.TextReading)
        {
            onTextReadingStart.Invoke();
        }
    }

    private void StopDistraction()
    {
        if (logger != null)
        {
            logger.WriteCustomEvent("DistractionStop", "");
        }

        onDistractionStop.Invoke();
    }

    public void ResetPlayerToStart()
    {
        if (playerRoot == null || trialStartPose == null)
        {
            return;
        }

        playerRoot.position = trialStartPose.position;
        playerRoot.rotation = trialStartPose.rotation;

        Rigidbody rb = playerRoot.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        CharacterController cc = playerRoot.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
            playerRoot.position = trialStartPose.position;
            playerRoot.rotation = trialStartPose.rotation;
            cc.enabled = true;
        }
    }
}