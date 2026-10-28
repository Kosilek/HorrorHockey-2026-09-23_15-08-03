//#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityLife.Tools.Navigation
{
    [AddComponentMenu("Hockey/Flat Map Footprint")]
    [DisallowMultipleComponent]
    public class FlatMapFootprint : MonoBehaviour
    {
        private const string DataFolderAsset = "Assets/_HorrorHockey/Data/FlatMap";
        private const string PreviewRootName = "FlatMapPreview";
        private const float CoplanarBias = 0.0001f;
        private const float OutlineWidth = 0.06f;

        [Tooltip("Объекты со сцены. Можно несколько. Берутся MeshFilter, а если их нет — MeshCollider.")]
        [SerializeField] private GameObject[] _sources = Array.Empty<GameObject>();

        [Tooltip("Насколько выше самой нижней точки ещё считать вершину нижней, в метрах.")]
        [SerializeField, Min(0f)] private float _bottomEpsilon = 0.05f;

        [Tooltip("Захватывать меши в детях объекта.")]
        [SerializeField] private bool _includeChildren = true;

        [Tooltip("Имя файла в Assets/_HorrorHockey/Data/FlatMap.")]
        [SerializeField] private string _fileName = "FlatMap";

        [Tooltip("Высота нарисованной карты. 0 — как просили. Если мерцает на земле, поставь 0.02.")]
        [SerializeField] private float _drawHeight;

        [SerializeField] private Color _mapColor = new Color(0.1f, 0.85f, 0.35f, 1f);

        [SerializeField] private string _lastExportAssetPath;
        [SerializeField] private TextAsset _jsonFile;

        [NonSerialized] private readonly List<MeshFilter> _filterBuffer = new();
        [NonSerialized] private readonly List<MeshCollider> _colliderBuffer = new();
        [NonSerialized] private readonly List<SourceMesh> _meshBuffer = new();
        [NonSerialized] private readonly List<FlatMapObject> _objectBuffer = new();
        [NonSerialized] private readonly HashSet<EntityId> _seenSources = new();
        [NonSerialized] private readonly Dictionary<long, int> _weld = new();
        [NonSerialized] private readonly HashSet<long> _edgeKeys = new();
        [NonSerialized] private readonly List<FlatMapPoint> _pointBuffer = new();
        [NonSerialized] private readonly List<int> _triangleBuffer = new();
        [NonSerialized] private readonly List<int> _outlineBuffer = new();
        [NonSerialized] private Material _runtimeMaterial;
        [NonSerialized] private Mesh _runtimeMesh;

        /// <summary>
        /// <para>{RU} Пишет json с нижними вершинами (x, z) в Assets/_MobCraftOnline/_Data/FlatMap.</para>
        /// <para>{EN} Writes json of bottom vertices (x, z) into Assets/_MobCraftOnline/_Data/FlatMap.</para>
        /// </summary>
        [ContextMenu("Export Json")]
        public void ExportJson()
        {
#if UNITY_EDITOR
            if (!TryBuildFromSources(out FlatMapFile data))
                return;

            string fileName = SanitizeFileName(_fileName);
            string directory = Path.Combine(Application.dataPath, "_HorrorHockey", "Data", "FlatMap");
            Directory.CreateDirectory(directory);

            string assetPath = DataFolderAsset + "/" + fileName + ".json";
            string fullPath = Path.Combine(directory, fileName + ".json");
            File.WriteAllText(fullPath, JsonUtility.ToJson(data, true));
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            Undo.RecordObject(this, "Export Flat Map Json");
            _lastExportAssetPath = assetPath;
            _jsonFile = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            EditorUtility.SetDirty(this);
            if (gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(gameObject.scene);

            int vertexCount = CountVertices(data);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("{EN} Flat map json written. Objects: " + data.objects.Length + ", vertices: " + vertexCount + ". " + assetPath);
#endif
            EditorGUIUtility.PingObject(_jsonFile);
#else
            Debug.LogWarning("FlatMapFootprint: Export Json работает в редакторе Unity.");
#endif
        }

        /// <summary>
        /// <para>{RU} Рисует карту объектов на высоте y = _drawHeight. Если список пуст, берёт последний json.</para>
        /// <para>{EN} Draws the object map at y = _drawHeight. Uses the last json when the source list is empty.</para>
        /// </summary>
        [ContextMenu("Draw Map")]
        public void DrawMap()
        {
            if (!TryResolveDocument(out FlatMapFile data))
                return;

            ApplyMap(data);
        }

        /// <summary>
        /// <para>{RU} Убирает нарисованную карту со сцены. Json не трогает.</para>
        /// <para>{EN} Removes the drawn map from the scene. Does not delete json.</para>
        /// </summary>
        [ContextMenu("Clear Map")]
        public void ClearMap()
        {
            Transform preview = transform.Find(PreviewRootName);
            if (preview == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Undo.DestroyObjectImmediate(preview.gameObject);
                return;
            }
#endif
            Destroy(preview.gameObject);
        }

        private bool TryResolveDocument(out FlatMapFile data)
        {
            if (HasAnySource())
                return TryBuildFromSources(out data);

            return TryBuildFromJson(out data);
        }

        private bool HasAnySource()
        {
            if (_sources == null)
                return false;

            for (int i = 0; i < _sources.Length; i++)
            {
                if (_sources[i] != null)
                    return true;
            }

            return false;
        }

        private bool TryBuildFromSources(out FlatMapFile data)
        {
            data = null;
            if (!HasAnySource())
            {
                Debug.LogWarning("FlatMapFootprint: список объектов пуст.");
                return false;
            }

            _objectBuffer.Clear();
            _seenSources.Clear();

            for (int i = 0; i < _sources.Length; i++)
            {
                GameObject source = _sources[i];
                if (source == null)
                    continue;

                if (!_seenSources.Add(source.GetEntityId()))
                    continue;

                if (TryBuildObject(source, out FlatMapObject entry))
                    _objectBuffer.Add(entry);
            }

            if (_objectBuffer.Count == 0)
            {
                Debug.LogWarning("FlatMapFootprint: нижние вершины не найдены. Проверь MeshFilter и Bottom Epsilon.");
                return false;
            }

            data = new FlatMapFile { objects = _objectBuffer.ToArray() };
            return true;
        }

        private bool TryBuildFromJson(out FlatMapFile data)
        {
            data = null;
            if (_jsonFile == null || string.IsNullOrEmpty(_jsonFile.text))
            {
                Debug.LogWarning("FlatMapFootprint: нет объектов и нет json. Сначала заполни список или сделай Export Json.");
                return false;
            }

            data = JsonUtility.FromJson<FlatMapFile>(_jsonFile.text);
            if (data?.objects == null || data.objects.Length == 0)
            {
                Debug.LogWarning("FlatMapFootprint: json пустой.");
                data = null;
                return false;
            }

            return true;
        }

        private bool TryBuildObject(GameObject source, out FlatMapObject entry)
        {
            entry = null;
            CollectMeshes(source);
            if (_meshBuffer.Count == 0)
            {
                if (HasSkinnedRenderer(source))
                    Debug.LogWarning("FlatMapFootprint: '" + source.name + "' — только SkinnedMeshRenderer, его пропускаю.");
                else
                    Debug.LogWarning("FlatMapFootprint: '" + source.name + "' без MeshFilter и MeshCollider.");
                return false;
            }

            float minY = FindMinWorldY();
            if (float.IsPositiveInfinity(minY))
            {
                Debug.LogWarning("FlatMapFootprint: '" + source.name + "' — меш без вершин.");
                return false;
            }

            float band = minY + Mathf.Max(_bottomEpsilon, CoplanarBias);
            AppendBottomGeometry(band);

            if (_pointBuffer.Count == 0)
            {
                Debug.LogWarning("FlatMapFootprint: '" + source.name + "' — нет нижних вершин.");
                return false;
            }

            entry = new FlatMapObject
            {
                name = source.name,
                vertices = _pointBuffer.ToArray(),
                triangles = _triangleBuffer.ToArray(),
                outline = _outlineBuffer.ToArray()
            };
            return true;
        }

        private void CollectMeshes(GameObject source)
        {
            _meshBuffer.Clear();
            _filterBuffer.Clear();
            _colliderBuffer.Clear();

            if (_includeChildren)
                source.GetComponentsInChildren(true, _filterBuffer);
            else if (source.TryGetComponent(out MeshFilter filter))
                _filterBuffer.Add(filter);

            for (int i = 0; i < _filterBuffer.Count; i++)
            {
                MeshFilter filter = _filterBuffer[i];
                if (filter == null || IsGeneratedPreview(filter.transform))
                    continue;

                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                    continue;

                _meshBuffer.Add(new SourceMesh(mesh, filter.transform.localToWorldMatrix));
            }

            if (_meshBuffer.Count > 0)
                return;

            if (_includeChildren)
                source.GetComponentsInChildren(true, _colliderBuffer);
            else if (source.TryGetComponent(out MeshCollider collider))
                _colliderBuffer.Add(collider);

            for (int i = 0; i < _colliderBuffer.Count; i++)
            {
                MeshCollider collider = _colliderBuffer[i];
                if (collider == null || collider.sharedMesh == null || IsGeneratedPreview(collider.transform))
                    continue;

                _meshBuffer.Add(new SourceMesh(collider.sharedMesh, collider.transform.localToWorldMatrix));
            }
        }

        private bool IsGeneratedPreview(Transform candidate)
        {
            Transform preview = transform.Find(PreviewRootName);
            return preview != null && (candidate == preview || candidate.IsChildOf(preview));
        }

        private bool HasSkinnedRenderer(GameObject source)
        {
            return _includeChildren
                ? source.GetComponentInChildren<SkinnedMeshRenderer>(true) != null
                : source.TryGetComponent(out SkinnedMeshRenderer _);
        }

        private float FindMinWorldY()
        {
            float minY = float.PositiveInfinity;
            for (int i = 0; i < _meshBuffer.Count; i++)
            {
                SourceMesh sourceMesh = _meshBuffer[i];
                try
                {
                    using Mesh.MeshDataArray meshDataArray = Mesh.AcquireReadOnlyMeshData(sourceMesh.Mesh);
                    Mesh.MeshData meshData = meshDataArray[0];
                    if (meshData.vertexCount == 0)
                        continue;

                    using var vertices = new NativeArray<Vector3>(meshData.vertexCount, Allocator.Temp);
                    meshData.GetVertices(vertices);
                    Matrix4x4 localToWorld = sourceMesh.LocalToWorld;
                    for (int v = 0; v < vertices.Length; v++)
                    {
                        float worldY = localToWorld.MultiplyPoint3x4(vertices[v]).y;
                        if (worldY < minY)
                            minY = worldY;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("FlatMapFootprint: не читается меш '" + sourceMesh.Mesh.name + "'. " + exception.Message);
                }
            }

            return minY;
        }

        private void AppendBottomGeometry(float band)
        {
            _weld.Clear();
            _edgeKeys.Clear();
            _pointBuffer.Clear();
            _triangleBuffer.Clear();
            _outlineBuffer.Clear();

            for (int i = 0; i < _meshBuffer.Count; i++)
            {
                SourceMesh sourceMesh = _meshBuffer[i];
                try
                {
                    AppendMesh(sourceMesh, band);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("FlatMapFootprint: не читается меш '" + sourceMesh.Mesh.name + "'. " + exception.Message);
                }
            }
        }

        private void AppendMesh(SourceMesh sourceMesh, float band)
        {
            using Mesh.MeshDataArray meshDataArray = Mesh.AcquireReadOnlyMeshData(sourceMesh.Mesh);
            Mesh.MeshData meshData = meshDataArray[0];
            if (meshData.vertexCount == 0)
                return;

            using var vertices = new NativeArray<Vector3>(meshData.vertexCount, Allocator.Temp);
            meshData.GetVertices(vertices);

            Matrix4x4 localToWorld = sourceMesh.LocalToWorld;
            var worldX = new float[vertices.Length];
            var worldY = new float[vertices.Length];
            var worldZ = new float[vertices.Length];
            for (int v = 0; v < vertices.Length; v++)
            {
                Vector3 world = localToWorld.MultiplyPoint3x4(vertices[v]);
                worldX[v] = RoundMilli(world.x);
                worldY[v] = world.y;
                worldZ[v] = RoundMilli(world.z);
                if (world.y <= band)
                    Weld(worldX[v], worldZ[v]);
            }

            int subMeshCount = meshData.subMeshCount;
            for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
            {
                SubMeshDescriptor descriptor = meshData.GetSubMesh(subMesh);
                if (descriptor.topology != MeshTopology.Triangles || descriptor.indexCount < 3)
                    continue;

                using var indices = new NativeArray<int>(descriptor.indexCount, Allocator.Temp);
                meshData.GetIndices(indices, subMesh, true);
                int triangleCount = descriptor.indexCount / 3;
                for (int t = 0; t < triangleCount; t++)
                {
                    int i0 = indices[t * 3];
                    int i1 = indices[t * 3 + 1];
                    int i2 = indices[t * 3 + 2];
                    if ((uint)i0 >= (uint)vertices.Length || (uint)i1 >= (uint)vertices.Length || (uint)i2 >= (uint)vertices.Length)
                        continue;

                    bool b0 = worldY[i0] <= band;
                    bool b1 = worldY[i1] <= band;
                    bool b2 = worldY[i2] <= band;
                    int bottomCount = (b0 ? 1 : 0) + (b1 ? 1 : 0) + (b2 ? 1 : 0);
                    if (bottomCount == 3)
                        AddTriangle(worldX[i0], worldZ[i0], worldX[i1], worldZ[i1], worldX[i2], worldZ[i2]);
                    else if (bottomCount == 2)
                        AddOutlineEdge(b0, b1, b2, worldX, worldZ, i0, i1, i2);
                }
            }
        }

        private void AddTriangle(float x0, float z0, float x1, float z1, float x2, float z2)
        {
            int a = Weld(x0, z0);
            int b = Weld(x1, z1);
            int c = Weld(x2, z2);
            if (a == b || b == c || a == c)
                return;

            FlatMapPoint pa = _pointBuffer[a];
            FlatMapPoint pb = _pointBuffer[b];
            FlatMapPoint pc = _pointBuffer[c];
            float crossY = (pb.z - pa.z) * (pc.x - pa.x) - (pb.x - pa.x) * (pc.z - pa.z);
            if (crossY < 0f)
                (b, c) = (c, b);

            _triangleBuffer.Add(a);
            _triangleBuffer.Add(b);
            _triangleBuffer.Add(c);
        }

        private void AddOutlineEdge(bool b0, bool b1, bool b2, float[] worldX, float[] worldZ, int i0, int i1, int i2)
        {
            if (!b0)
                AddEdge(worldX[i1], worldZ[i1], worldX[i2], worldZ[i2]);
            else if (!b1)
                AddEdge(worldX[i0], worldZ[i0], worldX[i2], worldZ[i2]);
            else
                AddEdge(worldX[i0], worldZ[i0], worldX[i1], worldZ[i1]);
        }

        private void AddEdge(float x0, float z0, float x1, float z1)
        {
            int a = Weld(x0, z0);
            int b = Weld(x1, z1);
            if (a == b)
                return;

            if (a > b)
                (a, b) = (b, a);

            long key = ((long)a << 32) | (uint)b;
            if (!_edgeKeys.Add(key))
                return;

            _outlineBuffer.Add(a);
            _outlineBuffer.Add(b);
        }

        private int Weld(float x, float z)
        {
            long key = PackKey(x, z);
            if (_weld.TryGetValue(key, out int index))
                return index;

            index = _pointBuffer.Count;
            _weld.Add(key, index);
            _pointBuffer.Add(new FlatMapPoint { x = x, z = z });
            return index;
        }

        private void ApplyMap(FlatMapFile data)
        {
            var vertices = new List<Vector3>(256);
            var triangles = new List<int>(512);
            bool hasFill = false;
            for (int i = 0; i < data.objects.Length; i++)
            {
                FlatMapObject entry = data.objects[i];
                if (entry?.vertices == null || entry.vertices.Length == 0)
                    continue;

                if (entry.triangles != null && entry.triangles.Length >= 3)
                {
                    AppendWorldTriangles(entry, _drawHeight, vertices, triangles);
                    hasFill = true;
                }
            }

            float outlineY = hasFill ? _drawHeight + 0.01f : _drawHeight;
            for (int i = 0; i < data.objects.Length; i++)
            {
                FlatMapObject entry = data.objects[i];
                if (entry?.vertices == null || entry.outline == null || entry.outline.Length < 2)
                    continue;

                AppendOutlineQuads(entry, outlineY, vertices, triangles);
            }

            if (vertices.Count == 0 || triangles.Count == 0)
            {
                Debug.LogWarning("FlatMapFootprint: вершины есть, но собрать плоскую карту не из чего. Увеличь Bottom Epsilon.");
                return;
            }

            Transform root = EnsurePreviewRoot();
            Material material = EnsureMaterial();
            ApplyPreviewMesh(root, material, vertices, triangles);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                AssetDatabase.SaveAssets();
                if (gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("{EN} Flat map drawn at y = " + _drawHeight + ". Objects: " + data.objects.Length);
#endif
        }

        private static void AppendWorldTriangles(FlatMapObject entry, float y, List<Vector3> vertices, List<int> triangles)
        {
            int offset = vertices.Count;
            FlatMapPoint[] points = entry.vertices;
            for (int i = 0; i < points.Length; i++)
                vertices.Add(new Vector3(points[i].x, y, points[i].z));

            int[] source = entry.triangles;
            for (int i = 0; i + 2 < source.Length; i += 3)
            {
                int a = source[i];
                int b = source[i + 1];
                int c = source[i + 2];
                if ((uint)a >= (uint)points.Length || (uint)b >= (uint)points.Length || (uint)c >= (uint)points.Length)
                    continue;

                triangles.Add(offset + a);
                triangles.Add(offset + b);
                triangles.Add(offset + c);
            }
        }

        private static void AppendOutlineQuads(FlatMapObject entry, float y, List<Vector3> vertices, List<int> triangles)
        {
            FlatMapPoint[] points = entry.vertices;
            int[] source = entry.outline;
            float half = OutlineWidth * 0.5f;
            for (int i = 0; i + 1 < source.Length; i += 2)
            {
                int ia = source[i];
                int ib = source[i + 1];
                if ((uint)ia >= (uint)points.Length || (uint)ib >= (uint)points.Length || ia == ib)
                    continue;

                float ax = points[ia].x;
                float az = points[ia].z;
                float bx = points[ib].x;
                float bz = points[ib].z;
                float dx = bx - ax;
                float dz = bz - az;
                float length = Mathf.Sqrt(dx * dx + dz * dz);
                if (length < 0.0001f)
                    continue;

                float inv = half / length;
                float sx = -dz * inv;
                float sz = dx * inv;
                int offset = vertices.Count;
                vertices.Add(new Vector3(ax - sx, y, az - sz));
                vertices.Add(new Vector3(ax + sx, y, az + sz));
                vertices.Add(new Vector3(bx + sx, y, bz + sz));
                vertices.Add(new Vector3(bx - sx, y, bz - sz));
                triangles.Add(offset);
                triangles.Add(offset + 1);
                triangles.Add(offset + 2);
                triangles.Add(offset);
                triangles.Add(offset + 2);
                triangles.Add(offset + 3);
            }
        }

        private Transform EnsurePreviewRoot()
        {
            Transform root = transform.Find(PreviewRootName);
            GameObject rootObject;
            if (root == null)
            {
                rootObject = new GameObject(PreviewRootName);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Undo.RegisterCreatedObjectUndo(rootObject, "Draw Flat Map");
#endif
                root = rootObject.transform;
            }
            else
            {
                rootObject = root.gameObject;
            }

            root.SetParent(null, false);
            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.localScale = Vector3.one;
            root.SetParent(transform, true);
            rootObject.layer = gameObject.layer;
            return root;
        }

        private void ApplyPreviewMesh(Transform root, Material material, List<Vector3> vertices, List<int> triangles)
        {
            GameObject rootObject = root.gameObject;
            rootObject.layer = gameObject.layer;
            MeshFilter filter = rootObject.GetComponent<MeshFilter>();
            if (filter == null)
                filter = rootObject.AddComponent<MeshFilter>();

            MeshRenderer renderer = rootObject.GetComponent<MeshRenderer>();
            if (renderer == null)
                renderer = rootObject.AddComponent<MeshRenderer>();

            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            Mesh mesh = GetOrCreateMesh(filter);
            FillMesh(mesh, vertices, triangles);
            filter.sharedMesh = mesh;
            if (mesh.name.StartsWith("FlatMapRuntime_", StringComparison.Ordinal))
                _runtimeMesh = mesh;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorUtility.SetDirty(mesh);
#endif
        }

        private Mesh GetOrCreateMesh(MeshFilter filter)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                string assetPath = DataFolderAsset + "/" + SanitizeFileName(_fileName) + "_Map.asset";
                Mesh assetMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                if (assetMesh != null)
                    return assetMesh;

                Directory.CreateDirectory(Path.Combine(Application.dataPath, "_MobCraftOnline", "Data", "FlatMap"));
                assetMesh = new Mesh { name = SanitizeFileName(_fileName) + "_Map" };
                AssetDatabase.CreateAsset(assetMesh, assetPath);
                return assetMesh;
            }
#endif
            Mesh mesh = filter.sharedMesh;
            if (mesh != null && mesh.name.StartsWith("FlatMapRuntime_", StringComparison.Ordinal))
                return mesh;

            return new Mesh { name = "FlatMapRuntime_Map" };
        }

        private Material EnsureMaterial()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                string assetPath = DataFolderAsset + "/" + SanitizeFileName(_fileName) + ".mat";
                Material assetMaterial = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (assetMaterial == null)
                {
                    Directory.CreateDirectory(Path.Combine(Application.dataPath, "_MobCraftOnline", "Data", "FlatMap"));
                    assetMaterial = CreateMapMaterial();
                    AssetDatabase.CreateAsset(assetMaterial, assetPath);
                }

                ApplyColor(assetMaterial);
                EditorUtility.SetDirty(assetMaterial);
                return assetMaterial;
            }
#endif
            if (_runtimeMaterial != null)
            {
                ApplyColor(_runtimeMaterial);
                return _runtimeMaterial;
            }

            _runtimeMaterial = CreateMapMaterial();
            return _runtimeMaterial;
        }

        private Material CreateMapMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            var material = new Material(shader) { name = "FlatMap" };
            if (material.HasProperty("_Cull"))
                material.SetFloat("_Cull", 0f);

            ApplyColor(material);
            return material;
        }

        private void ApplyColor(Material material)
        {
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", _mapColor);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", _mapColor);
        }

        private static void FillMesh(Mesh mesh, List<Vector3> vertices, List<int> triangles)
        {
            mesh.Clear(false);
            mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0, false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            DestroyOwned(_runtimeMaterial);
            DestroyOwned(_runtimeMesh);
            _runtimeMaterial = null;
            _runtimeMesh = null;
        }

        private static void DestroyOwned(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        private static void DestroyPreviewObject(GameObject target)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Undo.DestroyObjectImmediate(target);
                return;
            }
