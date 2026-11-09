using ContractsServer.Data.Navigation;
using System.Collections.Generic;
using FMVector2 = FM.Mathematics.DataTypes.Vector2;
using V2 = System.Numerics.Vector2;

namespace ContractsServer.Service.Navigation
{
    /// <summary>
    /// <para>{EN} Adds bypass controls around obstacles between route waypoints. Port of the client NavigationMap test. Not thread safe.</para>
    /// {RU} Добавляет точки обхода препятствий между вейпоинтами маршрута. Перенос клиентского теста NavigationMap. Не потокобезопасен.
    /// </summary>
    public sealed class RouteBypass
    {
        private const float GrazeDistance = 0.05f;
        private const float GrazeDistanceSqr = GrazeDistance * GrazeDistance;
        private const float BoundsPad = 0.01f;
        private const int MaxCurvePasses = 4;

        private readonly Contour[] _contours;
        private readonly float _curveStep;
        private readonly int _maxIterations;

        private readonly List<SegmentHit> _hit = new();
        private readonly List<ObjectCenter> _objectCenters = new();
        private readonly List<V2> _route = new();
        private readonly List<V2> _nextPoints = new();
        private readonly List<V2> _points = new();
        private readonly List<V2> _controls = new();
        private readonly List<V2> _refined = new();
        private readonly List<bool> _blocked = new();

        /// <param name="clearance">{EN} Gap kept between the route and walls. {RU} Зазор между маршрутом и стенами.</param>
        /// <param name="curveStep">{EN} Same step the unit passes to RouteCurve. {RU} Тот же шаг, что юнит передаёт в RouteCurve.</param>
        public RouteBypass(IReadOnlyList<NavigationObstacle> obstacles, float clearance, float curveStep, int maxIterations = 8)
        {
            _curveStep = curveStep;
            _maxIterations = maxIterations < 1 ? 1 : maxIterations;

            var contours = new List<Contour>();
            if (obstacles != null)
            {
                for (int i = 0; i < obstacles.Count; i++)
                {
                    FMVector2[] source = obstacles[i]?.Vertices;
                    if (source == null || source.Length < 3)
                        continue;

                    contours.Add(new Contour(obstacles[i].Name, source, clearance));
                }
            }

            _contours = contours.ToArray();
            LinkHoles(_contours, clearance);
        }

        public bool HasObstacles => _contours.Length > 0;

        /// <summary>
        /// <para>{EN} False if the point lies inside an obstacle inflated by the clearance. A loop inside another loop of the same object is a hole,
        /// e.g. the field inside the fence, so only the ring between them blocks. Reads only immutable contours, so it is thread safe.</para>
        /// {RU} false, если точка внутри препятствия, раздутого на зазор. Петля внутри другой петли того же объекта — дырка,
        /// например поле внутри забора, поэтому блокирует только кольцо между ними. Читает только неизменяемые контуры, поэтому потокобезопасен.
        /// </summary>
        public bool IsWalkable(FMVector2 point)
        {
            V2 p = ToV2(point);
            for (int i = 0; i < _contours.Length; i++)
            {
                Contour contour = _contours[i];
                if (contour.IsHole || !InBounds(p, contour.Min, contour.Max) || !ContainsPoint(contour.Vertices, p))
                    continue;
                if (!InsideAnyHole(contour, p))
                    return false;
            }

            return true;
        }

        private bool InsideAnyHole(Contour contour, V2 point)
        {
            int[] holes = contour.Holes;
            for (int h = 0; h < holes.Length; h++)
            {
                Contour hole = _contours[holes[h]];
                if (InBounds(point, hole.WalkMin, hole.WalkMax) && ContainsPoint(hole.WalkVertices, point))
                    return true;
            }

            return false;
        }

        private static bool InBounds(V2 point, V2 min, V2 max)
        {
            return point.X >= min.X && point.X <= max.X && point.Y >= min.Y && point.Y <= max.Y;
        }

