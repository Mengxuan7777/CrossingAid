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
    [Tooltip("GameObject shown during TextReading trials (world-space canvas).")]
    public GameObject     textPanel;
    [Tooltip("TextMeshPro on the text panel — shows distractionText to the player.")]
    public TextMeshProUGUI panelText;
    [Tooltip("Seconds each message stays visible.")]
    public float textDisplayDuration = 10f;

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

    [Header("Finish Zone")]
    [Tooltip("All EndPin CrossingFinishZones — unlocked after each trial starts so they can detect the next arrival.")]
    public CrossingFinishZone[] finishZones;

    [Header("Car Hit")]
    public PlayerCarCollision carHitDetector;

    [Header("Ambient Sound")]
    [Tooltip("Pool of ambient sound sources (e.g. traffic hum, birds, wind, crowd). Each should already have its own AudioClip assigned.")]
    public AudioSource[] ambientSoundSources;
    [Tooltip("How many of the pool to randomly pick and play at the start of each trial.")]
    public int ambientSoundsToPlay = 2;

    [Header("Zones")]
    public ZoneTracker zoneTracker;

    [Header("Debug / Researcher Keys")]
    public bool    enableDebugKeys    = true;
    public KeyCode loadConfigKey      = KeyCode.L;
    public KeyCode nextTrialKey       = KeyCode.N;
    public KeyCode prevTrialKey       = KeyCode.P;
    [Tooltip("Press during a Conversation trial to log that the player answered CORRECTLY.")]
    public KeyCode answerCorrectKey      = KeyCode.Y;
    [Tooltip("Press during a Conversation trial to log that the player answered INCORRECTLY.")]
    public KeyCode answerWrongKey        = KeyCode.U;
    [Tooltip("Manually trigger the distraction for the current trial (simulates zone entry).")]
    public KeyCode triggerDistractionKey = KeyCode.T;

    private ExperimentConfig _config;
    private int _currentIndex = -1;
    private Coroutine _textPanelCoroutine;
    private Coroutine _phoneCallCoroutine;
    private Coroutine _startTrialCoroutine;
    private List<string> _currentQuestions = new List<string>();
    private List<string> _currentAnswers   = new List<string>();
    private List<AudioClip> _currentAudioClips = new List<AudioClip>();
    private int _currentQuestionIndex;
    private int _activeTextItemIndex = -1;
    private int _activePhoneItemIndex = -1;
    private float _textItemStartTime;
    private float _phoneItemStartTime;
    private DistractionType _pendingDistractionType = DistractionType.None;
    private List<string> _pendingTexts = new List<string>();
    private bool[] _itemTriggered = new bool[3];

    public int CurrentTrialIndex => _currentIndex;
    public int TotalTrials       => _config?.trials?.Count ?? 0;

    private void Start() => StartWarmup();

    private void StartWarmup()
    {
        // Position the player at the floor-level start pose immediately, independent of
        // trial logging/arming — otherwise the player sits at the XR Origin's raw scene
        // Transform (often not floor-aligned) until the first real trial starts.
        experimentController?.ResetPlayerToStart();

        if (peripheralCues != null) foreach (var c in peripheralCues) if (c != null) c.enabled = false;
        if (curbIndicators != null) foreach (var c in curbIndicators) if (c != null) c.enabled = false;
        if (timerDisplays  != null) foreach (var c in timerDisplays)  if (c != null) c.enabled = false;

        phoneObject?.SetActive(false);
        textPanel?.SetActive(false);
        trialIDPanel?.SetActive(false);
        if (conversationAudio != null) conversationAudio.Stop();
        if (questionDisplay != null) questionDisplay.text = "";

        Debug.Log("[TrialSequencer] Warmup — press L when the participant is ready to begin trials.");
    }

    private void Update()
    {
        if (!enableDebugKeys) return;
        if (Input.GetKeyDown(loadConfigKey))     LoadConfig();
        if (Input.GetKeyDown(nextTrialKey))      NextTrial();
        if (Input.GetKeyDown(prevTrialKey))      PreviousTrial();
        if (Input.GetKeyDown(answerCorrectKey))      LogPhoneAnswer(correct: true);
        if (Input.GetKeyDown(answerWrongKey))        LogPhoneAnswer(correct: false);
        if (Input.GetKeyDown(triggerDistractionKey)) TriggerNextDistractionItem();
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
        Debug.Log($"[TrialSequencer] Loaded {_config.trials.Count} trials for participant '{_config.participantId}' — starting trial 1.");
        StartTrial(0);
    }

    public void NextTrial()
    {
        if (_config == null) { Debug.LogWarning("[TrialSequencer] No config loaded."); return; }
        int next = _currentIndex + 1;
        if (next >= _config.trials.Count) { CompleteExperiment(); return; }
        StartTrial(next);
    }

    private void CompleteExperiment()
    {
        if (experimentController != null &&
            experimentController.logger != null &&
            experimentController.logger.TrialActive)
            experimentController.EndTrial("ExperimentComplete");

        if (peripheralCues != null) foreach (var c in peripheralCues) if (c != null) c.enabled = false;
        if (curbIndicators != null) foreach (var c in curbIndicators) if (c != null) c.enabled = false;
        if (timerDisplays  != null) foreach (var c in timerDisplays)  if (c != null) c.enabled = false;
        phoneObject?.SetActive(false);
        textPanel?.SetActive(false);

        if (trialIDText != null) trialIDText.text = "Thank you for participating in the experiment.";
        trialIDPanel?.SetActive(true);

        Debug.Log("[TrialSequencer] All trials complete.");
    }

    public void PreviousTrial()
    {
        if (_config == null || _currentIndex <= 0) return;
        StartTrial(_currentIndex - 1);
    }

    public void ShowMessage(string message, float duration)
    {
        if (trialIDText != null) trialIDText.text = message;
        trialIDPanel?.SetActive(true);
        StartCoroutine(HideMessageAfter(duration));
    }

    private IEnumerator HideMessageAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        trialIDPanel?.SetActive(false);
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
        if (finishZones != null) foreach (var z in finishZones) z?.Unlock();
        carHitDetector?.ResetHit();
        zoneTracker?.ResetForNewTrial();

        ApplyAssistance(t.GetAssistanceLevel());
        ApplyDistraction(t.GetDistractionType(), t.distractionTexts, t.questionTexts, t.correctAnswers, t.pairId);
        ApplySignal(t.signalSecondsRemaining);

        endPinAnimator?.Play();
        startArrowAnimator?.Play();
        PlayRandomAmbientSounds();

        if (experimentController != null)
        {
            experimentController.participantId   = _config.participantId;
            experimentController.sessionId       = _config.sessionId;
            experimentController.conditionName   = t.conditionName;
            experimentController.distractionType = t.distraction;
            experimentController.trialNumber     = _currentIndex + 1;
            experimentController.Arm();
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

        string result = correct ? "Correct" : "Incorrect";
        string expected = _currentQuestionIndex < _currentAnswers.Count ? _currentAnswers[_currentQuestionIndex] : "";
        experimentController?.WriteCustomEvent($"PhoneAnswer_Q{_currentQuestionIndex + 1}", result);
        Debug.Log($"[TrialSequencer] Phone answer Q{_currentQuestionIndex + 1} logged: {result} | Expected: '{expected}'");
        // Note: _currentQuestionIndex advances automatically with the audio in PlayPhoneCallTimed.
    }

    // ── Condition appliers ───────────────────────────────────────────────────

    private void ApplyAssistance(AssistanceLevel level)
    {
        bool on = level == AssistanceLevel.FullyAssisted;
        if (peripheralCues != null) foreach (var c in peripheralCues) if (c != null) c.enabled = on;
        if (curbIndicators != null) foreach (var c in curbIndicators) if (c != null) c.enabled = on;
        if (timerDisplays  != null) foreach (var c in timerDisplays)  if (c != null) c.enabled = on;
    }

    // Logs an interrupted End event for whatever distraction item is still showing, then
    // stops its coroutine. Without this, advancing trials mid-distraction (e.g. reaching
    // the finish zone before the text duration elapses) left a Start event with no End.
    private void InterruptActiveDistraction()
    {
        if (_activeTextItemIndex >= 0)
        {
            float elapsed = Time.time - _textItemStartTime;
            experimentController?.WriteCustomEvent($"TextReadingEnd_Item{_activeTextItemIndex + 1}", $"interrupted,elapsed={elapsed:F2}");
            _activeTextItemIndex = -1;
        }
        if (_activePhoneItemIndex >= 0)
        {
            float elapsed = Time.time - _phoneItemStartTime;
            experimentController?.WriteCustomEvent($"PhoneCallEnd_Q{_activePhoneItemIndex + 1}", $"interrupted,elapsed={elapsed:F2}");
            _activePhoneItemIndex = -1;
        }

        if (_phoneCallCoroutine != null) { StopCoroutine(_phoneCallCoroutine); _phoneCallCoroutine = null; }
        if (_textPanelCoroutine != null) { StopCoroutine(_textPanelCoroutine); _textPanelCoroutine = null; }

        phoneObject?.SetActive(false);
        textPanel?.SetActive(false);
        if (conversationAudio != null) conversationAudio.Stop();
    }

    private void ApplyDistraction(DistractionType type, List<string> texts, List<string> questions, List<string> answers, string pairId)
    {
        InterruptActiveDistraction();

        // Cache everything for when TriggerDistraction() fires from a zone.
        _pendingDistractionType = type;
        _pendingTexts           = texts ?? new List<string>();
        _currentQuestions       = questions ?? new List<string>();
        _currentAnswers         = answers   ?? new List<string>();
        _currentAudioClips      = GetAudioClipsForPair(pairId);
        _currentQuestionIndex = 0;
        Array.Clear(_itemTriggered, 0, _itemTriggered.Length);

        UpdateQuestionDisplay();
    }

    // Called by DistractionTriggerZone — triggers one specific message or clip by index.
    public void TriggerDistractionItem(int itemIndex)
    {
        if (itemIndex < 0 || itemIndex >= _itemTriggered.Length) return;
        if (_itemTriggered[itemIndex]) return;
        _itemTriggered[itemIndex] = true;

        experimentController?.WriteCustomEvent($"DistractionTriggered_Item{itemIndex + 1}", _pendingDistractionType.ToString());

        if (_pendingDistractionType == DistractionType.TextReading && itemIndex < _pendingTexts.Count)
        {
            InterruptActiveDistraction();
            _textPanelCoroutine = StartCoroutine(ShowSingleMessage(itemIndex));
        }
        else if (_pendingDistractionType == DistractionType.Conversation && itemIndex < _currentAudioClips.Count)
        {
            InterruptActiveDistraction();
            _currentQuestionIndex = itemIndex;
            UpdateQuestionDisplay();
            _phoneCallCoroutine = StartCoroutine(PlaySingleClip(itemIndex));
        }
    }

    // Debug key T: trigger the next un-triggered item.
    private void TriggerNextDistractionItem()
    {
        for (int i = 0; i < _itemTriggered.Length; i++)
            if (!_itemTriggered[i]) { TriggerDistractionItem(i); return; }
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

    private IEnumerator PlaySingleClip(int itemIndex)
    {
        if (conversationAudio == null || itemIndex >= _currentAudioClips.Count) yield break;
        AudioClip clip = _currentAudioClips[itemIndex];
        if (clip == null) yield break;

        _activePhoneItemIndex = itemIndex;
        _phoneItemStartTime = Time.time;

        phoneObject?.SetActive(true);
        experimentController?.WriteCustomEvent($"PhoneCallStart_Q{itemIndex + 1}", "");
        conversationAudio.clip = clip;
        conversationAudio.Play();
        yield return new WaitForSeconds(clip.length);
        phoneObject?.SetActive(false);
        experimentController?.WriteCustomEvent($"PhoneCallEnd_Q{itemIndex + 1}", "");
        _activePhoneItemIndex = -1;
        _phoneCallCoroutine = null;
    }

    private IEnumerator ShowSingleMessage(int itemIndex)
    {
        string text = itemIndex < _pendingTexts.Count ? _pendingTexts[itemIndex] : "";
        _activeTextItemIndex = itemIndex;
        _textItemStartTime = Time.time;

        experimentController?.WriteCustomEvent($"TextReadingStart_Item{itemIndex + 1}", "");
        if (panelText != null) panelText.text = text;
        textPanel?.SetActive(true);
        yield return new WaitForSeconds(textDisplayDuration);
        textPanel?.SetActive(false);
        experimentController?.WriteCustomEvent($"TextReadingEnd_Item{itemIndex + 1}", "");
        _activeTextItemIndex = -1;
        _textPanelCoroutine = null;
    }

    private void ApplySignal(float secondsRemaining)
    {
        if (signals == null) return;
        bool nsRoadIsSafe = crossingRoad == PlayerCrossesRoad.NorthSouth;
        signals.StartCycleWithRemaining(nsRoadIsSafe, secondsRemaining);
    }

    private void PlayRandomAmbientSounds()
    {
        foreach (var src in ambientSoundSources)
            src?.Stop();

        if (ambientSoundSources == null || ambientSoundSources.Length == 0) return;

        var indices = new List<int>();
        for (int i = 0; i < ambientSoundSources.Length; i++) indices.Add(i);

        int count = Mathf.Min(ambientSoundsToPlay, ambientSoundSources.Length);

        for (int i = 0; i < count; i++)
        {
            int pick = UnityEngine.Random.Range(0, indices.Count);
            int idx = indices[pick];
            indices.RemoveAt(pick);

            AudioSource src = ambientSoundSources[idx];
            if (src == null) continue;
            src.Play();
        }
    }
}