#endif
            Destroy(target);
        }

        private static int CountVertices(FlatMapFile data)
        {
            int count = 0;
            for (int i = 0; i < data.objects.Length; i++)
            {
                FlatMapPoint[] vertices = data.objects[i].vertices;
                if (vertices != null)
                    count += vertices.Length;
            }

            return count;
        }

        private static float RoundMilli(float value)
        {
            return Mathf.Round(value * 1000f) * 0.001f;
        }

        private static long PackKey(float x, float z)
        {
            int ix = (int)Mathf.Round(x * 1000f);
            int iz = (int)Mathf.Round(z * 1000f);
            return ((long)ix << 32) | (uint)iz;
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "FlatMap";

            char[] invalid = Path.GetInvalidFileNameChars();
            string fileName = value.Trim();
            for (int i = 0; i < invalid.Length; i++)
                fileName = fileName.Replace(invalid[i], '_');

            return string.IsNullOrEmpty(fileName) ? "FlatMap" : fileName;
        }

        private readonly struct SourceMesh
        {
            public readonly Mesh Mesh;
            public readonly Matrix4x4 LocalToWorld;

            public SourceMesh(Mesh mesh, Matrix4x4 localToWorld)
            {
                Mesh = mesh;
                LocalToWorld = localToWorld;
            }
        }

        [Serializable]
        public sealed class FlatMapFile
        {
            public FlatMapObject[] objects;
        }

        [Serializable]
        public sealed class FlatMapObject
        {
            public string name;
            public FlatMapPoint[] vertices;
            public int[] triangles;
            public int[] outline;
        }

        [Serializable]
        public struct FlatMapPoint
        {
            public float x;
            public float z;
        }
    }
}
//#endif