        private static void LinkHoles(Contour[] contours, float clearance)
        {
            var holes = new List<int>();
            for (int i = 0; i < contours.Length; i++)
            {
                Contour outer = contours[i];
                holes.Clear();
                for (int j = 0; j < contours.Length; j++)
                {
                    Contour inner = contours[j];
                    if (i == j || inner.Name != outer.Name || inner.Raw.Length == 0)
                        continue;
                    if (ContainsPoint(outer.Raw, inner.Raw[0]))
                        holes.Add(j);
                }

                if (holes.Count > 0)
                    outer.Holes = holes.ToArray();
            }

            for (int i = 0; i < contours.Length; i++)
            {
                int[] linked = contours[i].Holes;
                for (int h = 0; h < linked.Length; h++)
                    contours[linked[h]].MarkHole(clearance);
            }
        }

        /// <summary>
        /// <para>{EN} Waypoints with bypass controls in between. Waypoints themselves are kept.</para>
        /// {RU} Вейпоинты с точками обхода между ними. Сами вейпоинты сохраняются.
        /// </summary>
        public FMVector2[] Build(FMVector2[] waypoints)
        {
            if (waypoints == null)
                return null;
            if (waypoints.Length < 2 || _contours.Length == 0)
                return (FMVector2[])waypoints.Clone();

            _controls.Clear();
            AddControl(ToV2(waypoints[0]));
            for (int i = 1; i < waypoints.Length; i++)
            {
                V2 start = ToV2(waypoints[i - 1]);
                V2 end = ToV2(waypoints[i]);
                BuildSegment(start, end);
                for (int p = 0; p < _points.Count; p++)
                    AddControl(_points[p]);
                AddControl(end);
            }

            KeepCurveClear();

            var result = new FMVector2[_controls.Count];
            for (int i = 0; i < _controls.Count; i++)
                result[i] = new FMVector2(_controls[i].X, _controls[i].Y);
            return result;
        }

        private void AddControl(V2 point)
        {
            if (_controls.Count > 0 && (point - _controls[_controls.Count - 1]).LengthSquared() <= 1e-6f)
                return;

            _controls.Add(point);
        }

        #region Polyline
        private void BuildSegment(V2 start, V2 end)
        {
            _points.Clear();
            CollectHits(start, end, start, end);
            SortHitsByT();
            AppendBypasses(start, end, _points, start, end);

            for (int iteration = 1; iteration < _maxIterations; iteration++)
            {
                if (!RefinePath(start, end))
                    return;
            }
        }

        private void CollectHits(V2 start, V2 end, V2 allowedStart, V2 allowedEnd)
        {
            _hit.Clear();
            V2 direction = end - start;
            if (direction.LengthSquared() == 0f)
                return;

            for (int o = 0; o < _contours.Length; o++)
            {
                Contour contour = _contours[o];
                if (ContainsRoute(contour.Vertices, allowedStart, allowedEnd))
                    continue;
                if (!OverlapsAabb(start, end, contour.Min, contour.Max))
                    continue;

                V2[] vertices = contour.Vertices;
                int length = vertices.Length;
                for (int i = 0; i < length; i++)
                {
                    int next = i + 1;
                    if (next == length)
                        next = 0;

                    if (TryHitPoint(start, direction, vertices[i], vertices[next], out V2 point, out float t))
                        _hit.Add(new SegmentHit { Object = o, Point = point, T = t, Edge = i });
                }
            }
        }

        private bool RefinePath(V2 globalStart, V2 globalEnd)
        {
            _route.Clear();
            _route.Add(globalStart);
            for (int i = 0; i < _points.Count; i++)
                _route.Add(_points[i]);
            _route.Add(globalEnd);

            _nextPoints.Clear();
            bool changed = false;
            int last = _route.Count - 1;
            for (int i = 0; i < last; i++)
            {
                V2 segmentStart = _route[i];
                V2 segmentEnd = _route[i + 1];
                if ((segmentEnd - segmentStart).LengthSquared() <= GrazeDistanceSqr)
                {
                    if (i + 1 < last)
                        changed = true;
                    continue;
                }

                CollectHits(segmentStart, segmentEnd, globalStart, globalEnd);
                SortHitsByT();
                int before = _nextPoints.Count;
                AppendBypasses(segmentStart, segmentEnd, _nextPoints, globalStart, globalEnd);
                if (_nextPoints.Count > before)
                    changed = true;

                if (i + 1 >= last)
                    continue;

                if (IsStrictlyInside(segmentEnd, globalStart, globalEnd))
                    changed = true;
                else
                    _nextPoints.Add(segmentEnd);
            }

            if (!changed)
                return false;

            _points.Clear();
            _points.AddRange(_nextPoints);
            return true;
        }

