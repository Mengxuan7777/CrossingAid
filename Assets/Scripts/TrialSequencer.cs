using System.IO;
using TMPro;
using UnityEngine;

public class TrialSequencer : MonoBehaviour
{
    [Header("Core References")]
    public ExperimentController experimentController;
    public IntersectionSignalController signals;

    [Header("Config")]
    [Tooltip("File name inside Assets/StreamingAssets/")]
    public string configFileName = "trial_config.json";

    [Header("Assistance Systems")]
    public PeripheralCue[]          peripheralCues;
    public CurbIndicator[]          curbIndicators;
    public CrossingTimerDisplay[]   timerDisplays;

    [Header("Distraction Objects")]
    [Tooltip("GameObject shown during Conversation trials (e.g. a phone mesh).")]
    public GameObject    phoneObject;
    [Tooltip("Optional audio clip played during Conversation trials.")]
    public AudioSource   conversationAudio;
    [Tooltip("GameObject shown during TextReading trials (e.g. a world-space canvas).")]
    public GameObject    textPanel;
    [Tooltip("TextMeshPro component on the text panel.")]
    public TextMeshProUGUI panelText;

    [Header("Crossing Direction")]
    [Tooltip("Which road the player crosses. Determines which signal state counts as Safe.")]
    public PlayerCrossesRoad crossingRoad = PlayerCrossesRoad.NorthSouth;

    [Header("Debug Keys")]
    public bool    enableDebugKeys = true;
    public KeyCode loadConfigKey   = KeyCode.L;
    public KeyCode nextTrialKey    = KeyCode.N;
    public KeyCode prevTrialKey    = KeyCode.P;

    private ExperimentConfig _config;
    private int _currentIndex = -1;

    public int CurrentTrialIndex => _currentIndex;
    public int TotalTrials       => _config?.trials?.Count ?? 0;

    private void Start()
    {
        LoadConfig();
    }

    private void Update()
    {
        if (!enableDebugKeys) return;
        if (Input.GetKeyDown(loadConfigKey)) LoadConfig();
        if (Input.GetKeyDown(nextTrialKey))  NextTrial();
        if (Input.GetKeyDown(prevTrialKey))  PreviousTrial();
    }

    public void LoadConfig()
    {
        string path = Path.Combine(Application.streamingAssetsPath, configFileName);
        if (!File.Exists(path))
        {
            Debug.LogError($"[TrialSequencer] Config file not found: {path}");
            return;
        }
        _config = JsonUtility.FromJson<ExperimentConfig>(File.ReadAllText(path));
        _currentIndex = -1;
        Debug.Log($"[TrialSequencer] Loaded {_config.trials.Count} trials for participant '{_config.participantId}'");
    }

    public void NextTrial()
    {
        if (_config == null) { Debug.LogWarning("[TrialSequencer] No config loaded. Press L first."); return; }
        int next = _currentIndex + 1;
        if (next >= _config.trials.Count) { Debug.Log("[TrialSequencer] All trials complete."); return; }
        StartTrial(next);
    }

    public void PreviousTrial()
    {
        if (_config == null || _currentIndex <= 0) return;
        StartTrial(_currentIndex - 1);
    }

    public void StartTrial(int index)
    {
        if (_config == null || index < 0 || index >= _config.trials.Count) return;

        // End currently active trial if any
        if (experimentController != null &&
            experimentController.logger != null &&
            experimentController.logger.TrialActive)
            experimentController.EndTrial("TrialSequencerAdvance");

        _currentIndex = index;
        TrialDefinition t = _config.trials[index];

        ApplyAssistance(t.GetAssistanceLevel());
        ApplyDistraction(t.GetDistractionType(), t.distractionText);
        ApplySignal(t.GetCrossingScenario());

        if (experimentController != null)
        {
            experimentController.participantId  = _config.participantId;
            experimentController.sessionId      = _config.sessionId;
            experimentController.conditionName  = t.conditionName;
            experimentController.distractionType = t.distraction;
            experimentController.trialNumber    = t.trialNumber;
            experimentController.StartTrial();
        }

        Debug.Log($"[TrialSequencer] Trial {t.trialNumber}/{_config.trials.Count}: {t.conditionName} | assist={t.assistanceLevel} distract={t.distraction} signal={t.crossingScenario}");
    }

    // ── Condition appliers ────────────────────────────────────────────────

    private void ApplyAssistance(AssistanceLevel level)
    {
        bool on = level == AssistanceLevel.FullyAssisted;
        if (peripheralCues  != null) foreach (var c in peripheralCues)  if (c != null) c.enabled = on;
        if (curbIndicators  != null) foreach (var c in curbIndicators)  if (c != null) c.enabled = on;
        if (timerDisplays   != null) foreach (var c in timerDisplays)   if (c != null) c.enabled = on;
    }

    private void ApplyDistraction(DistractionType type, string text)
    {
        bool isCall = type == DistractionType.Conversation;
        bool isText = type == DistractionType.TextReading;

        if (phoneObject != null) phoneObject.SetActive(isCall);

        if (conversationAudio != null)
        {
            if (isCall) conversationAudio.Play();
            else        conversationAudio.Stop();
        }

        if (textPanel != null) textPanel.SetActive(isText);

        if (panelText != null && isText)
            panelText.text = string.IsNullOrEmpty(text) ? "Read this text carefully while crossing." : text;
    }

    private void ApplySignal(CrossingScenario scenario)
    {
        if (signals == null) return;

        bool safe = scenario == CrossingScenario.Safe;
        // ewGoesFirst = true  → EW is green, NS is red → safe for players crossing the NS road
        // ewGoesFirst = false → NS is green, EW is red → safe for players crossing the EW road
        bool ewGoesFirst = crossingRoad == PlayerCrossesRoad.NorthSouth ? safe : !safe;
        signals.StartCycleFrom(ewGoesFirst);
    }
}
