using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FM.General.Core.Interfaces.Contracts
{
    public interface IBootstrap
    {
       public void Initialize(Assembly[] assemblies);
    }
}
