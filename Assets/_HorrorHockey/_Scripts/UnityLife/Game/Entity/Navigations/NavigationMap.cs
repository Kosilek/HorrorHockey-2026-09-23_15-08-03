using _FM.DI;
using ContractsServer.Data;
using Shared.Core.Contracts.Domain;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using FMVector3 = FM.Mathematics.DataTypes.Vector3;

public class NavigationMap : MonoBehaviour
{
    [SerializeField] private Transform[] _waypoints;

    [Inject] private INavigationMapData _navigationMapData = null;
    public int InitializeOrder => 51;

    private IReadOnlyList<NavigationMapObject> _navigationMapObjects;

    private const float GrazeDistance = 0.05f;
    private const float GrazeDistanceSqr = GrazeDistance * GrazeDistance;

    private readonly List<SegmentHit> _hit = new();
    private readonly List<ObjectCenter> _objectCenters = new();
    private readonly List<Vector2> _route = new();
    private readonly List<Vector2> _nextPoints = new();
    private readonly List<FMVector3> _controls = new();
    private readonly List<Vector3> _hitPoints = new();
    private readonly List<Vector3> _bypassPoints = new();
    [SerializeField] private List<Vector2> _points = new();
    [SerializeField, Min(0.01f)] private float _bezierStep = 0.05f;
    [SerializeField, Min(1)] private int _maxIterations = 8;

