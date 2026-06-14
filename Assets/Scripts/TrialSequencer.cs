using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[Serializable]
public class PairAudioClips
{
    [Tooltip("Must match a trial's pairId (e.g. \"pair_A\").")]
    public string pairId;

    [Tooltip("3 recorded question clips, in the same order as that pair's questionTexts.")]
    public AudioClip[] questionClips = new AudioClip[3];
}

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
    [Tooltip("GameObject shown during Conversation trials (phone icon).")]
    public GameObject     phoneObject;
    [Tooltip("Audio source the recorded questions are played through during Conversation trials.")]
    public AudioSource    conversationAudio;
    [Tooltip("Recorded question audio clips, grouped by pairId (3 clips per pair, matching questionTexts order).")]
    public PairAudioClips[] questionAudioClips;
    [Tooltip("Seconds after the trial starts before the phone call begins.")]
    public float phoneCallDelay = 5f;
    [Tooltip("Seconds between one recorded question ending and the next one starting.")]
    public float phoneCallQuestionInterval = 5f;
    [Tooltip("GameObject shown during TextReading trials (world-space canvas).")]
    public GameObject     textPanel;
    [Tooltip("TextMeshPro on the text panel — shows distractionText to the player.")]
    public TextMeshProUGUI panelText;
    [Tooltip("Seconds each message stays visible.")]
    public float textDisplayDuration = 10f;
    [Tooltip("Seconds after the trial starts before the first message appears.")]
    public float textDisplayDelay = 5f;
    [Tooltip("Seconds between one message disappearing and the next appearing.")]
    public float textDisplayInterval = 3f;

    [Header("Researcher Display")]
    [Tooltip("UI text shown only to the researcher. Displays the phone-call question during Conversation trials.")]
    public TextMeshProUGUI questionDisplay;

    [Header("Trial ID Display")]
    [Tooltip("GameObject shown briefly before each trial starts, displaying the trial ID.")]
    public GameObject trialIDPanel;
    [Tooltip("TextMeshPro that shows the upcoming trial's ID.")]
    public TextMeshProUGUI trialIDText;
    [Tooltip("Seconds to show the trial ID before the trial begins.")]
    public float trialIDDisplayDuration = 3f;

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
    private Coroutine _phoneCallCoroutine;
    private Coroutine _startTrialCoroutine;
    private List<string> _currentQuestions = new List<string>();
    private List<string> _currentAnswers   = new List<string>();
    private List<AudioClip> _currentAudioClips = new List<AudioClip>();
    private int _currentQuestionIndex;

    public int CurrentTrialIndex => _currentIndex;
    public int TotalTrials       => _config?.trials?.Count ?? 0;

    private void Start()
    {
        LoadConfig();
        StartTrial(0);
    }

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

        if (_startTrialCoroutine != null)
            StopCoroutine(_startTrialCoroutine);
        _startTrialCoroutine = StartCoroutine(StartTrialRoutine(index));
    }

    private IEnumerator StartTrialRoutine(int index)
    {
        if (experimentController != null &&
            experimentController.logger != null &&
            experimentController.logger.TrialActive)
            experimentController.EndTrial("TrialSequencerAdvance");

        _currentIndex = index;
        TrialDefinition t = _config.trials[index];

        if (trialIDText != null) trialIDText.text = t.trialID;
        trialIDPanel?.SetActive(true);
        yield return new WaitForSeconds(trialIDDisplayDuration);
        trialIDPanel?.SetActive(false);

        ApplyAssistance(t.GetAssistanceLevel());
        ApplyDistraction(t.GetDistractionType(), t.distractionTexts, t.questionTexts, t.correctAnswers, t.pairId);
        ApplySignal(t.signalSecondsRemaining);

        endPinAnimator?.Play();
        startArrowAnimator?.Play();

        if (experimentController != null)
        {
            experimentController.participantId   = _config.participantId;
            experimentController.sessionId       = _config.sessionId;
            experimentController.conditionName   = t.conditionName;
            experimentController.distractionType = t.distraction;
            experimentController.trialNumber     = _currentIndex + 1;
            experimentController.StartTrial();
        }

        string pairNote = string.IsNullOrEmpty(t.pairId) ? "" : $" [{t.pairId}]";
        Debug.Log($"[TrialSequencer] Trial {_currentIndex + 1}/{_config.trials.Count} ({t.trialID}): {t.conditionName}{pairNote}");

        _startTrialCoroutine = null;
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

        if (_currentQuestionIndex >= _currentQuestions.Count)
        {
            Debug.LogWarning("[TrialSequencer] All phone questions for this trial have already been answered.");
            return;
        }

        string result = correct ? "Correct" : "Incorrect";
        string expected = _currentAnswers[_currentQuestionIndex];
        experimentController?.WriteCustomEvent($"PhoneAnswer_Q{_currentQuestionIndex + 1}", result);
        Debug.Log($"[TrialSequencer] Phone answer Q{_currentQuestionIndex + 1} logged: {result} | Expected: '{expected}'");

        _currentQuestionIndex++;
        UpdateQuestionDisplay();
    }

    // ── Condition appliers ───────────────────────────────────────────────────

    private void ApplyAssistance(AssistanceLevel level)
    {
        bool on = level == AssistanceLevel.FullyAssisted;
        if (peripheralCues != null) foreach (var c in peripheralCues) if (c != null) c.enabled = on;
        if (curbIndicators != null) foreach (var c in curbIndicators) if (c != null) c.enabled = on;
        if (timerDisplays  != null) foreach (var c in timerDisplays)  if (c != null) c.enabled = on;
    }

    private void ApplyDistraction(DistractionType type, List<string> texts, List<string> questions, List<string> answers, string pairId)
    {
        bool isCall = type == DistractionType.Conversation;
        bool isText = type == DistractionType.TextReading;

        if (_phoneCallCoroutine != null)
        {
            StopCoroutine(_phoneCallCoroutine);
            _phoneCallCoroutine = null;
        }

        phoneObject?.SetActive(false);
        if (conversationAudio != null) conversationAudio.Stop();

        _currentQuestions = questions ?? new List<string>();
        _currentAnswers   = answers   ?? new List<string>();
        _currentAudioClips = GetAudioClipsForPair(pairId);
        _currentQuestionIndex = 0;

        if (isCall)
            _phoneCallCoroutine = StartCoroutine(PlayPhoneCallTimed());

        if (_textPanelCoroutine != null)
        {
            StopCoroutine(_textPanelCoroutine);
            _textPanelCoroutine = null;
        }

        if (isText)
        {
            textPanel?.SetActive(false);
            _textPanelCoroutine = StartCoroutine(ShowTextMessagesTimed(texts));
        }
        else
        {
            textPanel?.SetActive(false);
        }

        UpdateQuestionDisplay();
    }

    // ── Researcher question display ─────────────────────────────────────────

    private void UpdateQuestionDisplay()
    {
        if (questionDisplay == null) return;

        if (_currentQuestions.Count == 0 || _currentQuestionIndex >= _currentQuestions.Count)
        {
            questionDisplay.text = "";
            return;
        }

        string question = _currentQuestions[_currentQuestionIndex];
        string answer   = _currentQuestionIndex < _currentAnswers.Count ? _currentAnswers[_currentQuestionIndex] : "";

        questionDisplay.text =
            $"Question {_currentQuestionIndex + 1} of {_currentQuestions.Count}\n" +
            $"ASK: {question}\nEXPECT: {answer}\n[Y] Correct   [U] Incorrect";

        Debug.Log($"[TrialSequencer] PHONE QUESTION {_currentQuestionIndex + 1}/{_currentQuestions.Count}: {question}  |  CORRECT ANSWER: {answer}");
    }

    private List<AudioClip> GetAudioClipsForPair(string pairId)
    {
        if (!string.IsNullOrEmpty(pairId) && questionAudioClips != null)
            foreach (var set in questionAudioClips)
                if (set.pairId == pairId)
                    return new List<AudioClip>(set.questionClips);

        return new List<AudioClip>();
    }

    private IEnumerator PlayPhoneCallTimed()
    {
        yield return new WaitForSeconds(phoneCallDelay);

        phoneObject?.SetActive(true);
        experimentController?.WriteCustomEvent("PhoneCallStart", "");
        UpdateQuestionDisplay();

        if (conversationAudio != null)
        {
            for (int i = 0; i < _currentAudioClips.Count; i++)
            {
                AudioClip clip = _currentAudioClips[i];
                if (clip != null)
                {
                    conversationAudio.clip = clip;
                    conversationAudio.Play();
                    yield return new WaitForSeconds(clip.length);
                }

                if (i < _currentAudioClips.Count - 1)
                    yield return new WaitForSeconds(phoneCallQuestionInterval);
            }
        }

        phoneObject?.SetActive(false);
        experimentController?.WriteCustomEvent("PhoneCallEnd", "");
        _phoneCallCoroutine = null;
    }

    private IEnumerator ShowTextMessagesTimed(List<string> texts)
    {
        if (texts == null || texts.Count == 0)
            texts = new() { "Read this text carefully while crossing." };

        yield return new WaitForSeconds(textDisplayDelay);

        experimentController?.WriteCustomEvent("TextReadingStart", "");

        for (int i = 0; i < texts.Count; i++)
        {
            if (panelText != null) panelText.text = texts[i];
            textPanel?.SetActive(true);

            yield return new WaitForSeconds(textDisplayDuration);

            textPanel?.SetActive(false);

            if (i < texts.Count - 1)
                yield return new WaitForSeconds(textDisplayInterval);
        }

        experimentController?.WriteCustomEvent("TextReadingEnd", "");
        _textPanelCoroutine = null;
    }

    private void ApplySignal(float secondsRemaining)
    {
        if (signals == null) return;
        bool nsRoadIsSafe = crossingRoad == PlayerCrossesRoad.NorthSouth;
        signals.StartCycleWithRemaining(nsRoadIsSafe, secondsRemaining);
    }
}
