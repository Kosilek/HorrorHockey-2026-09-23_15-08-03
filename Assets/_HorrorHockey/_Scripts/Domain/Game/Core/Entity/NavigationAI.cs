using ContractsServer.Data;
using System.Collections.Generic;
using UnityEngine;
using FMVector2 = FM.Mathematics.DataTypes.Vector2;
using FMVector3 = FM.Mathematics.DataTypes.Vector3;

namespace Domain.Game.Core.Entity
{
    public static class NavigationAI
    {
        private static float _curveStep = GameConstants.CurveStep;

        public static bool TryBuildCurve(FMVector2[] controls, out Vector2[] curve)
        {
            curve = null;
            if (controls == null || controls.Length < 2)
                return false;

            // Кривая живёт в плоскости XZ. Y контрола — это мировая Z.
            var lifted = new FMVector3[controls.Length];
            for (int i = 0; i < controls.Length; i++)
                lifted[i] = new FMVector3(controls[i].X, 0f, controls[i].Y);

            // step у Bezier — доля всей кривой. Делим на число отрезков, чтобы плотность на каждом углу была как у FlatMap.
            float localStep = Mathf.Clamp(_curveStep, 0.01f, 1f);
            float step = localStep / (controls.Length - 1);
            if (!FM.Mathematics.BezierCurve.BezierCurve.TryGenerationThroughPoints(step, out List<FMVector3> generated, lifted)
                || generated == null
                || generated.Count < 2)
                return false;

            curve = new Vector2[generated.Count];
            for (int i = 0; i < generated.Count; i++)
            {
                FMVector3 point = generated[i];
                curve[i] = new Vector2(point.X, point.Z);
            }

            return true;
        }
    }
}
