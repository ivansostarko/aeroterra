using UnityEngine;

namespace AeroTerra.Drone
{
    /// <summary>Tracks attached payload mass; affects total mass and battery drain.</summary>
    public class PayloadSystem : MonoBehaviour
    {
        public float CurrentPayloadKg { get; private set; }

        public void Configure(float payloadKg)
        {
            CurrentPayloadKg = Mathf.Max(0f, payloadKg);
            var flight = GetComponent<DroneFlightController>();
            if (flight != null) flight.ApplyMass();
        }
    }
}
