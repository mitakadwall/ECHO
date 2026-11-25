using System.Collections.Generic;
using UnityEngine;

namespace Eco.Enemies.Perception
{
    /// <summary>
    /// Implemented by enemies that can react to alarms triggered by allied units.
    /// </summary>
    public interface IAlertReceiver
    {
        Vector3 Position { get; }
        bool IsAlertedOrDead { get; }
        void OnExternalAlert(Vector3 targetPosition, GameObject alertSource);
    }

    /// <summary>
    /// Lightweight registry that broadcasts enemy alarms within a radius without runtime FindObjects calls.
    /// </summary>
    public static class EnemyAlertSystem
    {
        private static readonly HashSet<IAlertReceiver> Receivers = new HashSet<IAlertReceiver>();

        public static void Register(IAlertReceiver receiver)
        {
            if (receiver != null)
            {
                Receivers.Add(receiver);
            }
        }

        public static void Unregister(IAlertReceiver receiver)
        {
            if (receiver != null)
            {
                Receivers.Remove(receiver);
            }
        }

        public static int BroadcastAlert(Vector3 origin, float radius, Vector3 targetPosition, GameObject source)
        {
            if (radius <= 0f || Receivers.Count == 0) return 0;

            float radiusSqr = radius * radius;
            int alertedCount = 0;

            // Snapshot to avoid modification issues if a receiver changes registration during alert.
            List<IAlertReceiver> snapshot = new List<IAlertReceiver>(Receivers);
            foreach (IAlertReceiver receiver in snapshot)
            {
                if (receiver == null || receiver.IsAlertedOrDead) continue;
                if (receiver is Component comp && comp.gameObject == source) continue;

                if ((receiver.Position - origin).sqrMagnitude <= radiusSqr)
                {
                    receiver.OnExternalAlert(targetPosition, source);
                    alertedCount++;
                }
            }

            return alertedCount;
        }
    }
}
