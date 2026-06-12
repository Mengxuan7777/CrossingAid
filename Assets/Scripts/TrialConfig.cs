using System;
using System.Collections.Generic;

public enum AssistanceLevel   { Unassisted, FullyAssisted }
public enum DistractionType   { None, Conversation, TextReading }
public enum PlayerCrossesRoad { NorthSouth, EastWest }

[Serializable]
public class TrialDefinition
{
    public int    trialNumber      = 1;
    public string conditionName    = "";
    public string assistanceLevel  = "Unassisted";
    public string distraction      = "None";
    public string distractionText  = "";

    // Seconds remaining on the player's "walk" signal when the trial starts,
    // before it changes (turns yellow then red).
    public float signalSecondsRemaining = 8f;

    // Pairing: TextReading and its follow-up Conversation share the same pairId.
    // Empty for None-distraction trials.
    public string pairId        = "";

    // Conversation trials only: question asked during the phone call (shown to researcher).
    public string questionText  = "";

    // Conversation trials only: expected correct answer (shown to researcher).
    public string correctAnswer = "";

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
