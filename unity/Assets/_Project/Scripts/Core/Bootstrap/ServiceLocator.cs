using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aayam.Core.Bootstrap
{
    /// <summary>
    /// Lightweight service locator for dependency injection.
    /// Suitable for MVP - can be replaced with a full DI container (VContainer, Zenject) later.
    ///
    /// Usage:
    ///   ServiceLocator.Register(new MyService());
    ///   var service = ServiceLocator.Get&lt;MyService&gt;();
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new();
        private static bool _isInitialized;

        /// <summary>
        /// Initialize the service locator. Must be called before any Register/Get calls.
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized)
            {
                Debug.LogWarning("[ServiceLocator] Already initialized. Clearing existing services.");
                _services.Clear();
            }

            _isInitialized = true;
            Debug.Log("[ServiceLocator] Initialized.");
        }

        /// <summary>
        /// Register a service instance by its type.
        /// </summary>
        public static void Register<T>(T service) where T : class
        {
            EnsureInitialized();

            var type = typeof(T);
            if (_services.ContainsKey(type))
            {
                Debug.LogWarning($"[ServiceLocator] Overwriting existing service: {type.Name}");
            }

            _services[type] = service;
            Debug.Log($"[ServiceLocator] Registered: {type.Name}");
        }

        /// <summary>
        /// Register a service instance by a specific interface/base type.
        /// </summary>
        public static void Register<TInterface, TImplementation>(TImplementation service)
            where TInterface : class
            where TImplementation : class, TInterface
        {
            Register<TInterface>(service);
        }

        /// <summary>
        /// Get a registered service. Throws if not found.
        /// </summary>
        public static T Get<T>() where T : class
        {
            EnsureInitialized();

            var type = typeof(T);
            if (_services.TryGetValue(type, out var service))
            {
                return (T)service;
            }

            throw new InvalidOperationException(
                $"[ServiceLocator] Service not found: {type.Name}. " +
                "Did you forget to register it in AppBootstrapper?"
            );
        }

        /// <summary>
        /// Try to get a registered service. Returns false if not found.
        /// </summary>
        public static bool TryGet<T>(out T service) where T : class
        {
            EnsureInitialized();

            var type = typeof(T);
            if (_services.TryGetValue(type, out var obj))
            {
                service = (T)obj;
                return true;
            }

            service = null;
            return false;
        }

        /// <summary>
        /// Check if a service is registered.
        /// </summary>
        public static bool IsRegistered<T>() where T : class
        {
            return _services.ContainsKey(typeof(T));
        }

        /// <summary>
        /// Unregister a service.
        /// </summary>
        public static void Unregister<T>() where T : class
        {
            var type = typeof(T);
            if (_services.Remove(type))
            {
                Debug.Log($"[ServiceLocator] Unregistered: {type.Name}");
            }
        }

        /// <summary>
        /// Clear all registered services. Called on app teardown.
        /// </summary>
        public static void Clear()
        {
            _services.Clear();
            _isInitialized = false;
            Debug.Log("[ServiceLocator] Cleared all services.");
        }

        private static void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException(
                    "[ServiceLocator] Not initialized. Call Initialize() first."
                );
            }
        }
    }
}
