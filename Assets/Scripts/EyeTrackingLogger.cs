using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

public class EyeTrackingLogger : MonoBehaviour
{
    [Header("Scene References")]
    public Camera xrCamera;

    [Tooltip("Only AOI colliders should be on this layer.")]
    public LayerMask aoiLayerMask;

    [Header("OpenXR Eye Gaze Input Actions")]
    [Tooltip("Bind to <EyeGaze>/pose/position")]
    public InputActionProperty gazePositionAction;

    [Tooltip("Bind to <EyeGaze>/pose/rotation")]
    public InputActionProperty gazeRotationAction;

    [Tooltip("Bind to <EyeGaze>/isTracked")]
    public InputActionProperty gazeTrackedAction;

    [Header("Sampling")]
    [Range(10, 240)]
    public int sampleRateHz = 90;

    public float maxRayDistance = 100f;
    public bool recordOnlyDuringTrial = true;

    [Header("Metadata")]
    public string participantId = "P001";
    public string sessionId = "S001";
    public string conditionName = "Baseline";
    public string distractionType = "Text";
    public int trialNumber = 1;

    private string currentSignalState = "UNKNOWN";

    private bool trialActive = false;
    private bool crossingInitiationLogged = false;

    private double trialStartTime = -1.0;
    private double nextSampleTime = 0.0;
    private double sampleInterval = 1.0 / 90.0;
    private double nextFlushTime = 0.0;
    private const double FlushInterval = 2.0;

    private string folderPath;
    private string gazeFilePath;
    private string eventFilePath;

    private StreamWriter gazeWriter;
    private StreamWriter eventWriter;

    public bool TrialActive
    {
        get { return trialActive; }
    }

    public bool CrossingInitiationLogged
    {
        get { return crossingInitiationLogged; }
    }

    private void Awake()
    {
        if (xrCamera == null && Camera.main != null)
        {
            xrCamera = Camera.main;
        }

        sampleInterval = 1.0 / Mathf.Max(1, sampleRateHz);

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        folderPath = Path.Combine(Application.persistentDataPath, "EyeTrackingLogs", participantId + "_" + timestamp);

        Directory.CreateDirectory(folderPath);

        gazeFilePath = Path.Combine(folderPath, "gaze_samples.csv");
        eventFilePath = Path.Combine(folderPath, "events.csv");

        OpenFiles();
    }

    private void OnEnable()
    {
        EnableAction(gazePositionAction);
        EnableAction(gazeRotationAction);
        EnableAction(gazeTrackedAction);
    }

    private void OnDisable()
    {
        DisableAction(gazePositionAction);
        DisableAction(gazeRotationAction);
        DisableAction(gazeTrackedAction);

        CloseFiles();
    }

    private void OnApplicationQuit()
    {
        CloseFiles();
    }

    private void Update()
    {
        if (recordOnlyDuringTrial && !trialActive)
        {
            return;
        }

        double now = Time.unscaledTimeAsDouble;

        while (now >= nextSampleTime)
        {
            SampleGaze(now);
            nextSampleTime += sampleInterval;
        }

        if (now >= nextFlushTime)
        {
            gazeWriter?.Flush();
            nextFlushTime = now + FlushInterval;
        }
    }

    private void OpenFiles()
    {
        gazeWriter = new StreamWriter(gazeFilePath, false);
        eventWriter = new StreamWriter(eventFilePath, false);

        gazeWriter.WriteLine(
            "timestamp_s,trial_time_s,participant_id,session_id,condition_name,distraction_type,trial_number," +
            "trial_active,gaze_valid,signal_state," +
            "gaze_origin_x,gaze_origin_y,gaze_origin_z," +
            "gaze_dir_x,gaze_dir_y,gaze_dir_z," +
            "hit_aoi_id,hit_aoi_group,hit_point_x,hit_point_y,hit_point_z,hit_distance," +
            "head_pos_x,head_pos_y,head_pos_z,head_rot_x,head_rot_y,head_rot_z,head_rot_w"
        );

        eventWriter.WriteLine(
            "timestamp_s,trial_time_s,participant_id,session_id,condition_name,distraction_type,trial_number," +
            "event_name,event_value,signal_state"
        );

        gazeWriter.Flush();
        eventWriter.Flush();
    }

    private void CloseFiles()
    {
        if (gazeWriter != null)
        {
            gazeWriter.Flush();
            gazeWriter.Close();
            gazeWriter = null;
        }

        if (eventWriter != null)
        {
            eventWriter.Flush();
            eventWriter.Close();
            eventWriter = null;
        }
    }

    private void EnableAction(InputActionProperty property)
    {
        if (property.action != null)
        {
            property.action.Enable();
        }
    }

    private void DisableAction(InputActionProperty property)
    {
        if (property.action != null)
        {
            property.action.Disable();
        }
    }

