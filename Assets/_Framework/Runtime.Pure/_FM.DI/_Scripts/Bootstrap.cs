using System;
using System.Reflection;
using UnityEngine;
using FM.General.Core.Interfaces.Contracts;

namespace _FM.DI
{
    public class DiBootstrap : IBootstrap
    {
        private static DIManager _instance;

        public static DIManager Instance => _instance;

        public void Initialize(Assembly[] assemblies)
        {
            var options = new DIManagerOptions { Assemblies = assemblies };
            DiscoverAndCollectRegistrations(options);
            _instance = new DIManager();
            _instance.Initialize(options);
        }

        private static void DiscoverAndCollectRegistrations(DIManagerOptions options)
        {
            var context = new RegistrationContext(options);
            var interfaceType = typeof(IRegisterInDI);

            foreach (var asm in options.Assemblies ?? Array.Empty<Assembly>())
            {
                try
                {
                    foreach (var type in asm.GetTypes())
                    {
                        if (type.IsClass && !type.IsAbstract && interfaceType.IsAssignableFrom(type))
                        {
                            var instance = (IRegisterInDI)Activator.CreateInstance(type);
                            instance.AddRegistrations(context);
                        }
                    }
                }
                catch (ReflectionTypeLoadException) { }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DiBootstrap] DiscoverRegistrations {asm.GetName().Name}: {ex.Message}");
                }
            }
        }
    }
}