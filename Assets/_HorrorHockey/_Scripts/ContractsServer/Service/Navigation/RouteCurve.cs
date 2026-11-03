using System.Collections.Generic;
using FMVector2 = FM.Mathematics.DataTypes.Vector2;
using FMVector3 = FM.Mathematics.DataTypes.Vector3;

namespace ContractsServer.Service.Navigation
{
    /// <summary>
    /// <para>{EN} The only through-points curve. Client and server both call this, so the polyline matches.</para>
    /// {RU} Единственная through-points кривая. Её вызывают и клиент, и сервер, поэтому ломаная совпадает.
    /// </summary>
    public static class RouteCurve
    {
        public static bool TryBuild(FMVector2[] controls, float step, out FMVector2[] curve)
        {
            curve = null;
            if (!TryCompact(controls, out FMVector2[] compact))
                return false;

            // Кривая живёт в плоскости XZ. Y контрола — это мировая Z.
            var lifted = new FMVector3[compact.Length];
            for (int i = 0; i < compact.Length; i++)
                lifted[i] = new FMVector3(compact[i].X, 0f, compact[i].Y);

            // step у Bezier — доля всей кривой. Делим на число отрезков, чтобы плотность на каждом углу была как у FlatMap.
            float localStep = Clamp(step, 0.01f, 1f);
            float curveStep = localStep / (compact.Length - 1);
            if (!FM.Mathematics.BezierCurve.BezierCurve.TryGenerationThroughPoints(curveStep, out List<FMVector3> generated, lifted)
                || generated == null
                || generated.Count < 2)
                return false;

            curve = new FMVector2[generated.Count];
            for (int i = 0; i < generated.Count; i++)
            {
                FMVector3 point = generated[i];
                curve[i] = new FMVector2(point.X, point.Z);
            }

            return true;
        }

        private static bool TryCompact(FMVector2[] controls, out FMVector2[] compact)
        {
            compact = null;
            if (controls == null || controls.Length < 2)
                return false;

            int count = 0;
            var buffer = new FMVector2[controls.Length];
            for (int i = 0; i < controls.Length; i++)
            {
                FMVector2 point = controls[i];
                if (count > 0)
                {
                    FMVector2 last = buffer[count - 1];
                    float dx = point.X - last.X;
                    float dz = point.Y - last.Y;
                    if (dx * dx + dz * dz < 1e-8f)
                        continue;
                }

                buffer[count++] = point;
            }

            if (count < 2)
                return false;

            if (count == buffer.Length)
            {
                compact = buffer;
                return true;
            }

            compact = new FMVector2[count];
            for (int i = 0; i < count; i++)
                compact[i] = buffer[i];
            return true;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }
}
