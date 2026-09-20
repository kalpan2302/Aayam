using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aayam.Core.Bootstrap
{
    /// <summary>
    /// Simple event bus for decoupled communication between systems.
    /// Components can publish and subscribe to events without direct references.
    ///
    /// Usage:
    ///   // Subscribe
    ///   eventBus.Subscribe&lt;DoorDetectedEvent&gt;(OnDoorDetected);
    ///
    ///   // Publish
    ///   eventBus.Publish(new DoorDetectedEvent { DoorId = "door_1" });
    ///
    ///   // Unsubscribe
    ///   eventBus.Unsubscribe&lt;DoorDetectedEvent&gt;(OnDoorDetected);
    /// </summary>
    public class EventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _subscribers = new();

        /// <summary>
        /// Subscribe to an event type.
        /// </summary>
        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            var type = typeof(T);
            if (!_subscribers.ContainsKey(type))
            {
                _subscribers[type] = new List<Delegate>();
            }

            _subscribers[type].Add(handler);
        }

        /// <summary>
        /// Unsubscribe from an event type.
        /// </summary>
        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            var type = typeof(T);
            if (_subscribers.TryGetValue(type, out var handlers))
            {
                handlers.Remove(handler);
                if (handlers.Count == 0)
                {
                    _subscribers.Remove(type);
                }
            }
        }

        /// <summary>
        /// Publish an event to all subscribers.
        /// </summary>
        public void Publish<T>(T eventData) where T : struct
        {
            var type = typeof(T);
            if (!_subscribers.TryGetValue(type, out var handlers))
            {
                return;
            }

            // Iterate a copy to allow subscribe/unsubscribe during publish
            var handlersCopy = new List<Delegate>(handlers);
            foreach (var handler in handlersCopy)
            {
                try
                {
                    ((Action<T>)handler).Invoke(eventData);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[EventBus] Error handling {type.Name}: {e.Message}\n{e.StackTrace}");
                }
            }
        }

        /// <summary>
        /// Remove all subscribers. Call on cleanup/teardown.
        /// </summary>
        public void Clear()
        {
            _subscribers.Clear();
        }

        /// <summary>
        /// Get subscriber count for a given event type (useful for debugging).
        /// </summary>
        public int GetSubscriberCount<T>() where T : struct
        {
            var type = typeof(T);
            return _subscribers.TryGetValue(type, out var handlers) ? handlers.Count : 0;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Event Definitions
    // Define all application events as structs here for discoverability.
    // ─────────────────────────────────────────────────────────────────

    #region AR Events

    /// <summary>Fired when the AR session state changes.</summary>
    public struct ARSessionStateChangedEvent
    {
        public string State;
        public bool IsTracking;
    }

    /// <summary>Fired when tracking is lost.</summary>
    public struct TrackingLostEvent
    {
        public float Duration;
    }

    /// <summary>Fired when tracking is recovered after being lost.</summary>
    public struct TrackingRecoveredEvent { }

    /// <summary>Fired when a new AR plane is detected.</summary>
    public struct PlaneDetectedEvent
    {
        public string PlaneId;
        public bool IsVertical;
        public Vector3 Position;
        public Vector2 Size;
    }

    /// <summary>Fired when a door candidate is identified from plane detection.</summary>
    public struct DoorCandidateDetectedEvent
    {
        public string PlaneId;
        public Vector3 Position;
        public float Width;
        public float Height;
        public float Confidence;
    }

    #endregion

    #region Product Events

    /// <summary>Fired when a product is placed in AR.</summary>
    public struct ProductPlacedEvent
    {
        public string ProductId;
        public Vector3 Position;
        public Quaternion Rotation;
    }

    /// <summary>Fired when a placed product is moved.</summary>
    public struct ProductMovedEvent
    {
        public string ProductId;
        public Vector3 NewPosition;
    }

    /// <summary>Fired when a placed product is removed.</summary>
    public struct ProductRemovedEvent
    {
        public string ProductId;
    }

    /// <summary>Fired when the user selects a product from the catalogue.</summary>
    public struct ProductSelectedEvent
    {
        public string ProductId;
        public string ProductName;
    }

    #endregion

    #region UI Events

    /// <summary>Fired when a screenshot is captured.</summary>
    public struct ScreenshotCapturedEvent
    {
        public string FilePath;
    }

    /// <summary>Fired when the user navigates to a new screen.</summary>
    public struct ScreenChangedEvent
    {
        public string PreviousScreen;
        public string CurrentScreen;
    }

    #endregion
}

