using System.Collections;
using TMPro;
using UnityEngine;

public class TrialSequencer : MonoBehaviour
{
    [Header("Core References")]
    public ExperimentController experimentController;
    public IntersectionSignalController signals;

    [Header("Config")]
    [Tooltip("Drag a trial_config_*.json file here (e.g. from Assets/Scripts/TrialConfigs/).")]
    public TextAsset configFile;

    [Header("Assistance Systems")]
    public PeripheralCue[]        peripheralCues;
    public CurbIndicator[]        curbIndicators;
    public CrossingTimerDisplay[] timerDisplays;

    [Header("Distraction Objects")]
    [Tooltip("GameObject shown during Conversation trials (phone mesh).")]
    public GameObject     phoneObject;
    [Tooltip("Audio played during Conversation trials (background call sound).")]
    public AudioSource    conversationAudio;
    [Tooltip("GameObject shown during TextReading trials (world-space canvas).")]
    public GameObject     textPanel;
    [Tooltip("TextMeshPro on the text panel — shows distractionText to the player.")]
    public TextMeshProUGUI panelText;
    [Tooltip("Seconds the text panel stays visible during TextReading trials before it auto-hides.")]
    public float textDisplayDuration = 10f;

    [Header("Researcher Display")]
    [Tooltip("UI text shown only to the researcher. Displays the phone-call question during Conversation trials.")]
    public TextMeshProUGUI questionDisplay;

    [Header("Crossing Direction")]
    [Tooltip("Which road the player crosses. Determines which signal state counts as Safe.")]
    public PlayerCrossesRoad crossingRoad = PlayerCrossesRoad.NorthSouth;

    [Header("Start/End Markers")]
    [Tooltip("Plays its float/pulse animation from the beginning each time a trial starts.")]
    public EndPinAnimator endPinAnimator;
    [Tooltip("Plays its wipe animation from the beginning each time a trial starts.")]
    public StartArrowAnimator startArrowAnimator;

    [Header("Debug / Researcher Keys")]
    public bool    enableDebugKeys    = true;
    public KeyCode loadConfigKey      = KeyCode.L;
    public KeyCode nextTrialKey       = KeyCode.N;
    public KeyCode prevTrialKey       = KeyCode.P;
    [Tooltip("Press during a Conversation trial to log that the player answered CORRECTLY.")]
    public KeyCode answerCorrectKey   = KeyCode.Y;
    [Tooltip("Press during a Conversation trial to log that the player answered INCORRECTLY.")]
    public KeyCode answerWrongKey     = KeyCode.U;

    private ExperimentConfig _config;
    private int _currentIndex = -1;
    private Coroutine _textPanelCoroutine;

    public int CurrentTrialIndex => _currentIndex;
    public int TotalTrials       => _config?.trials?.Count ?? 0;

    private void Start() => LoadConfig();

