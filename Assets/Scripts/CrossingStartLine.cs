using UnityEngine;

public class CrossingStartLine : MonoBehaviour
{
    public EyeTrackingLogger logger;
    public string crossingLineId = "CurbLine_A";
    public string participantTag = "Player";
    public bool restrictByTag = true;
    public bool endTrialOnCrossing = false;

    private void OnTriggerEnter(Collider other)
    {
        if (logger == null || !logger.TrialActive)
        {
            return;
        }

        if (restrictByTag && !other.CompareTag(participantTag))
        {
            return;
        }

        if (logger.CrossingInitiationLogged)
        {
            return;
        }

        logger.MarkCrossingInitiation(crossingLineId);

        if (endTrialOnCrossing)
        {
            logger.EndTrial("CrossedStartLine");
        }
    }
}