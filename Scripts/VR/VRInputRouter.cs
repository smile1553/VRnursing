using UnityEngine;

public class VRInputRouter : MonoBehaviour
{
    private static class SignalBus
    {
        public delegate void SignalHandler(string signalName, object payload);

        public static event SignalHandler OnSignal;

        public static void Subscribe(SignalHandler handler)
        {
            OnSignal += handler;
        }

        public static void Unsubscribe(SignalHandler handler)
        {
            OnSignal -= handler;
        }

        public static void Emit(string signalName, object payload)
        {
            if (string.IsNullOrEmpty(signalName))
            {
                return;
            }

            OnSignal?.Invoke(signalName, payload);
        }
    }

    public class InteractPayload
    {
        public string targetId;
        public GameObject target;
        public Vector3 hitPoint;
        public string hand;
        public string source;
        public string zoneId;

        public InteractPayload(string targetId, GameObject target, Vector3 hitPoint, string hand, string source, string zoneId)
        {
            this.targetId = targetId;
            this.target = target;
            this.hitPoint = hitPoint;
            this.hand = hand;
            this.source = source;
            this.zoneId = zoneId;
        }
    }

    public void EmitPoint(string targetId, GameObject target, Vector3 hitPoint, string hand, string source)
    {
        Emit("Input.Point", new InteractPayload(targetId, target, hitPoint, hand, source, string.Empty));
    }

    public void EmitClick(string targetId, GameObject target, Vector3 hitPoint, string hand, string source)
    {
        Emit("Input.Click", new InteractPayload(targetId, target, hitPoint, hand, source, string.Empty));
    }

    public void EmitUIClick(string targetId, GameObject target, string hand, string source)
    {
        Emit("UI.Click", new InteractPayload(targetId, target, Vector3.zero, hand, source, string.Empty));
    }

    public void EmitZoneEnter(string zoneId, string hand, string source)
    {
        Emit("Zone.Enter", new InteractPayload(string.Empty, null, Vector3.zero, hand, source, zoneId));
    }

    public void EmitZoneExit(string zoneId, string hand, string source)
    {
        Emit("Zone.Exit", new InteractPayload(string.Empty, null, Vector3.zero, hand, source, zoneId));
    }

    private void Emit(string signalName, InteractPayload payload)
    {
        SignalBus.Emit(signalName, payload);
    }
}