    private void SampleGaze(double now)
    {
        Vector3 gazeOrigin;
        Quaternion gazeRotation;
        bool gazeValid = TryReadGazePose(out gazeOrigin, out gazeRotation) && IsGazeTracked();

        Vector3 gazeDir = Vector3.zero;
        string hitAoiId = "NONE";
        string hitAoiGroup = "NONE";
        Vector3 hitPoint = Vector3.zero;
        float hitDistance = -1f;

        if (gazeValid)
        {
            gazeDir = gazeRotation * Vector3.forward;

            // Eye gaze pose is in head-local space; transform to world space for raycasting.
            if (xrCamera != null)
            {
                gazeOrigin = xrCamera.transform.TransformPoint(gazeOrigin);
                gazeDir = xrCamera.transform.TransformDirection(gazeDir);
            }

            Ray ray = new Ray(gazeOrigin, gazeDir);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, maxRayDistance, aoiLayerMask, QueryTriggerInteraction.Collide))
            {
                AOIMarker marker = hit.collider.GetComponent<AOIMarker>();

                if (marker == null)
                {
                    marker = hit.collider.GetComponentInParent<AOIMarker>();
                }

                hitAoiId = marker != null ? marker.aoiId : hit.collider.gameObject.name;
                hitAoiGroup = marker != null ? marker.aoiGroup : "UNLABELED";
                hitPoint = hit.point;
                hitDistance = hit.distance;
            }
        }

        Vector3 headPos = Vector3.zero;
        Quaternion headRot = Quaternion.identity;

        if (xrCamera != null)
        {
            headPos = xrCamera.transform.position;
            headRot = xrCamera.transform.rotation;
        }

        double trialTime = trialStartTime >= 0.0 ? now - trialStartTime : -1.0;

        string row =
            F(now) + "," +
            F(trialTime) + "," +
            Escape(participantId) + "," +
            Escape(sessionId) + "," +
            Escape(conditionName) + "," +
            Escape(distractionType) + "," +
            trialNumber.ToString(CultureInfo.InvariantCulture) + "," +
            (trialActive ? "1" : "0") + "," +
            (gazeValid ? "1" : "0") + "," +
            Escape(currentSignalState) + "," +
            F(gazeOrigin.x) + "," + F(gazeOrigin.y) + "," + F(gazeOrigin.z) + "," +
            F(gazeDir.x) + "," + F(gazeDir.y) + "," + F(gazeDir.z) + "," +
            Escape(hitAoiId) + "," + Escape(hitAoiGroup) + "," +
            F(hitPoint.x) + "," + F(hitPoint.y) + "," + F(hitPoint.z) + "," + F(hitDistance) + "," +
            F(headPos.x) + "," + F(headPos.y) + "," + F(headPos.z) + "," +
            F(headRot.x) + "," + F(headRot.y) + "," + F(headRot.z) + "," + F(headRot.w);

        gazeWriter.WriteLine(row);
    }

    private bool TryReadGazePose(out Vector3 gazeOrigin, out Quaternion gazeRotation)
    {
        gazeOrigin = Vector3.zero;
        gazeRotation = Quaternion.identity;

        if (gazePositionAction.action == null || gazeRotationAction.action == null)
            return false;

        try
        {
            gazeOrigin = gazePositionAction.action.ReadValue<Vector3>();
            gazeRotation = gazeRotationAction.action.ReadValue<Quaternion>();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private bool IsGazeTracked()
    {
        if (gazeTrackedAction.action == null)
        {
            return true;
        }

        try
        {
            float trackedValue = gazeTrackedAction.action.ReadValue<float>();
            return trackedValue > 0.5f;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void ConfigureTrial(string newParticipantId, string newSessionId, string newConditionName, string newDistractionType, int newTrialNumber)
    {
        participantId = newParticipantId;
        sessionId = newSessionId;
        conditionName = newConditionName;
        distractionType = newDistractionType;
        trialNumber = newTrialNumber;
    }

    public void StartTrial()
    {
        trialActive = true;
        crossingInitiationLogged = false;
        trialStartTime = Time.unscaledTimeAsDouble;
        nextSampleTime = trialStartTime;
        WriteEvent("TrialStart", "");
    }

    public void EndTrial(string eventValue = "")
    {
        WriteEvent("TrialEnd", eventValue);
        trialActive = false;
    }

    public void SetSignalState(string state)
    {
        currentSignalState = state;
        WriteEvent("SignalState", state);
    }

    public void MarkCrossingInitiation(string crossingLineId)
    {
        if (!trialActive || crossingInitiationLogged)
        {
            return;
        }

        crossingInitiationLogged = true;
        WriteEvent("CrossingInitiation", crossingLineId);
    }

    public void WriteCustomEvent(string eventName, string eventValue)
    {
        WriteEvent(eventName, eventValue);
    }

    private void WriteEvent(string eventName, string eventValue)
    {
        if (eventWriter == null)
        {
            return;
        }

        double now = Time.unscaledTimeAsDouble;
        double trialTime = trialStartTime >= 0.0 ? now - trialStartTime : -1.0;

        string row =
            F(now) + "," +
            F(trialTime) + "," +
            Escape(participantId) + "," +
            Escape(sessionId) + "," +
            Escape(conditionName) + "," +
            Escape(distractionType) + "," +
            trialNumber.ToString(CultureInfo.InvariantCulture) + "," +
            Escape(eventName) + "," +
            Escape(eventValue) + "," +
            Escape(currentSignalState);

        eventWriter.WriteLine(row);
        eventWriter.Flush();
    }

    private string F(double value)
    {
        return value.ToString("F6", CultureInfo.InvariantCulture);
    }

    private string F(float value)
    {
        return value.ToString("F6", CultureInfo.InvariantCulture);
    }

    private string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}