        private void AppendBypasses(V2 start, V2 end, List<V2> target, V2 globalStart, V2 globalEnd)
        {
            _objectCenters.Clear();
            V2 direction = end - start;
            float directionSqr = direction.LengthSquared();
            int count = _hit.Count;
            for (int i = 0; i < count; i++)
            {
                SegmentHit enter = _hit[i];
                if (enter.Used)
                    continue;

                int exitEdge = enter.Edge;
                V2 exitPoint = enter.Point;
                int hits = 1;
                enter.Used = true;
                _hit[i] = enter;

                for (int j = i + 1; j < count; j++)
                {
                    SegmentHit candidate = _hit[j];
                    if (candidate.Used || candidate.Object != enter.Object)
                        continue;

                    exitEdge = candidate.Edge;
                    exitPoint = candidate.Point;
                    hits++;
                    candidate.Used = true;
                    _hit[j] = candidate;
                }

                V2[] vertices = _contours[enter.Object].Vertices;
                if (IsGraze(vertices, start, end, enter.Point, exitPoint, hits, globalStart, globalEnd))
                    continue;

                V2 bypass = FindBypassPoint(vertices, enter.Edge, enter.Point, exitEdge, exitPoint, start, end);
                if (IsNear(bypass, start) || IsNear(bypass, end))
                    continue;

                float along = directionSqr > 0f ? V2.Dot(bypass - start, direction) / directionSqr : 0f;
                _objectCenters.Add(new ObjectCenter { Point = bypass, T = along });
            }

            SortCentersByT();
            for (int i = 0; i < _objectCenters.Count; i++)
            {
                V2 point = _objectCenters[i].Point;
                if (target.Count > 0 && IsNear(point, target[target.Count - 1]))
                    continue;

                target.Add(point);
            }
        }

        private V2 FindBypassPoint(V2[] vertices, int enterEdge, V2 enterPoint, int exitEdge, V2 exitPoint, V2 start, V2 end)
        {
            int count = vertices.Length;
            if (count == 0)
                return enterPoint;

            V2 direction = end - start;
            if (enterEdge == exitEdge || direction.LengthSquared() == 0f)
                return FartherEdgeVertex(vertices, enterEdge, start, direction);

            int forwardFrom = (enterEdge + 1) % count;
            int forwardTo = exitEdge;
            float forwardLength = ChainLength(vertices, enterPoint, forwardFrom, forwardTo, exitPoint, 1);

            int backwardFrom = enterEdge;
            int backwardTo = (exitEdge + 1) % count;
            float backwardLength = ChainLength(vertices, enterPoint, backwardFrom, backwardTo, exitPoint, -1);

            int from;
            int to;
            int step;
            if (backwardLength < forwardLength)
            {
                from = backwardFrom;
                to = backwardTo;
                step = -1;
            }
            else
            {
                from = forwardFrom;
                to = forwardTo;
                step = 1;
            }

            V2 inside = (enterPoint + exitPoint) * .5f;
            if (TryTangentBypass(vertices, from, to, step, start, end, inside, direction, out V2 bypass))
                return bypass;

            return FarthestOnChain(vertices, from, to, step, start, direction);
        }

        private static bool TryTangentBypass(V2[] vertices, int from, int to, int step, V2 start, V2 end, V2 inside, V2 direction, out V2 bypass)
        {
            bypass = default;
            float side = ChainSide(vertices, from, to, step, start, direction);
            if (side == 0f)
                return false;

            V2 perp = new V2(-direction.Y, direction.X);
            V2 outward = side > 0f ? perp : -perp;
            if (!TryOuterTangent(vertices, from, to, step, start, inside, outward, out V2 startTangent))
                return false;
            if (!TryOuterTangent(vertices, from, to, step, end, inside, outward, out V2 endTangent))
                return false;

            V2 startDir = startTangent - start;
            V2 endDir = endTangent - end;
            if (startDir.LengthSquared() == 0f || endDir.LengthSquared() == 0f)
                return false;

            float denom = Cross(startDir, endDir);
            if (denom == 0f)
                return false;

            V2 diff = end - start;
            float t = Cross(diff, endDir) / denom;
            float u = Cross(diff, startDir) / denom;
            if (t < 1f - 1e-3f || u < 1f - 1e-3f)
                return false;

            V2 point = start + startDir * t;
            if (Cross(direction, point - start) * side <= 0f)
                return false;

            bypass = point;
            return true;
        }

