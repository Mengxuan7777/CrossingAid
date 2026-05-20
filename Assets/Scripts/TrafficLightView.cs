using UnityEngine;

public enum VehicleLightState
{
    Red,
    Yellow,
    Green
}

public class TrafficLightView : MonoBehaviour
{
    [Header("Lens Renderers")]
    public Renderer redLens;
    public Renderer yellowLens;
    public Renderer greenLens;

    [Header("On Emission Materials")]
    public Material redOn;
    public Material yellowOn;
    public Material greenOn;

    [Header("Off Materials")]
    public Material redOff;
    public Material yellowOff;
    public Material greenOff;

    public void SetState(VehicleLightState state)
    {
        redLens.material = state == VehicleLightState.Red ? redOn : redOff;
        yellowLens.material = state == VehicleLightState.Yellow ? yellowOn : yellowOff;
        greenLens.material = state == VehicleLightState.Green ? greenOn : greenOff;
    }
}