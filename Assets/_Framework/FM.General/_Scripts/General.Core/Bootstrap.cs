using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using FM.General.Core.Interfaces.Contracts;

namespace FM.General.Core
{
    public static class Bootstrap
    {
        public static event Action OnAppboot;
        private readonly static List<IBootstrap> _bootstrap = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnRuntimeStart()
        {
            OnLoadAssemble(out var assemblies);
            InvokeBeginIBootstrap(assemblies);
            OnAppboot?.Invoke();
        }

        #region Load Assemble
        private static void OnLoadAssemble(out Assembly[] assemblies)
        {
            assemblies = Array.Empty<Assembly>();
            try
            {
                assemblies = AppDomain.CurrentDomain.GetAssemblies();
                FindAllIBootstrap(assemblies);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Bootstrap] OnLoadAssemble: {ex.Message}");
            }
        }

        private static void FindAllIBootstrap(Assembly[] assemblies)
        {
            var interfaceType = typeof(IBootstrap);

            foreach (var asm in assemblies)
            {
                try
                {
                    foreach (var type in asm.GetTypes())
                    {
                        if (type.IsClass && !type.IsAbstract && interfaceType.IsAssignableFrom(type))
                        {
                            var instance = (IBootstrap)Activator.CreateInstance(type);
                            _bootstrap.Add(instance);
                        }
                    }
                    Debug.Log("Load Succes");
                }
                catch (ReflectionTypeLoadException)
                {
                    Debug.LogWarning("Load Failed");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Bootstrap] FindAllIBootstrap {asm.GetName().Name}: {ex.Message}");
                }
            }
        }
        #endregion

        #region Invoke Begin Action
        private static void InvokeBeginIBootstrap(Assembly[] assemblies)
        {
            // DiBootstrap первым — остальные могут зависеть от DI
            var ordered = _bootstrap.OrderBy(b => b.GetType().Name == "DiBootstrap" ? 0 : 1).ToList();
            foreach (var bootstrap in ordered)
            {
                try
                {
                    bootstrap.Initialize(assemblies);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Bootstrap] Initialize failed: {ex.Message}");
                }
            }
        }
        #endregion

        public static void AddListener(Action listener)
        {
            OnAppboot += listener;
        }

        public static void RemoveListener(Action listener)
        {
            OnAppboot -= listener;
        }
    }
}