        private static bool TryOuterTangent(V2[] vertices, int from, int to, int step, V2 origin, V2 inside, V2 outward, out V2 vertex)
        {
            vertex = default;
            V2 toInside = inside - origin;
            if (toInside.LengthSquared() == 0f)
                return false;

            float outsideSign = Cross(toInside, outward);
            if (outsideSign == 0f)
                return false;

            float sign = outsideSign > 0f ? 1f : -1f;
            int count = vertices.Length;
            int index = from;
            vertex = vertices[index];
            float bestScore = TangentScore(toInside, vertex - origin, sign);
            int guard = 0;
            while (index != to && guard < count)
            {
                index = StepIndex(index, step, count);
                guard++;
                V2 candidate = vertices[index];
                float score = TangentScore(toInside, candidate - origin, sign);
                if (score > bestScore)
                {
                    bestScore = score;
                    vertex = candidate;
                }
            }

            return true;
        }

        private static float TangentScore(V2 toInside, V2 toVertex, float sign)
        {
            return System.MathF.Atan2(Cross(toInside, toVertex), V2.Dot(toInside, toVertex)) * sign;
        }

        private static float ChainLength(V2[] vertices, V2 fromHit, int from, int to, V2 toHit, int step)
        {
            int count = vertices.Length;
            float length = (vertices[from] - fromHit).Length();
            int index = from;
            int guard = 0;
            while (index != to && guard < count)
            {
                int next = StepIndex(index, step, count);
                length += (vertices[next] - vertices[index]).Length();
                index = next;
                guard++;
            }

            length += (toHit - vertices[to]).Length();
            return length;
        }

        private static float ChainSide(V2[] vertices, int from, int to, int step, V2 start, V2 direction)
        {
            int count = vertices.Length;
            int index = from;
            float bestAbs = 0f;
            float side = 0f;
            int guard = 0;
            while (true)
            {
                float cross = Cross(direction, vertices[index] - start);
                float abs = cross >= 0f ? cross : -cross;
                if (abs > bestAbs)
                {
                    bestAbs = abs;
                    side = cross;
                }

                if (index == to || guard >= count)
                    break;

                index = StepIndex(index, step, count);
                guard++;
            }

            return side;
        }

        private static V2 FarthestOnChain(V2[] vertices, int from, int to, int step, V2 start, V2 direction)
        {
            int count = vertices.Length;
            int index = from;
            V2 farthest = vertices[index];
            float best = OffsetSqr(farthest, start, direction);
            int guard = 0;
            while (index != to && guard < count)
            {
                index = StepIndex(index, step, count);
                guard++;
                V2 candidate = vertices[index];
                float offset = OffsetSqr(candidate, start, direction);
                if (offset > best)
                {
                    best = offset;
                    farthest = candidate;
                }
            }

            return farthest;
        }

        private static V2 FartherEdgeVertex(V2[] vertices, int edge, V2 start, V2 direction)
        {
            V2 a = vertices[edge];
            V2 b = vertices[(edge + 1) % vertices.Length];
            return OffsetSqr(a, start, direction) >= OffsetSqr(b, start, direction) ? a : b;
        }

        private static float OffsetSqr(V2 point, V2 start, V2 direction)
        {
            float cross = Cross(direction, point - start);
            return cross * cross;
        }

        private bool IsGraze(V2[] vertices, V2 segmentStart, V2 segmentEnd, V2 enterPoint, V2 exitPoint, int hits, V2 globalStart, V2 globalEnd)
        {
            if ((exitPoint - enterPoint).LengthSquared() > GrazeDistanceSqr)
                return false;

            if (hits > 1)
                return true;

            bool startInside = IsStrictlyInsideContour(vertices, segmentStart);
            bool endInside = IsStrictlyInsideContour(vertices, segmentEnd);
            if (!startInside && !endInside)
                return true;
            if (startInside && IsNear(segmentStart, globalStart))
                return true;
            if (endInside && IsNear(segmentEnd, globalEnd))
                return true;

            return false;
        }

