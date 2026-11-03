using UnityEngine;
using ContractsServer.Service.Navigation;
using FMVector2 = FM.Mathematics.DataTypes.Vector2;

namespace Domain.Game.Core.Entity
{
    public static class NavigationAI
    {
        /// <summary>
        /// <para>{EN} One through-points curve for the whole control list. Y of a control is world Z. Step is the sample density per edge.</para>
        /// {RU} Одна through-points кривая на весь список контролов. Y контрола — мировая Z. Step — плотность сэмпла на отрезке.
        /// </summary>
        public static bool TryBuildCurve(FMVector2[] controls, float step, out Vector2[] curve)
        {
            curve = null;
            if (!RouteCurve.TryBuild(controls, step, out FMVector2[] built))
                return false;

            curve = new Vector2[built.Length];
            for (int i = 0; i < built.Length; i++)
                curve[i] = new Vector2(built[i].X, built[i].Y);

            return true;
        }
    }
}
