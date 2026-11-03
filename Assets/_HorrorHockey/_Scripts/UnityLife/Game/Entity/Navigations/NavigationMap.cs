using _FM.DI;
using ContractsServer.Data;
using ContractsServer.Data.Navigation;
using ContractsServer.Service.Navigation;
using Shared.Core.Contracts.Domain;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using FMVector2 = FM.Mathematics.DataTypes.Vector2;

/// <summary>
/// <para>{RU} Тестовый просмотр маршрута. Тот же RouteBypass и RouteCurve, что у юнитов на сервере и клиенте.</para>
/// <para>{EN} Route preview. Same RouteBypass and RouteCurve the server and client units use.</para>
/// </summary>
public class NavigationMap : MonoBehaviour
{
    [SerializeField] private Transform[] _waypoints;

    [Inject] private INavigationMapData _navigationMapData = null;
    public int InitializeOrder => 51;

    private IReadOnlyList<NavigationMapObject> _navigationMapObjects;
    private RouteBypass _bypass;
    private FMVector2[] _controls = System.Array.Empty<FMVector2>();
    private Vector3[] _curve = System.Array.Empty<Vector3>();

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(1f);
        UnityInjectService.Instance.Inject(this);
        _navigationMapObjects = _navigationMapData.Objects;
        UnitSeparation.SetObstacles(_navigationMapObjects);
        _bypass = CreateBypass(_navigationMapObjects);
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

        if (_bypass == null)
            Debug.LogWarning("{EN} Navigation path has no map data. Curve follows waypoints only.");
#endif

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        var clock = System.Diagnostics.Stopwatch.StartNew();
#endif
        RebuildPath();

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        clock.Stop();
        Debug.Log("{EN} Navigation path built. Controls: " + _controls.Length
            + ", samples: " + _curve.Length
            + ", time: " + clock.Elapsed.TotalMilliseconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
            + " ms.");
#endif
    }

    private static RouteBypass CreateBypass(IReadOnlyList<NavigationMapObject> objects)
    {
        if (objects == null)
            return null;

        var obstacles = new List<NavigationObstacle>(objects.Count);
        for (int i = 0; i < objects.Count; i++)
        {
            Vector2[] source = objects[i].Vertices;
            var vertices = new FMVector2[source.Length];
            for (int v = 0; v < source.Length; v++)
                vertices[v] = new FMVector2(source[v].x, source[v].y);

            obstacles.Add(new NavigationObstacle(objects[i].Name, vertices));
        }

        return new RouteBypass(obstacles, GameConstants.RouteClearance, GameConstants.CurveStep);
    }

    private void RebuildPath()
    {
        _controls = System.Array.Empty<FMVector2>();
        _curve = System.Array.Empty<Vector3>();
        if (_waypoints == null)
            return;

        var waypoints = new List<FMVector2>(_waypoints.Length);
        float height = 0f;
        for (int i = 0; i < _waypoints.Length; i++)
        {
            Transform waypoint = _waypoints[i];
            if (waypoint == null)
                continue;

            if (waypoints.Count == 0)
                height = waypoint.position.y;
            waypoints.Add(new FMVector2(waypoint.position.x, waypoint.position.z));
        }

        if (waypoints.Count < 2)
            return;

        _controls = _bypass != null ? _bypass.Build(waypoints.ToArray()) : waypoints.ToArray();
        if (!RouteCurve.TryBuild(_controls, GameConstants.CurveStep, out FMVector2[] curve))
            return;

        _curve = new Vector3[curve.Length];
        for (int i = 0; i < curve.Length; i++)
            _curve[i] = new Vector3(curve[i].X, height, curve[i].Y);
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
            Gizmos.color = Color.yellow;
            foreach (var obj in _navigationMapData.Objects)
            {
                if (obj.Vertices.Length < 2)
                    continue;

                for (int i = 0; i < obj.Vertices.Length; i++)
                {
                    Vector2 a = obj.Vertices[i];
                    Vector2 b = obj.Vertices[(i + 1) % obj.Vertices.Length];
                    Gizmos.DrawLine(new Vector3(a.x, 0f, a.y), new Vector3(b.x, 0f, b.y));
                }
            }
        }

        if (_controls.Length > 0)
        {
            Gizmos.color = Color.blue;
            float height = _curve.Length > 0 ? _curve[0].y : 0f;
            for (int i = 0; i < _controls.Length; i++)
                Gizmos.DrawSphere(new Vector3(_controls[i].X, height, _controls[i].Y), 0.5f);
        }

        if (_curve.Length > 1)
        {
            Gizmos.color = Color.magenta;
            for (int i = 1; i < _curve.Length; i++)
                Gizmos.DrawLine(_curve[i - 1], _curve[i]);
        }
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