using ContractsServer.Data;
using System.Collections.Generic;
using UnityEngine;
using FMVector3 = FM.Mathematics.DataTypes.Vector3;

namespace Domain.Game.Core.Entity
{
    public static class NavigationAI
    {
        private static float _curveStep = GameConstants.CurveStep;

        public static bool TryBuildCurve(FMVector3[] controls, out Vector3[] curve)
        {
            curve = null;

            // step у Bezier — доля всей кривой. Делим на число отрезков, чтобы плотность на каждом углу была как у FlatMap.
            float localStep = Mathf.Clamp(_curveStep, 0.01f, 1f);
            float step = localStep / (controls.Length - 1);
            if (!FM.Mathematics.BezierCurve.BezierCurve.TryGenerationThroughPoints(step, out List<FMVector3> generated, controls)
                || generated == null
                || generated.Count < 2)
                return false;

            curve = new Vector3[generated.Count];
            for (int i = 0; i < generated.Count; i++)
            {
                FMVector3 point = generated[i];
                curve[i] = new Vector3(point.X, point.Y, point.Z);
            }

            return true;
        }
    }
}
