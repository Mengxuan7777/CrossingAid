using UnityEngine;

public class PedestrianLightView : MonoBehaviour
{
    [Header("Renderers")]
    public Renderer handRenderer;
    public Renderer[] figureRenderers;

    [Header("Hand Materials (Don't Walk)")]
    public Material handOn;
    public Material handOff;

    [Header("Figure Materials (Walk)")]
    public Material figureOn;
    public Material figureOff;

    public void SetState(PedestrianLightState state)
    {
        bool walk = state == PedestrianLightState.Walk;

        if (handRenderer != null)
            handRenderer.material = walk ? handOff : handOn;

        if (figureRenderers != null)
            foreach (var r in figureRenderers)
                if (r != null) r.material = walk ? figureOn : figureOff;
    }
}
