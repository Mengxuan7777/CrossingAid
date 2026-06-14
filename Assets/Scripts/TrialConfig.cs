using System;
using System.Collections.Generic;

public enum AssistanceLevel   { Unassisted, FullyAssisted }
public enum DistractionType   { None, Conversation, TextReading }
public enum PlayerCrossesRoad { NorthSouth, EastWest }

[Serializable]
public class TrialDefinition
{
    public string trialID          = "";
    public string conditionName    = "";
    public string assistanceLevel  = "Unassisted";
    public string distraction      = "None";

    // TextReading trials only: messages shown one at a time, each for
    // textDisplayDuration seconds, with a gap between each.
    public List<string> distractionTexts = new List<string>();

    // Seconds remaining on the player's "walk" signal when the trial starts,
    // before it changes (turns yellow then red).
    public float signalSecondsRemaining = 8f;

    // Pairing: TextReading and its follow-up Conversation share the same pairId.
    // Empty for None-distraction trials.
    public string pairId        = "";

    // Conversation trials only: questions asked during the phone call (shown to researcher),
    // one per message from the paired TextReading trial's distractionTexts.
    public List<string> questionTexts  = new List<string>();

    // Conversation trials only: expected correct answers, matching questionTexts by index.
    public List<string> correctAnswers = new List<string>();

    public AssistanceLevel GetAssistanceLevel() => Enum.TryParse(assistanceLevel, out AssistanceLevel v) ? v : AssistanceLevel.Unassisted;
    public DistractionType GetDistractionType() => Enum.TryParse(distraction,     out DistractionType v) ? v : DistractionType.None;
}

[Serializable]
public class ExperimentConfig
{
    public string participantId = "P001";
    public string sessionId     = "S001";
    public List<TrialDefinition> trials = new List<TrialDefinition>();
}