    private void Update()
    {
        if (!enableDebugKeys) return;
        if (Input.GetKeyDown(loadConfigKey))     LoadConfig();
        if (Input.GetKeyDown(nextTrialKey))      NextTrial();
        if (Input.GetKeyDown(prevTrialKey))      PreviousTrial();
        if (Input.GetKeyDown(answerCorrectKey))  LogPhoneAnswer(correct: true);
        if (Input.GetKeyDown(answerWrongKey))    LogPhoneAnswer(correct: false);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void LoadConfig()
    {
        if (configFile == null)
        {
            Debug.LogError("[TrialSequencer] No config assigned — drag a trial_config_*.json into the Config File field in the Inspector.");
            return;
        }
        _config = JsonUtility.FromJson<ExperimentConfig>(configFile.text);
        _currentIndex = -1;
        Debug.Log($"[TrialSequencer] Loaded {_config.trials.Count} trials for participant '{_config.participantId}'");
    }

    public void NextTrial()
    {
        if (_config == null) { Debug.LogWarning("[TrialSequencer] No config loaded."); return; }
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

        if (experimentController != null &&
            experimentController.logger != null &&
            experimentController.logger.TrialActive)
            experimentController.EndTrial("TrialSequencerAdvance");

        _currentIndex = index;
        TrialDefinition t = _config.trials[index];

        ApplyAssistance(t.GetAssistanceLevel());
        ApplyDistraction(t.GetDistractionType(), t.distractionText, t.questionText, t.correctAnswer);
        ApplySignal(t.signalSecondsRemaining);

        endPinAnimator?.Play();
        startArrowAnimator?.Play();

        if (experimentController != null)
        {
            experimentController.participantId   = _config.participantId;
            experimentController.sessionId       = _config.sessionId;
            experimentController.conditionName   = t.conditionName;
            experimentController.distractionType = t.distraction;
            experimentController.trialNumber     = t.trialNumber;
            experimentController.StartTrial();
        }

        string pairNote = string.IsNullOrEmpty(t.pairId) ? "" : $" [{t.pairId}]";
        Debug.Log($"[TrialSequencer] Trial {t.trialNumber}/{_config.trials.Count}: {t.conditionName}{pairNote}");
    }

    // ── Researcher answer logging ────────────────────────────────────────────

    private void LogPhoneAnswer(bool correct)
    {
        if (_config == null || _currentIndex < 0) return;
        TrialDefinition t = _config.trials[_currentIndex];
        if (t.GetDistractionType() != DistractionType.Conversation)
        {
            Debug.LogWarning("[TrialSequencer] Answer key pressed but current trial is not a Conversation trial.");
            return;
        }

        string result = correct ? "Correct" : "Incorrect";
        experimentController?.WriteCustomEvent("PhoneAnswer", result);
        Debug.Log($"[TrialSequencer] Phone answer logged: {result} | Expected: '{t.correctAnswer}'");
    }

    // ── Condition appliers ───────────────────────────────────────────────────

    private void ApplyAssistance(AssistanceLevel level)
    {
        bool on = level == AssistanceLevel.FullyAssisted;
        if (peripheralCues != null) foreach (var c in peripheralCues) if (c != null) c.enabled = on;
        if (curbIndicators != null) foreach (var c in curbIndicators) if (c != null) c.enabled = on;
        if (timerDisplays  != null) foreach (var c in timerDisplays)  if (c != null) c.enabled = on;
    }

    private void ApplyDistraction(DistractionType type, string text, string question, string answer)
    {
        bool isCall = type == DistractionType.Conversation;
        bool isText = type == DistractionType.TextReading;

        if (phoneObject != null) phoneObject.SetActive(isCall);

        if (conversationAudio != null)
        {
            if (isCall) conversationAudio.Play();
            else        conversationAudio.Stop();
        }

        if (_textPanelCoroutine != null)
        {
            StopCoroutine(_textPanelCoroutine);
            _textPanelCoroutine = null;
        }

        if (isText)
        {
            if (panelText != null)
                panelText.text = string.IsNullOrEmpty(text) ? "Read this text carefully while crossing." : text;
            _textPanelCoroutine = StartCoroutine(ShowTextPanelTimed());
        }
        else
        {
            textPanel?.SetActive(false);
        }

        // Show question to researcher during phone-call trials
        if (questionDisplay != null)
        {
            if (isCall && !string.IsNullOrEmpty(question))
                questionDisplay.text = $"ASK: {question}\nEXPECT: {answer}\n[Y] Correct   [U] Incorrect";
            else
                questionDisplay.text = "";
        }

        // Also log to console so the researcher can see it even without a UI
        if (isCall && !string.IsNullOrEmpty(question))
            Debug.Log($"[TrialSequencer] PHONE QUESTION: {question}  |  CORRECT ANSWER: {answer}");
    }

    private IEnumerator ShowTextPanelTimed()
    {
        textPanel?.SetActive(true);
        experimentController?.WriteCustomEvent("TextReadingStart", "");

        yield return new WaitForSeconds(textDisplayDuration);

        textPanel?.SetActive(false);
        experimentController?.WriteCustomEvent("TextReadingEnd", textDisplayDuration.ToString("F2"));
        _textPanelCoroutine = null;
    }

    private void ApplySignal(float secondsRemaining)
    {
        if (signals == null) return;
        bool nsRoadIsSafe = crossingRoad == PlayerCrossesRoad.NorthSouth;
        signals.StartCycleWithRemaining(nsRoadIsSafe, secondsRemaining);
    }
}