    private readonly FMVector3[] _segmentControls = new FMVector3[2];
    private readonly List<Vector3> _bezierCurve = new();
    private Vector3[] _curve = System.Array.Empty<Vector3>();

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(1f);
        UnityInjectService.Instance.Inject(this);
        _navigationMapObjects = _navigationMapData.Objects;
        UnitSeparation.SetObstacles(_navigationMapObjects);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            CheckMove();
        }
    }

    public void CheckMove()
    {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        if (CountWaypoints() < 2)
        {
            Debug.LogWarning("{EN} Navigation path skipped. Need at least two waypoints.");
            _curve = System.Array.Empty<Vector3>();
            return;
        }

        if (_navigationMapObjects == null)
            Debug.LogWarning("{EN} Navigation path has no map data. Curve follows waypoints only.");
#endif

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        var clock = System.Diagnostics.Stopwatch.StartNew();
#endif
        bool capped = RebuildPath();

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        clock.Stop();
        Debug.Log("{EN} Navigation path built. Controls: " + _controls.Count
            + ", samples: " + _curve.Length
            + ", time: " + clock.Elapsed.TotalMilliseconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
            + " ms"
            + (capped ? ", cap reached." : "."));
#endif
    }

    private bool RebuildPath()
    {
        _controls.Clear();
        _hitPoints.Clear();
        _bypassPoints.Clear();
        _curve = System.Array.Empty<Vector3>();
        if (_waypoints == null)
            return false;

        bool capped = false;
        Transform previous = null;
        for (int i = 0; i < _waypoints.Length; i++)
        {
            Transform current = _waypoints[i];
            if (current == null)
                continue;

            if (previous != null && _navigationMapObjects != null)
            {
                Vector3 from = previous.position;
                Vector3 to = current.position;
                BuildPath(new Vector2(from.x, from.z), new Vector2(to.x, to.z), out bool segmentCapped);
                if (segmentCapped)
                    capped = true;

                for (int h = 0; h < _hit.Count; h++)
                    _hitPoints.Add(new Vector3(_hit[h].Point.x, 0f, _hit[h].Point.y));

                for (int p = 0; p < _points.Count; p++)
                {
                    Vector2 point = _points[p];
                    Vector3 bypass = new Vector3(point.x, LerpHeight(from, to, point), point.y);
                    _bypassPoints.Add(bypass);
                    AddControl(bypass);
                }
            }

            AddControl(current.position);
            previous = current;
        }

        if (_controls.Count < 2)
            return capped;

        RebuildCurve();
        return capped;
    }

    private void RebuildCurve()
    {
        _bezierCurve.Clear();
        _curve = System.Array.Empty<Vector3>();
        int segments = _controls.Count - 1;
        if (segments <= 0 || _bezierStep <= 0f)
            return;

        for (int i = 0; i < segments; i++)
        {
            _segmentControls[0] = _controls[i];
            _segmentControls[1] = _controls[i + 1];
            if (FM.Mathematics.BezierCurve.BezierCurve.TryGenerationThroughPoints(
                    _bezierStep,
                    out List<FMVector3> generated,
                    _segmentControls)
                && generated != null
                && generated.Count > 0)
            {
                int begin = i == 0 ? 0 : 1;
                for (int p = begin; p < generated.Count; p++)
                {
                    FMVector3 point = generated[p];
                    _bezierCurve.Add(new Vector3(point.X, point.Y, point.Z));
                }
            }
            else
            {
                if (i == 0)
                    _bezierCurve.Add(ToVector3(_controls[i]));
                _bezierCurve.Add(ToVector3(_controls[i + 1]));
            }
        }

        if (_bezierCurve.Count > 1)
            _curve = _bezierCurve.ToArray();
    }

    private static Vector3 ToVector3(FMVector3 point)
    {
        return new Vector3(point.X, point.Y, point.Z);
    }

    private int CountWaypoints()
    {
        if (_waypoints == null)
            return 0;

        int count = 0;
        for (int i = 0; i < _waypoints.Length; i++)
        {
            if (_waypoints[i] != null)
                count++;
        }

        return count;
    }

    private void AddControl(Vector3 point)
    {
        if (_controls.Count > 0)
        {
            FMVector3 last = _controls[_controls.Count - 1];
            float dx = point.x - last.X;
            float dy = point.y - last.Y;
            float dz = point.z - last.Z;
            if (dx * dx + dy * dy + dz * dz <= 1e-6f)
                return;
        }

        _controls.Add(new FMVector3(point.x, point.y, point.z));
    }

    private static float LerpHeight(Vector3 from, Vector3 to, Vector2 point)
    {
        Vector2 edge = new Vector2(to.x - from.x, to.z - from.z);
        float lengthSqr = edge.sqrMagnitude;
        float t = lengthSqr <= 1e-8f
            ? 0f
            : Mathf.Clamp01(Vector2.Dot(new Vector2(point.x - from.x, point.y - from.z), edge) / lengthSqr);
        return Mathf.Lerp(from.y, to.y, t);
    }

    private int BuildPath(Vector2 start, Vector2 end, out bool capped)
    {
        _points.Clear();
        CollectHits(start, end, start, end);
        SortHitsByT();
        AppendBypasses(start, end, _points, start, end);

        int iterations = 1;
        capped = false;
        while (iterations < _maxIterations)
        {
            bool changed = RefinePath(start, end);
            iterations++;
            if (!changed)
                return iterations;
        }

        capped = true;
        return iterations;
    }

    private void CollectHits(Vector2 start, Vector2 end, Vector2 allowedStart, Vector2 allowedEnd)
    {
        _hit.Clear();
        Vector2 direction = end - start;
        if (direction.sqrMagnitude == 0f)
            return;

        int objectCount = _navigationMapObjects.Count;
        for (int o = 0; o < objectCount; o++)
        {
            NavigationMapObject obj = _navigationMapObjects[o];
            if (ContainsRoute(obj.Vertices, allowedStart, allowedEnd))
                continue;
            if (!OverlapsAabb(start, end, obj.Min, obj.Max))
                continue;

            Vector2[] vertices = obj.Vertices;
            if (vertices == null)
                continue;

            int length = vertices.Length;
            for (int i = 0; i < length; i++)
            {
                int next = i + 1;
                if (next == length)
                    next = 0;

                if (TryHitPoint(start, direction, vertices[i], vertices[next], out Vector2 point, out float t))
                    _hit.Add(new SegmentHit { Object = obj, Point = point, T = t, Edge = i });
            }
        }
    }

    private static bool OverlapsAabb(Vector2 start, Vector2 end, Vector2 min, Vector2 max)
    {
        float t0 = 0f;
        float t1 = 1f;
        return ClipAxis(start.x, end.x - start.x, min.x, max.x, ref t0, ref t1)
            && ClipAxis(start.y, end.y - start.y, min.y, max.y, ref t0, ref t1);
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

    private bool RefinePath(Vector2 globalStart, Vector2 globalEnd)
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
            Vector2 segmentStart = _route[i];
            Vector2 segmentEnd = _route[i + 1];
            if ((segmentEnd - segmentStart).sqrMagnitude <= GrazeDistanceSqr)
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
        for (int i = 0; i < _nextPoints.Count; i++)
            _points.Add(_nextPoints[i]);
        return true;
    }

    private bool TryHitPoint(Vector2 start, Vector2 direction, Vector2 a, Vector2 b, out Vector2 point, out float t)
    {
        point = default;
        t = 0f;
        Vector2 edge = b - a;
        float denominator = direction.x * edge.y - direction.y * edge.x;
        if (denominator == 0f)
            return false;

        Vector2 offset = a - start;
        float along = (offset.x * edge.y - offset.y * edge.x) / denominator;
        if (along < 0f || along > 1f)
            return false;

        float across = (offset.x * direction.y - offset.y * direction.x) / denominator;
        if (across < 0f || across > 1f)
            return false;

        t = along;
        point = start + t * direction;
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

    private void AppendBypasses(Vector2 start, Vector2 end, List<Vector2> target, Vector2 globalStart, Vector2 globalEnd)
    {
        _objectCenters.Clear();
        Vector2 direction = end - start;
        float directionSqr = direction.sqrMagnitude;
        int count = _hit.Count;
        for (int i = 0; i < count; i++)
        {
            SegmentHit enter = _hit[i];
            if (enter.Used)
                continue;

            int enterEdge = enter.Edge;
            Vector2 enterPoint = enter.Point;
            int exitEdge = enter.Edge;
            Vector2 exitPoint = enter.Point;
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

            if (IsGraze(enter.Object.Vertices, start, end, enterPoint, exitPoint, hits, globalStart, globalEnd))
                continue;

            Vector2 bypass = FindBypassPoint(
                enter.Object.Vertices,
                enterEdge,
                enterPoint,
                exitEdge,
                exitPoint,
                start,
                end);
            if (IsNear(bypass, start) || IsNear(bypass, end))
                continue;

            float along = directionSqr > 0f ? Vector2.Dot(bypass - start, direction) / directionSqr : 0f;
            _objectCenters.Add(new ObjectCenter
            {
                Point = bypass,
                T = along
            });
        }

        SortCentersByT();
        for (int i = 0; i < _objectCenters.Count; i++)
        {
            Vector2 point = _objectCenters[i].Point;
            if (target.Count > 0 && IsNear(point, target[target.Count - 1]))
                continue;

            target.Add(point);
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

    private float Vector2Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    private Vector2 FindBypassPoint(
        Vector2[] vertices,
        int enterEdge,
        Vector2 enterPoint,
        int exitEdge,
        Vector2 exitPoint,
        Vector2 start,
        Vector2 end)
    {
        int count = vertices.Length;
        if (count == 0)
            return enterPoint;

        Vector2 direction = end - start;
        if (enterEdge == exitEdge || direction.sqrMagnitude == 0f)
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

        Vector2 inside = (enterPoint + exitPoint) * .5f;
        if (TryTangentBypass(vertices, from, to, step, start, end, inside, direction, out Vector2 bypass))
            return bypass;

        return FarthestOnChain(vertices, from, to, step, start, direction);
    }

    private bool TryTangentBypass(
        Vector2[] vertices,
        int from,
        int to,
        int step,
        Vector2 start,
        Vector2 end,
        Vector2 inside,
        Vector2 direction,
        out Vector2 bypass)
    {
        bypass = default;
        float side = ChainSide(vertices, from, to, step, start, direction);
        if (side == 0f)
            return false;

        Vector2 perp = new Vector2(-direction.y, direction.x);
        Vector2 outward = side > 0f ? perp : -perp;
        if (!TryOuterTangent(vertices, from, to, step, start, inside, outward, out Vector2 startTangent))
            return false;
        if (!TryOuterTangent(vertices, from, to, step, end, inside, outward, out Vector2 endTangent))
            return false;

        Vector2 startDir = startTangent - start;
        Vector2 endDir = endTangent - end;
        if (startDir.sqrMagnitude == 0f || endDir.sqrMagnitude == 0f)
            return false;

        float denom = startDir.x * endDir.y - startDir.y * endDir.x;
        if (denom == 0f)
            return false;

        Vector2 diff = end - start;
        float t = (diff.x * endDir.y - diff.y * endDir.x) / denom;
        float u = (diff.x * startDir.y - diff.y * startDir.x) / denom;
        if (t < 1f - 1e-3f || u < 1f - 1e-3f)
            return false;

        Vector2 point = start + startDir * t;
        float pointSide = direction.x * (point.y - start.y) - direction.y * (point.x - start.x);
        if (pointSide * side <= 0f)
            return false;

        bypass = point;
        return true;
    }

    private bool TryOuterTangent(
        Vector2[] vertices,
        int from,
        int to,
        int step,
        Vector2 origin,
        Vector2 inside,
        Vector2 outward,
        out Vector2 vertex)
    {
        vertex = default;
        Vector2 toInside = inside - origin;
        if (toInside.sqrMagnitude == 0f)
            return false;

        float outsideSign = toInside.x * outward.y - toInside.y * outward.x;
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
            Vector2 candidate = vertices[index];
            float score = TangentScore(toInside, candidate - origin, sign);
            if (score > bestScore)
            {
                bestScore = score;
                vertex = candidate;
            }
        }

        return true;
    }

    private float TangentScore(Vector2 toInside, Vector2 toVertex, float sign)
    {
        float cross = toInside.x * toVertex.y - toInside.y * toVertex.x;
        float dot = toInside.x * toVertex.x + toInside.y * toVertex.y;
        return Mathf.Atan2(cross, dot) * sign;
    }

    private float ChainLength(Vector2[] vertices, Vector2 fromHit, int from, int to, Vector2 toHit, int step)
    {
        int count = vertices.Length;
        float length = (vertices[from] - fromHit).magnitude;
        int index = from;
        int guard = 0;
        while (index != to && guard < count)
        {
            int next = StepIndex(index, step, count);
            length += (vertices[next] - vertices[index]).magnitude;
            index = next;
            guard++;
        }

        length += (toHit - vertices[to]).magnitude;
        return length;
    }

    private float ChainSide(Vector2[] vertices, int from, int to, int step, Vector2 start, Vector2 direction)
    {
        int count = vertices.Length;
        int index = from;
        float bestAbs = 0f;
        float side = 0f;
        int guard = 0;
        while (true)
        {
            Vector2 point = vertices[index];
            float cross = direction.x * (point.y - start.y) - direction.y * (point.x - start.x);
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

    private Vector2 FarthestOnChain(Vector2[] vertices, int from, int to, int step, Vector2 start, Vector2 direction)
    {
        int count = vertices.Length;
        int index = from;
        Vector2 farthest = vertices[index];
        float best = OffsetSqr(farthest, start, direction);
        int guard = 0;
        while (index != to && guard < count)
        {
            index = StepIndex(index, step, count);
            guard++;
            Vector2 candidate = vertices[index];
            float offset = OffsetSqr(candidate, start, direction);
            if (offset > best)
            {
                best = offset;
                farthest = candidate;
            }
        }

        return farthest;
    }

    private Vector2 FartherEdgeVertex(Vector2[] vertices, int edge, Vector2 start, Vector2 direction)
    {
        int count = vertices.Length;
        Vector2 a = vertices[edge];
        Vector2 b = vertices[(edge + 1) % count];
        return OffsetSqr(a, start, direction) >= OffsetSqr(b, start, direction) ? a : b;
    }

    private float OffsetSqr(Vector2 point, Vector2 start, Vector2 direction)
    {
        float cross = direction.x * (point.y - start.y) - direction.y * (point.x - start.x);
        return cross * cross;
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

    private bool IsGraze(
        Vector2[] vertices,
        Vector2 segmentStart,
        Vector2 segmentEnd,
        Vector2 enterPoint,
        Vector2 exitPoint,
        int hits,
        Vector2 globalStart,
        Vector2 globalEnd)
    {
        if ((exitPoint - enterPoint).sqrMagnitude > GrazeDistanceSqr)
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

    private bool IsStrictlyInside(Vector2 point, Vector2 allowedStart, Vector2 allowedEnd)
    {
        int objectCount = _navigationMapObjects.Count;
        for (int i = 0; i < objectCount; i++)
        {
            Vector2[] vertices = _navigationMapObjects[i].Vertices;
            if (ContainsRoute(vertices, allowedStart, allowedEnd))
                continue;
            if (IsStrictlyInsideContour(vertices, point))
                return true;
        }

        return false;
    }

    private bool ContainsRoute(Vector2[] vertices, Vector2 start, Vector2 end)
    {
        return IsStrictlyInsideContour(vertices, start) && IsStrictlyInsideContour(vertices, end);
    }

    private bool IsStrictlyInsideContour(Vector2[] vertices, Vector2 point)
    {
        if (vertices == null || vertices.Length < 3 || !ContainsPoint(vertices, point))
            return false;

        return DistanceToEdgesSqr(vertices, point) > GrazeDistanceSqr;
    }

    private bool ContainsPoint(Vector2[] vertices, Vector2 point)
    {
        int count = vertices.Length;
        bool inside = false;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            Vector2 a = vertices[i];
            Vector2 b = vertices[j];
            if ((a.y > point.y) != (b.y > point.y)
                && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private float DistanceToEdgesSqr(Vector2[] vertices, Vector2 point)
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

    private float DistanceToSegmentSqr(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 edge = b - a;
        float lengthSqr = edge.sqrMagnitude;
        if (lengthSqr == 0f)
            return (point - a).sqrMagnitude;

        float t = Vector2.Dot(point - a, edge) / lengthSqr;
        if (t < 0f)
            t = 0f;
        else if (t > 1f)
            t = 1f;

        Vector2 closest = a + edge * t;
        return (point - closest).sqrMagnitude;
    }

    private static bool IsNear(Vector2 a, Vector2 b)
    {
        return (a - b).sqrMagnitude <= GrazeDistanceSqr;
    }

    private void OnDrawGizmos()
    {
        RebuildPath();
        if (_waypoints != null)
        {
            Gizmos.color = Color.green;
            Vector3 previous = default;
            bool hasPrevious = false;
            for (int i = 0; i < _waypoints.Length; i++)
            {
                Transform waypoint = _waypoints[i];
                if (waypoint == null)
                    continue;

                Gizmos.DrawSphere(waypoint.position, 0.35f);
                if (hasPrevious)
                    Gizmos.DrawLine(previous, waypoint.position);

                previous = waypoint.position;
                hasPrevious = true;
            }
        }

        if (_navigationMapData != null)
        {
            var list = _navigationMapData.Objects;

            foreach (var obj in list)
            {
                if (obj.Vertices.Length < 2)
                    continue;
                Gizmos.color = Color.yellow;
                for (int i = 0; i < obj.Vertices.Length; i++)
                {
                    Vector3 start = new Vector3(obj.Vertices[i].x, 0f, obj.Vertices[i].y);
                    Vector3 end = new Vector3(obj.Vertices[(i + 1) % obj.Vertices.Length].x, 0f, obj.Vertices[(i + 1) % obj.Vertices.Length].y);
                    Gizmos.DrawLine(start, end);
                }
            }
        }

        if (_hitPoints.Count > 0)
        {
            Gizmos.color = Color.red;
            for (int i = 0; i < _hitPoints.Count; i++)
                Gizmos.DrawSphere(_hitPoints[i], 0.5f);
        }

        if (_bypassPoints.Count > 0)
        {
            Gizmos.color = Color.blue;
            for (int i = 0; i < _bypassPoints.Count; i++)
                Gizmos.DrawSphere(_bypassPoints[i], 0.5f);
        }

        if (_curve.Length > 1)
        {
            Gizmos.color = Color.magenta;
            for (int i = 1; i < _curve.Length; i++)
                Gizmos.DrawLine(_curve[i - 1], _curve[i]);
        }
    }

    private struct SegmentHit
    {
        public NavigationMapObject Object;
        public Vector2 Point;
        public float T;
        public int Edge;
        public bool Used;
    }

    private struct ObjectCenter
    {
        public Vector2 Point;
        public float T;
    }
}


public static class UnitSeparation
{
    public const float DefaultRadius = 0.28f;

    const float SeparationFactor = 1.15f;
    const float InfluencePadding = 0.12f;
    const float MaxOffset = 0.4f;
    const float MaxOffsetSpeed = 1.7f;
    const float ReturnSpeed = 3.2f;
    const float StationaryMaxNudge = 0.18f;
    const float StationaryGiveUp = 0.26f;
    const float AbandonDistance = MaxOffset * 3.25f;
    const float ObstacleSkin = 0.06f;
    const float MinRadius = 0.2f;
    const float MaxRadius = 0.75f;
    const int MaxAgents = 1024;
    const int BucketCount = 2048;
    const int BucketMask = BucketCount - 1;
    const float CellSize = 2.5f;
    const float InvCellSize = 1f / CellSize;

    /// <summary>
    /// <para>{RU} Главный выключатель. false — юниты снова идут строго по серверной кривой.</para>
    /// <para>{EN} Master switch. False puts every unit back on the raw server curve.</para>
    /// </summary>
    public static bool Enabled = true;

    struct Agent
    {
        public bool InUse;
        public bool Active;
        public bool Pushable;
        public int Frame;
        public int Next;
        public int CellX;
        public int CellZ;
        public float X;
        public float Z;
        public float DirX;
        public float DirZ;
        public float Radius;
        public float OffsetX;
        public float OffsetZ;
    }

    static Agent[] _agents;
    static int[] _buckets;
    static int[] _freeNext;
    static int _count;
    static int _freeHead = -1;
    static int _gridFrame = -1;
    static bool _loggedPoolFull;
    static IReadOnlyList<NavigationMapObject> _obstacles;

    /// <summary>
    /// <para>{RU} Контуры непроходимых объектов. null или пустой список — проверка стен пропускается.</para>
    /// <para>{EN} Impassable contours. Null or an empty list skips the wall test.</para>
    /// </summary>
    public static void SetObstacles(IReadOnlyList<NavigationMapObject> objects)
    {
        _obstacles = objects;
    }

    /// <summary>
    /// <para>{RU} Занимает слот. -1, если пул полный: юнит остаётся на серверном пути.</para>
    /// <para>{EN} Takes a slot. Returns -1 when the pool is full, and that unit stays on the server path.</para>
    /// </summary>
    public static int Register()
    {
        EnsureArrays();
        int slot;
        if (_freeHead >= 0)
        {
            slot = _freeHead;
            _freeHead = _freeNext[slot];
        }
        else
        {
            if (_count >= MaxAgents)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                if (!_loggedPoolFull)
                {
                    _loggedPoolFull = true;
                    Debug.LogWarning("{EN} Unit separation pool is full. Extra units stay on the server path.");
                }
#endif
                return -1;
            }

            slot = _count++;
        }

        _agents[slot] = default;
        _agents[slot].InUse = true;
        _agents[slot].Radius = DefaultRadius;
        _agents[slot].Frame = -1;
        return slot;
    }

    /// <summary>
    /// <para>{RU} Возвращает слот в пул. Повторный вызов безопасен.</para>
    /// <para>{EN} Returns the slot to the pool. A second call is safe.</para>
    /// </summary>
    public static void Unregister(int slot)
    {
        if (!IsLive(slot))
            return;

        _agents[slot].InUse = false;
        _agents[slot].Active = false;
        _agents[slot].Frame = -1;
        _agents[slot].OffsetX = 0f;
        _agents[slot].OffsetZ = 0f;
        _freeNext[slot] = _freeHead;
        _freeHead = slot;
    }

    /// <summary>
    /// <para>{RU} Юнит не толкается и не является препятствием, пока снова не пришлёт позицию.</para>
    /// <para>{EN} The unit is neither pushed nor an obstacle until it submits a position again.</para>
    /// </summary>
    public static void Deactivate(int slot)
    {
        if (!IsLive(slot))
            return;

        _agents[slot].Active = false;
        _agents[slot].Frame = -1;
        _agents[slot].OffsetX = 0f;
        _agents[slot].OffsetZ = 0f;
    }

    /// <summary>
    /// <para>{RU} Сбрасывает накопленный offset, не выключая слот.</para>
    /// <para>{EN} Clears the stored offset without releasing the slot.</para>
    /// </summary>
    public static void ClearOffset(int slot)
    {
        if (!IsLive(slot))
            return;

        _agents[slot].OffsetX = 0f;
        _agents[slot].OffsetZ = 0f;
        _agents[slot].Active = false;
        _agents[slot].Frame = -1;
    }

    public static float ClampRadius(float radius)
    {
        if (radius <= 0.05f)
            return DefaultRadius;
        return Mathf.Clamp(radius, MinRadius, MaxRadius);
    }

    /// <summary>
    /// <para>{RU} Пишет серверную позицию кадра. Вызывать из Update, до визуального offset.</para>
    /// <para>{EN} Stores this frame's server position. Call from Update, before the visual offset.</para>
    /// </summary>
    public static void Submit(int slot, float x, float z, float dirX, float dirZ, float radius, bool pushable)
    {
        if (!Enabled || !IsLive(slot))
            return;

        if (dirX * dirX + dirZ * dirZ < 1e-6f)
            pushable = false;

        Agent agent = _agents[slot];
        agent.Active = true;
        agent.Pushable = pushable;
        agent.Frame = Time.frameCount;
        agent.X = x;
        agent.Z = z;
        agent.DirX = dirX;
        agent.DirZ = dirZ;
        agent.Radius = ClampRadius(radius);
        _agents[slot] = agent;
    }

    /// <summary>
    /// <para>{RU} Боковой offset этого кадра. Ноль — юнит остаётся на серверной кривой.</para>
    /// <para>{EN} Lateral offset for this frame. Zero keeps the unit on the server curve.</para>
    /// </summary>
    public static Vector2 Evaluate(int slot, float deltaTime)
    {
        if (!Enabled || !IsLive(slot))
        {
            if (IsLive(slot))
            {
                _agents[slot].OffsetX = 0f;
                _agents[slot].OffsetZ = 0f;
            }

            return Vector2.zero;
        }

        int frame = Time.frameCount;
        if (_gridFrame != frame)
            RebuildGrid(frame);

        Agent agent = _agents[slot];
        if (!agent.Active || agent.Frame != frame)
        {
            agent.OffsetX = 0f;
            agent.OffsetZ = 0f;
            _agents[slot] = agent;
            return Vector2.zero;
        }

        float dt = Mathf.Min(Mathf.Max(deltaTime, 0f), 0.05f);
        Vector2 current = new Vector2(agent.OffsetX, agent.OffsetZ);
        Vector2 target = agent.Pushable
            ? SolveOffset(slot, agent)
            : Vector2.zero;
        float speed = target.sqrMagnitude <= 1e-8f ? ReturnSpeed : MaxOffsetSpeed;
        Vector2 next = Vector2.MoveTowards(current, target, speed * dt);
        if (next.sqrMagnitude < 1e-8f)
            next = Vector2.zero;

        agent.OffsetX = next.x;
        agent.OffsetZ = next.y;
        _agents[slot] = agent;
        return next;
    }

    static Vector2 SolveOffset(int slot, Agent agent)
    {
        float dirLenSq = agent.DirX * agent.DirX + agent.DirZ * agent.DirZ;
        if (dirLenSq < 1e-6f)
            return Vector2.zero;

        float invDir = 1f / Mathf.Sqrt(dirLenSq);
        float forwardX = agent.DirX * invDir;
        float forwardZ = agent.DirZ * invDir;
        float rightX = forwardZ;
        float rightZ = -forwardX;

        float signed = 0f;
        int cx = FloorToInt(agent.X * InvCellSize);
        int cz = FloorToInt(agent.Z * InvCellSize);
        for (int ix = cx - 1; ix <= cx + 1; ix++)
        {
            for (int iz = cz - 1; iz <= cz + 1; iz++)
            {
                int index = _buckets[Hash(ix, iz)];
                while (index >= 0)
                {
                    Agent other = _agents[index];
                    int next = other.Next;
                    if (index != slot && other.CellX == ix && other.CellZ == iz)
                        signed += PushFromNeighbor(slot, index, agent, other, rightX, rightZ);
                    index = next;
                }
            }
        }

        if (Mathf.Abs(signed) > AbandonDistance)
            return Vector2.zero;

        float lateral = Mathf.Clamp(signed, -MaxOffset, MaxOffset);
        lateral = AcceptLateral(agent.X, agent.Z, rightX, rightZ, lateral);
        return new Vector2(rightX * lateral, rightZ * lateral);
    }

    static float PushFromNeighbor(int slot, int otherSlot, Agent agent, Agent other, float rightX, float rightZ)
    {
        float dx = agent.X - other.X;
        float dz = agent.Z - other.Z;
        float distSq = dx * dx + dz * dz;
        float desired = (agent.Radius + other.Radius) * SeparationFactor;
        float influence = desired + InfluencePadding;
        if (distSq >= influence * influence)
            return 0f;

        float side = SideSign(slot, otherSlot, dx, dz, distSq, rightX, rightZ);
        if (distSq < 1e-8f)
        {
            if (!other.Pushable)
                return 0f;
            return side * Mathf.Min(desired * 0.5f, MaxOffset);
        }

        float dist = Mathf.Sqrt(distSq);
        if (!other.Pushable)
        {
            float penetration = desired - dist;
            if (penetration > StationaryGiveUp)
                return 0f;
            if (penetration <= 0f)
                return side * PaddingNudge(influence, desired, dist) * 0.45f;
            return side * Mathf.Min(penetration, StationaryMaxNudge);
        }

        if (dist < desired)
            return side * Mathf.Min((desired - dist) * 0.5f, MaxOffset);

        return side * PaddingNudge(influence, desired, dist);
    }

    static float PaddingNudge(float influence, float desired, float dist)
    {
        float band = influence - desired;
        if (band < 1e-4f)
            return 0f;
        float closeness = Mathf.Clamp01((influence - dist) / band);
        return closeness * 0.05f;
    }

    static float SideSign(int slot, int otherSlot, float dx, float dz, float distSq, float rightX, float rightZ)
    {
        if (distSq > 1e-8f)
        {
            float lateral = (dx * rightX + dz * rightZ) / Mathf.Sqrt(distSq);
            if (Mathf.Abs(lateral) >= 0.2f)
                return lateral > 0f ? 1f : -1f;
        }

        return CollinearSide(slot, otherSlot, rightX, rightZ);
    }

    static float CollinearSide(int slot, int otherSlot, float rightX, float rightZ)
    {
        float sign = slot < otherSlot ? 1f : -1f;
        float alongRight = sign * rightX;
        if (Mathf.Abs(alongRight) < 0.35f)
            alongRight = sign * rightZ;
        if (alongRight > 0f)
            return 1f;
        if (alongRight < 0f)
            return -1f;
        return sign;
    }

    static float AcceptLateral(float x, float z, float rightX, float rightZ, float lateral)
    {
        if (Mathf.Abs(lateral) < 1e-4f)
            return 0f;
        if (OffsetSafe(x, z, rightX * lateral, rightZ * lateral))
            return lateral;

        float flipped = -lateral;
        if (OffsetSafe(x, z, rightX * flipped, rightZ * flipped))
            return flipped;

        float half = lateral * 0.5f;
        if (OffsetSafe(x, z, rightX * half, rightZ * half))
            return half;

        float halfFlipped = -half;
        if (OffsetSafe(x, z, rightX * halfFlipped, rightZ * halfFlipped))
            return halfFlipped;

        return 0f;
    }

    static bool OffsetSafe(float x, float z, float offsetX, float offsetZ)
    {
        if (IsUnsafe(x + offsetX, z + offsetZ))
            return false;
        return !IsUnsafe(x + offsetX * 0.5f, z + offsetZ * 0.5f);
    }

    static bool IsUnsafe(float x, float z)
    {
        IReadOnlyList<NavigationMapObject> objects = _obstacles;
        if (objects == null)
            return false;

        int count = objects.Count;
        for (int i = 0; i < count; i++)
        {
            NavigationMapObject obj = objects[i];
            if (obj == null)
                continue;
            if (x < obj.Min.x - ObstacleSkin || x > obj.Max.x + ObstacleSkin
                || z < obj.Min.y - ObstacleSkin || z > obj.Max.y + ObstacleSkin)
                continue;

            Vector2[] vertices = obj.Vertices;
            if (vertices == null || vertices.Length < 3)
                continue;
            if (Contains(vertices, x, z) || NearEdge(vertices, x, z, ObstacleSkin))
                return true;
        }

        return false;
    }

    static bool Contains(Vector2[] vertices, float x, float z)
    {
        int count = vertices.Length;
        bool inside = false;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            Vector2 a = vertices[i];
            Vector2 b = vertices[j];
            if ((a.y > z) != (b.y > z)
                && x < (b.x - a.x) * (z - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }

        return inside;
    }

    static bool NearEdge(Vector2[] vertices, float x, float z, float skin)
    {
        float skinSq = skin * skin;
        int count = vertices.Length;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            if (DistanceToSegmentSqr(x, z, vertices[j], vertices[i]) <= skinSq)
                return true;
        }

        return false;
    }

    static float DistanceToSegmentSqr(float x, float z, Vector2 a, Vector2 b)
    {
        float abX = b.x - a.x;
        float abZ = b.y - a.y;
        float lengthSq = abX * abX + abZ * abZ;
        if (lengthSq < 1e-8f)
        {
            float dx = x - a.x;
            float dz = z - a.y;
            return dx * dx + dz * dz;
        }

        float t = ((x - a.x) * abX + (z - a.y) * abZ) / lengthSq;
        t = Mathf.Clamp01(t);
        float px = a.x + abX * t - x;
        float pz = a.y + abZ * t - z;
        return px * px + pz * pz;
    }

    static void RebuildGrid(int frame)
    {
        _gridFrame = frame;
        ClearBuckets();
        int count = _count;
        for (int i = 0; i < count; i++)
        {
            Agent agent = _agents[i];
            if (!agent.Active || agent.Frame != frame)
                continue;

            int cellX = FloorToInt(agent.X * InvCellSize);
            int cellZ = FloorToInt(agent.Z * InvCellSize);
            int bucket = Hash(cellX, cellZ);
            agent.CellX = cellX;
            agent.CellZ = cellZ;
            agent.Next = _buckets[bucket];
            _buckets[bucket] = i;
            _agents[i] = agent;
        }
    }

    static int Hash(int cellX, int cellZ)
    {
        return (cellX * 73856093 ^ cellZ * 19349663) & BucketMask;
    }

    static int FloorToInt(float value)
    {
        int truncated = (int)value;
        return value < truncated ? truncated - 1 : truncated;
    }

    static void ClearBuckets()
    {
        for (int i = 0; i < BucketCount; i++)
            _buckets[i] = -1;
    }

    static bool IsLive(int slot)
    {
        return _agents != null && (uint)slot < (uint)_count && _agents[slot].InUse;
    }

    static void EnsureArrays()
    {
        if (_agents != null)
            return;

        _agents = new Agent[MaxAgents];
        _freeNext = new int[MaxAgents];
        _buckets = new int[BucketCount];
        ClearBuckets();
    }
}