        private bool IsStrictlyInside(V2 point, V2 allowedStart, V2 allowedEnd)
        {
            for (int i = 0; i < _contours.Length; i++)
            {
                V2[] vertices = _contours[i].Vertices;
                if (ContainsRoute(vertices, allowedStart, allowedEnd))
                    continue;
                if (IsStrictlyInsideContour(vertices, point))
                    return true;
            }

            return false;
        }
        #endregion

        #region Curve
        /// <summary>
        /// <para>{EN} The through-points curve can cut a corner the polyline went around. Each blocked span gets its midpoint as an extra control.</para>
        /// {RU} Through-points кривая может срезать угол, который ломаная обошла. В каждый заблокированный отрезок добавляется его середина.
        /// </summary>
        private void KeepCurveClear()
        {
            for (int pass = 0; pass < MaxCurvePasses; pass++)
            {
                if (_controls.Count < 3)
                    return;

                var controls = new FMVector2[_controls.Count];
                for (int i = 0; i < controls.Length; i++)
                    controls[i] = new FMVector2(_controls[i].X, _controls[i].Y);

                if (!RouteCurve.TryBuild(controls, _curveStep, out FMVector2[] curve))
                    return;

                V2 routeStart = _controls[0];
                V2 routeEnd = _controls[_controls.Count - 1];
                _blocked.Clear();
                for (int i = 0; i < _controls.Count - 1; i++)
                    _blocked.Add(false);

                bool any = false;
                for (int i = 1; i < curve.Length; i++)
                {
                    V2 a = ToV2(curve[i - 1]);
                    V2 b = ToV2(curve[i]);
                    if (!ChordBlocked(a, b, routeStart, routeEnd))
                        continue;

                    int span = NearestSpan((a + b) * .5f);
                    if (span < 0 || _blocked[span])
                        continue;
                    if ((_controls[span + 1] - _controls[span]).LengthSquared() <= 4f * GrazeDistanceSqr)
                        continue;

                    _blocked[span] = true;
                    any = true;
                }

                if (!any)
                    return;

                _refined.Clear();
                for (int i = 0; i < _controls.Count - 1; i++)
                {
                    _refined.Add(_controls[i]);
                    if (_blocked[i])
                        _refined.Add((_controls[i] + _controls[i + 1]) * .5f);
                }

                _refined.Add(_controls[_controls.Count - 1]);
                _controls.Clear();
                _controls.AddRange(_refined);
            }
        }

        private bool ChordBlocked(V2 a, V2 b, V2 routeStart, V2 routeEnd)
        {
            V2 direction = b - a;
            for (int o = 0; o < _contours.Length; o++)
            {
                Contour contour = _contours[o];
                V2[] raw = contour.Raw;
                if (ContainsRoute(raw, routeStart, routeEnd))
                    continue;
                if (!OverlapsAabb(a, b, contour.RawMin, contour.RawMax))
                    continue;

                if (IsStrictlyInsideContour(raw, (a + b) * .5f))
                    return true;

                if (direction.LengthSquared() == 0f)
                    continue;

                int length = raw.Length;
                for (int i = 0; i < length; i++)
                {
                    if (TryHitPoint(a, direction, raw[i], raw[(i + 1) % length], out _, out _))
                        return true;
                }
            }

            return false;
        }

        private int NearestSpan(V2 point)
        {
            int best = -1;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _controls.Count - 1; i++)
            {
                float distance = DistanceToSegmentSqr(point, _controls[i], _controls[i + 1]);
                if (distance < bestSqr)
                {
                    bestSqr = distance;
                    best = i;
                }
            }

            return best;
        }
        #endregion

        #region Geometry
        private static bool OverlapsAabb(V2 start, V2 end, V2 min, V2 max)
        {
            float t0 = 0f;
            float t1 = 1f;
            return ClipAxis(start.X, end.X - start.X, min.X, max.X, ref t0, ref t1)
                && ClipAxis(start.Y, end.Y - start.Y, min.Y, max.Y, ref t0, ref t1);
        }

