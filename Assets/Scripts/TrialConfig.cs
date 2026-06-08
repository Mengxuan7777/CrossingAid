using System;
using System.Collections.Generic;

public enum AssistanceLevel  { Unassisted, FullyAssisted }
public enum DistractionType  { None, Conversation, TextReading }
public enum CrossingScenario { Safe, Unsafe }
public enum PlayerCrossesRoad { NorthSouth, EastWest }

[Serializable]
public class TrialDefinition
{
    public int    trialNumber      = 1;
    public string conditionName    = "";
    public string assistanceLevel  = "Unassisted";
    public string distraction      = "None";
    public string crossingScenario = "Safe";
    public string distractionText  = "";

    public AssistanceLevel  GetAssistanceLevel()  => Enum.TryParse(assistanceLevel,  out AssistanceLevel  v) ? v : AssistanceLevel.Unassisted;
    public DistractionType  GetDistractionType()  => Enum.TryParse(distraction,      out DistractionType  v) ? v : DistractionType.None;
    public CrossingScenario GetCrossingScenario() => Enum.TryParse(crossingScenario, out CrossingScenario v) ? v : CrossingScenario.Safe;
}

[Serializable]
public class ExperimentConfig
{
    public string participantId = "P001";
    public string sessionId     = "S001";
    public List<TrialDefinition> trials = new List<TrialDefinition>();
}
