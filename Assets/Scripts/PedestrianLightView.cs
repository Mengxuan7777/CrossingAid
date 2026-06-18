using System.Collections;
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

    private Coroutine _flashCoroutine;

    public void SetState(PedestrianLightState state)
    {
        StopFlashing();
        ApplyMaterials(state, true);
    }

    public void StartFlashing(PedestrianLightState state, float interval = 0.4f)
    {
        StopFlashing();
        _flashCoroutine = StartCoroutine(FlashCoroutine(state, interval));
    }

    private void StopFlashing()
    {
        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
            _flashCoroutine = null;
        }
    }

    private IEnumerator FlashCoroutine(PedestrianLightState state, float interval)
    {
        bool lit = true;
        while (true)
        {
            ApplyMaterials(state, lit);
            lit = !lit;
            yield return new WaitForSeconds(interval);
        }
    }

    private void ApplyMaterials(PedestrianLightState state, bool lit)
    {
        bool walk = state == PedestrianLightState.Walk;
        if (handRenderer != null)
            handRenderer.material = (!walk && lit) ? handOn : handOff;
        if (figureRenderers != null)
            foreach (var r in figureRenderers)
                if (r != null) r.material = (walk && lit) ? figureOn : figureOff;
    }
}