        private static bool ClipAxis(float origin, float delta, float min, float max, ref float t0, ref float t1)
        {
            if (delta == 0f)
                return origin >= min && origin <= max;

            float inv = 1f / delta;
            float near = (min - origin) * inv;
            float far = (max - origin) * inv;
            if (near > far)
            {
                float swap = near;
                near = far;
                far = swap;
            }

            if (near > t0)
                t0 = near;
            if (far < t1)
                t1 = far;
            return t0 <= t1;
        }

        private static bool TryHitPoint(V2 start, V2 direction, V2 a, V2 b, out V2 point, out float t)
        {
            point = default;
            t = 0f;
            V2 edge = b - a;
            float denominator = Cross(direction, edge);
            if (denominator == 0f)
                return false;

            V2 offset = a - start;
            float along = Cross(offset, edge) / denominator;
            if (along < 0f || along > 1f)
                return false;

            float across = Cross(offset, direction) / denominator;
            if (across < 0f || across > 1f)
                return false;

            t = along;
            point = start + direction * t;
            return true;
        }

        private void SortHitsByT()
        {
            int count = _hit.Count;
            for (int i = 1; i < count; i++)
            {
                SegmentHit current = _hit[i];
                int j = i - 1;
                while (j >= 0 && _hit[j].T > current.T)
                {
                    _hit[j + 1] = _hit[j];
                    j--;
                }

                _hit[j + 1] = current;
            }
        }

        private void SortCentersByT()
        {
            int count = _objectCenters.Count;
            for (int i = 1; i < count; i++)
            {
                ObjectCenter current = _objectCenters[i];
                int j = i - 1;
                while (j >= 0 && _objectCenters[j].T > current.T)
                {
                    _objectCenters[j + 1] = _objectCenters[j];
                    j--;
                }

                _objectCenters[j + 1] = current;
            }
        }

        private static int StepIndex(int index, int step, int count)
        {
            int next = index + step;
            if (next >= count)
                return next - count;
            if (next < 0)
                return next + count;
            return next;
        }

        private static bool ContainsRoute(V2[] vertices, V2 start, V2 end)
        {
            return IsStrictlyInsideContour(vertices, start) && IsStrictlyInsideContour(vertices, end);
        }

        private static bool IsStrictlyInsideContour(V2[] vertices, V2 point)
        {
            if (vertices == null || vertices.Length < 3 || !ContainsPoint(vertices, point))
                return false;

            return DistanceToEdgesSqr(vertices, point) > GrazeDistanceSqr;
        }

        private static bool ContainsPoint(V2[] vertices, V2 point)
        {
            int count = vertices.Length;
            bool inside = false;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                V2 a = vertices[i];
                V2 b = vertices[j];
                if ((a.Y > point.Y) != (b.Y > point.Y)
                    && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static float DistanceToEdgesSqr(V2[] vertices, V2 point)
        {
            float best = float.MaxValue;
            int count = vertices.Length;
            for (int i = 0; i < count; i++)
            {
                float distance = DistanceToSegmentSqr(point, vertices[i], vertices[(i + 1) % count]);
                if (distance < best)
                    best = distance;
            }

            return best;
        }

        private static float DistanceToSegmentSqr(V2 point, V2 a, V2 b)
        {
            V2 edge = b - a;
            float lengthSqr = edge.LengthSquared();
            if (lengthSqr == 0f)
                return (point - a).LengthSquared();

            float t = V2.Dot(point - a, edge) / lengthSqr;
            if (t < 0f)
                t = 0f;
            else if (t > 1f)
                t = 1f;

            return (point - (a + edge * t)).LengthSquared();
        }

        private static bool IsNear(V2 a, V2 b)
        {
            return (a - b).LengthSquared() <= GrazeDistanceSqr;
        }

        private static float Cross(V2 a, V2 b)
        {
            return a.X * b.Y - a.Y * b.X;
        }

        private static V2 ToV2(FMVector2 point)
        {
            return new V2(point.X, point.Y);
        }
        #endregion

        private sealed class Contour
        {
            /// <summary>{EN} Contour inflated by the clearance. Routing works on it. {RU} Контур, раздутый на зазор. По нему строится обход.</summary>
            public readonly V2[] Vertices;
            public readonly V2 Min;
            public readonly V2 Max;

            /// <summary>{EN} Real wall. The final curve must stay out of it. {RU} Настоящая стена. Итоговая кривая не должна в неё заходить.</summary>
            public readonly V2[] Raw;
            public readonly V2 RawMin;
            public readonly V2 RawMax;

            public readonly string Name;

            /// <summary>{EN} Loops of the same object inside this one. {RU} Петли того же объекта внутри этой.</summary>
            public int[] Holes = System.Array.Empty<int>();

            /// <summary>{EN} This loop is a hole of another one. {RU} Эта петля — дырка другой.</summary>
            public bool IsHole { get; private set; }

            /// <summary>{EN} Walkable side of a hole, shrunk by the clearance away from the wall. {RU} Проходимая часть дырки, сжатая на зазор от стены.</summary>
            public V2[] WalkVertices { get; private set; }
            public V2 WalkMin { get; private set; }
            public V2 WalkMax { get; private set; }

            public Contour(string name, FMVector2[] source, float clearance)
            {
                Name = name ?? string.Empty;
                Raw = new V2[source.Length];
                for (int i = 0; i < source.Length; i++)
                    Raw[i] = new V2(source[i].X, source[i].Y);

                Vertices = clearance > 0f ? Inflate(Raw, clearance) : Raw;
                Bounds(Vertices, out Min, out Max);
                Bounds(Raw, out RawMin, out RawMax);
            }

            public void MarkHole(float clearance)
            {
                if (IsHole)
                    return;

                IsHole = true;
                WalkVertices = clearance > 0f ? Inflate(Raw, -clearance) : Raw;
                Bounds(WalkVertices, out V2 min, out V2 max);
                WalkMin = min;
                WalkMax = max;
            }

            /// <summary>
            /// <para>{EN} Miter offset along the vertex normal. Sharp corners are capped at twice the clearance.</para>
            /// {RU} Смещение вдоль нормали вершины. Острые углы ограничены двойным зазором.
            /// </summary>
            private static V2[] Inflate(V2[] raw, float clearance)
            {
                int count = raw.Length;
                float area = 0f;
                for (int i = 0, j = count - 1; i < count; j = i++)
                    area += Cross(raw[j], raw[i]);
                float orientation = area >= 0f ? 1f : -1f;

                var inflated = new V2[count];
                for (int i = 0; i < count; i++)
                {
                    V2 previous = EdgeNormal(raw[(i - 1 + count) % count], raw[i], orientation);
                    V2 next = EdgeNormal(raw[i], raw[(i + 1) % count], orientation);
                    V2 sum = previous + next;
                    float sumLength = sum.Length();
                    if (sumLength <= 1e-5f)
                    {
                        inflated[i] = raw[i] + next * clearance;
                        continue;
                    }

                    V2 normal = sum / sumLength;
                    float cos = V2.Dot(normal, next);
                    float scale = clearance / (cos > 0.5f ? cos : 0.5f);
                    inflated[i] = raw[i] + normal * scale;
                }

                return inflated;
            }

            private static V2 EdgeNormal(V2 a, V2 b, float orientation)
            {
                V2 edge = b - a;
                float length = edge.Length();
                if (length <= 1e-6f)
                    return V2.Zero;

                return new V2(edge.Y, -edge.X) * (orientation / length);
            }

            private static void Bounds(V2[] vertices, out V2 min, out V2 max)
            {
                V2 low = vertices[0];
                V2 high = vertices[0];
                for (int i = 1; i < vertices.Length; i++)
                {
                    low = V2.Min(low, vertices[i]);
                    high = V2.Max(high, vertices[i]);
                }

                min = low - new V2(BoundsPad);
                max = high + new V2(BoundsPad);
            }
        }

        private struct SegmentHit
        {
            public int Object;
            public V2 Point;
            public float T;
            public int Edge;
            public bool Used;
        }

        private struct ObjectCenter
        {
            public V2 Point;
            public float T;
        }
    }
}
