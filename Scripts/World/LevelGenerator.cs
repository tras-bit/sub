// ============================================================================
//  SUBSISTENCE — World/LevelGenerator.cs
//  Процедурная генерация трёх уровней Backrooms (детерминированно по сиду):
//    Level 0 «Коридоры»   — жёлтые обои, ковролин, гудящий свет, разводы влаги;
//    Level 1 «Бассейны»   — плитка, вода разной глубины, эхо, насосные;
//    Level 2 «Станция»    — бетон, турбины, реактор с радиацией, босс-арена.
//  Геометрия собирается в ОДИН меш на уровень (MeshBuilder) → минимум draw-call'ов,
//  что критично для 100 игроков. Тут же генерируются: граф A*, точки спавна монстров,
//  узлы лута по тирам, объёмы воды/радиации, лестницы между уровнями.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Subsistence.Core;
using Subsistence.AI;

namespace Subsistence.World
{
    #region Меш-билдер
    /// <summary>Сборка одного меша из квадов с тайлингом UV (1 юнит = 1 м текстуры).</summary>
    public class MeshBuilder
    {
        readonly List<Vector3> _verts = new List<Vector3>(4096);
        readonly List<Vector3> _normals = new List<Vector3>(4096);
        readonly List<Vector2> _uvs = new List<Vector2>(4096);
        readonly List<int> _tris = new List<int>(8192);
        readonly List<Vector2> _uv2 = new List<Vector2>(4096);   // lightmap UV

        public int VertexCount => _verts.Count;

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uvScale)
        {
            int i = _verts.Count;
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            _verts.Add(a); _verts.Add(b); _verts.Add(c); _verts.Add(d);
            _normals.Add(n); _normals.Add(n); _normals.Add(n); _normals.Add(n);

            float w = (b - a).magnitude * uvScale.x;
            float h = (c - b).magnitude * uvScale.y;
            _uvs.Add(new Vector2(0, 0)); _uvs.Add(new Vector2(w, 0)); _uvs.Add(new Vector2(w, h)); _uvs.Add(new Vector2(0, h));
            _uv2.Add(new Vector2(a.x * 0.1f, a.z * 0.1f)); _uv2.Add(new Vector2(b.x * 0.1f, b.z * 0.1f));
            _uv2.Add(new Vector2(c.x * 0.1f, c.z * 0.1f)); _uv2.Add(new Vector2(d.x * 0.1f, d.z * 0.1f));

            _tris.Add(i); _tris.Add(i + 1); _tris.Add(i + 2);
            _tris.Add(i); _tris.Add(i + 2); _tris.Add(i + 3);
        }

        /// <summary>Прямоугольник пола/потолка (горизонтальная плоскость).</summary>
        public void AddFloor(float x0, float z0, float x1, float z1, float y, bool up)
        {
            var a = new Vector3(x0, y, z0); var b = new Vector3(x1, y, z0);
            var c = new Vector3(x1, y, z1); var d = new Vector3(x0, y, z1);
            if (up) AddQuad(d, c, b, a, new Vector2(0.5f, 0.5f));
            else AddQuad(a, b, c, d, new Vector2(0.5f, 0.5f));
        }

        /// <summary>Стена-отрезок с высотой.</summary>
        public void AddWall(Vector3 from, Vector3 to, float y0, float height)
            => AddQuad(new Vector3(from.x, y0, from.z), new Vector3(to.x, y0, to.z),
                       new Vector3(to.x, y0 + height, to.z), new Vector3(from.x, y0 + height, from.z),
                       new Vector2(1f, 1f));

        /// <summary>Толстая стена (плита) — чтобы не было видно «бумагу» с обратной стороны.</summary>
        public void AddWallSlab(Vector3 from, Vector3 to, float y0, float height, float thickness = 0.2f)
        {
            Vector3 dir = (to - from).normalized;
            Vector3 off = Vector3.Cross(dir, Vector3.up) * (thickness * 0.5f);
            AddQuad(new Vector3(from.x, y0, from.z) - off, new Vector3(to.x, y0, to.z) - off,
                    new Vector3(to.x, y0 + height, to.z) - off, new Vector3(from.x, y0 + height, from.z) - off, new Vector2(1f, 1f));
            AddQuad(new Vector3(to.x, y0, to.z) + off, new Vector3(from.x, y0, from.z) + off,
                    new Vector3(from.x, y0 + height, from.z) + off, new Vector3(to.x, y0 + height, to.z) + off, new Vector2(1f, 1f));
            // торцы
            AddQuad(new Vector3(to.x - off.x, y0, to.z - off.z), new Vector3(to.x + off.x, y0, to.z + off.z),
                    new Vector3(to.x + off.x, y0 + height, to.z + off.z), new Vector3(to.x - off.x, y0 + height, to.z - off.z), new Vector2(1f, 1f));
            AddQuad(new Vector3(from.x + off.x, y0, from.z + off.z), new Vector3(from.x - off.x, y0, from.z - off.z),
                    new Vector3(from.x - off.x, y0 + height, from.z - off.z), new Vector3(from.x + off.x, y0 + height, from.z + off.z), new Vector2(1f, 1f));
        }

        public Mesh Build(string name, bool addCollider = true)
        {
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(_verts);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.SetUVs(1, _uv2);
            mesh.SetTriangles(_tris, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        public void Clear() { _verts.Clear(); _normals.Clear(); _uvs.Clear(); _uv2.Clear(); _tris.Clear(); }
    }
    #endregion

    #region Окружение (вода, свет, шум, радиация)
    /// <summary>
    /// Мировые запросы, которые нужны выживанию и ИИ: глубина воды, темнота,
    /// уровень шума, радиация. Заполняется генератором уровней.
    /// </summary>
    public static class WorldEnvironment
    {
        public struct WaterVolume { public Bounds bounds; public float surfaceY; }

        public static readonly List<WaterVolume> Water = new List<WaterVolume>(64);
        public static readonly List<RadiationVolume> Radiation = new List<RadiationVolume>(32);
        public static readonly List<LightZone> Lights = new List<LightZone>(512);

        public struct RadiationVolume
        {
            public Bounds bounds; public float radsPerSecond; public string label;
        }

        public struct LightZone
        {
            public Vector3 position; public float radius; public float intensity;
        }

        public static void Clear() { Water.Clear(); Radiation.Clear(); Lights.Clear(); }

        public static float SampleWaterDepth(Vector3 p)
        {
            float depth = 0f;
            for (int i = 0; i < Water.Count; i++)
            {
                var w = Water[i];
                if (p.x < w.bounds.min.x || p.x > w.bounds.max.x || p.z < w.bounds.min.z || p.z > w.bounds.max.z) continue;
                if (p.y > w.surfaceY || p.y < w.bounds.min.y) continue;
                depth = Mathf.Max(depth, w.surfaceY - p.y);
            }
            return depth;
        }

        public static bool IsUnderwater(Vector3 p) => SampleWaterDepth(p) > 0.05f;

        /// <summary>0..1 — освещённость точки (рассудок падает в темноте).</summary>
        public static float LightLevelAt(Vector3 p)
        {
            float best = 0f;
            for (int i = 0; i < Lights.Count; i++)
            {
                var l = Lights[i];
                float d = Vector3.Distance(l.position, p);
                if (d > l.radius) continue;
                best = Mathf.Max(best, l.intensity * (1f - d / l.radius));
            }
            return Mathf.Clamp01(best);
        }

        /// <summary>0..1 — «громкость» места (в машинном зале станции тихо не спрячешься).</summary>
        public static float NoiseLevelAt(Vector3 p)
        {
            float level = 0f;
            for (int i = 0; i < NoiseSources.Count; i++)
            {
                var s = NoiseSources[i];
                float d = Vector3.Distance(s.position, p);
                if (d > s.radius) continue;
                level = Mathf.Max(level, s.intensity * (1f - d / s.radius));
            }
            return Mathf.Clamp01(level);
        }

        public struct NoiseSource { public Vector3 position; public float radius; public float intensity; }
        public static readonly List<NoiseSource> NoiseSources = new List<NoiseSource>(64);

        public static float RadiationAt(Vector3 p)
        {
            float rads = 0f;
            for (int i = 0; i < Radiation.Count; i++)
            {
                var r = Radiation[i];
                if (!r.bounds.Contains(p)) continue;
                rads += r.radsPerSecond;
            }
            return rads;
        }
    }

    /// <summary>Источник радиации (реактор, ТВЭЛ, лужа охлаждающей жидкости).</summary>
    public class RadiationEmitter : MonoBehaviour, Subsistence.Player.IRadiationSource
    {
        public float radsPerSecond = 4f;
        public float radius = 12f;
        public float falloffPower = 1.6f;
        public string label = "реактор";

        public float RadsPerSecondAt(Vector3 worldPos)
        {
            float d = Vector3.Distance(worldPos, transform.position);
            if (d > radius) return 0f;
            return radsPerSecond * Mathf.Pow(1f - d / radius, falloffPower);
        }
    }
    #endregion

    [Serializable]
    public class LevelGenSettings
    {
        public LevelTheme theme;
        public int seed = 1337;
        public int gridWidth = 24;         // в клетках (клетка 6 м)
        public int gridDepth = 24;
        public float cellSize = 6f;
        public float wallHeight = 3.2f;
        public float originY = 0f;
        public int roomCount = 14;
        public int monsterCount = 8;
        public int lootNodeCount = 28;
        public int exitCount = 3;
        public int keycardsRequired = 1;
    }

    /// <summary>Результат генерации — всё, что нужно рантайму.</summary>
    public class GeneratedLevel
    {
        public LevelTheme theme;
        public GameObject root;
        public Pathfinder pathfinder = new Pathfinder();
        public List<Vector3> lootNodes = new List<Vector3>();
        public List<Vector3> monsterSpawns = new List<Vector3>();
        public List<Vector3> exits = new List<Vector3>();
        public List<Vector3> playerSpawns = new List<Vector3>();
        public List<Vector3> buildingZones = new List<Vector3>();   // точки интереса (стройка разрешена везде, 25_build_zones)
        public int seed;
    }

    /// <summary>Процедурный генератор мира (детерминированный: одинаковый seed → одинаковая карта).</summary>
    public class LevelGenerator : MonoBehaviour
    {
        [Header("Материалы (назначаются RuntimeBootstrap или вручную)")]
        public Material wallpaperMat;      // жёлтые обои L0
        public Material carpetMat;         // ковролин L0
        public Material ceilingMat;        // потолочные панели L0
        public Material tileMat;           // плитка L1
        public Material poolWaterMat;      // вода L1
        public Material concreteMat;       // бетон L2
        public Material metalMat;          // металл (трубы, турбины)
        public Material emissiveLampMat;   // лампы
        public Material reactorMat;

        public LevelGenSettings[] levels;
        public bool buildOnStart = true;
        public List<GeneratedLevel> Generated { get; private set; } = new List<GeneratedLevel>();

        void Start()
        {
            if (buildOnStart) GenerateAll();
        }

        public void GenerateAll()
        {
            WorldEnvironment.Clear();
            Generated.Clear();
            var parent = new GameObject("GeneratedWorld").transform;
            for (int i = 0; i < levels.Length; i++)
                Generated.Add(Generate(levels[i], parent));
            // после генерации — соединяем уровни (лестничные шахты/лифты)
            ConnectLevels(Generated);
        }

        /// <summary>Генерация одного уровня по его сеттингу.</summary>
        public GeneratedLevel Generate(LevelGenSettings s, Transform parent)
        {
            var rng = new System.Random(s.seed);
            _propRng = rng;                                   // пропсы крутятся тем же сидом
            var lvl = new GeneratedLevel { theme = s.theme, seed = s.seed };
            var root = new GameObject($"Level_{(int)s.theme}_{s.theme}") { transform = { parent = parent } };
            root.transform.position = new Vector3(0, s.originY, 0);
            lvl.root = root;

            // Своя палитра и габариты уровня: система атмосферы берёт тему отсюда
            var zone = root.AddComponent<LevelZone>();
            zone.theme = s.theme;
            zone.halfExtents = new Vector3(s.gridWidth * s.cellSize * 0.5f, 12f, s.gridDepth * s.cellSize * 0.5f);

            // 1) Планировка: случайное блуждание по сетке + комнаты
            bool[,] solid = new bool[s.gridWidth, s.gridDepth];
            for (int x = 0; x < s.gridWidth; x++) for (int z = 0; z < s.gridDepth; z++) solid[x, z] = true;

            var rooms = new List<RectInt>();
            for (int r = 0; r < s.roomCount; r++)
            {
                int rw = rng.Next(3, 7), rd = rng.Next(3, 7);
                int rx = rng.Next(1, Mathf.Max(2, s.gridWidth - rw - 1));
                int rz = rng.Next(1, Mathf.Max(2, s.gridDepth - rd - 1));
                var rect = new RectInt(rx, rz, rw, rd);
                rooms.Add(rect);
                for (int x = rx; x < rx + rw; x++) for (int z = rz; z < rz + rd; z++) solid[x, z] = false;
                // соединяем комнаты L-коридорами
                if (rooms.Count > 1)
                {
                    var prev = rooms[rng.Next(0, rooms.Count - 1)];
                    CarveCorridor(solid, new Vector2Int(Mathf.RoundToInt(prev.center.x), Mathf.RoundToInt(prev.center.y)),
                                  new Vector2Int(Mathf.RoundToInt(rect.center.x), Mathf.RoundToInt(rect.center.y)), rng);
                }
            }
            // стартовый «лабиринт»: гарантированные длинные коридоры (Backrooms-ощущение)
            for (int i = 0; i < 6; i++)
            {
                int x = rng.Next(1, s.gridWidth - 1);
                for (int z = 1; z < s.gridDepth - 1; z++) solid[x, z] = false;
                int zz = rng.Next(1, s.gridDepth - 1);
                for (int xx = 1; xx < s.gridWidth - 1; xx++) solid[xx, zz] = false;
            }

            // 2) Геометрия: один меш на уровень
            var mb = new MeshBuilder();
            float cs = s.cellSize, h = s.wallHeight;
            for (int x = 0; x < s.gridWidth; x++)
                for (int z = 0; z < s.gridDepth; z++)
                {
                    if (solid[x, z]) continue;
                    float x0 = x * cs, z0 = z * cs, x1 = x0 + cs, z1 = z0 + cs;

                    // пол + потолок
                    mb.AddFloor(x0, z0, x1, z1, 0f, true);
                    mb.AddFloor(x0, z0, x1, z1, h, false);

                    // стены там, где сосед — «камень»
                    if (x == 0 || solid[x - 1, z]) mb.AddWallSlab(new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), 0f, h, 0.25f);
                    if (x == s.gridWidth - 1 || solid[x + 1, z]) mb.AddWallSlab(new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), 0f, h, 0.25f);
                    if (z == 0 || solid[x, z - 1]) mb.AddWallSlab(new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), 0f, h, 0.25f);
                    if (z == s.gridDepth - 1 || solid[x, z + 1]) mb.AddWallSlab(new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), 0f, h, 0.25f);

                    // узел графа для ИИ: центр клетки (каждые 2 клетки — чтобы граф не раздувался)
                    if (x % 3 == 0 && z % 3 == 0)   // 1×1 км: узлы реже, иначе граф слишком тяжёлый
                        lvl.pathfinder.AddNode(new Vector3(x0 + cs * 0.5f, 0.1f, z0 + cs * 0.5f), cs * 2.6f);
                }

            // 3) Колонны/пальцы в больших комнатах (как в референсе коридоров)
            foreach (var room in rooms)
            {
                for (int x = room.xMin + 1; x < room.xMax; x += 3)
                    for (int z = room.yMin + 1; z < room.yMax; z += 3)
                    {
                        float px = x * cs + cs * 0.5f, pz = z * cs + cs * 0.5f;
                        if (solid[x, z]) continue;
                        AddPillar(mb, new Vector3(px, 0, pz), h, 0.9f);
                    }
            }

            var meshGo = new GameObject("Geometry");
            meshGo.transform.SetParent(root.transform, false);
            var mf = meshGo.AddComponent<MeshFilter>();
            var mr = meshGo.AddComponent<MeshRenderer>();
            mf.sharedMesh = mb.Build($"Level{ (int)s.theme }_Mesh");
            mr.sharedMaterial = MaterialFor(s.theme, Surface.Room);
            meshGo.layer = Layers.LevelGeometry;
            var col = meshGo.AddComponent<MeshCollider>();
            col.sharedMesh = mf.sharedMesh;
            meshGo.isStatic = true;

            // 4) Тематические добавки
            switch (s.theme)
            {
                case LevelTheme.Corridors: DecorateCorridors(lvl, s, rooms, rng, root); break;
                case LevelTheme.Poolrooms: DecoratePoolrooms(lvl, s, rooms, rng, root); break;
                case LevelTheme.PowerStation: DecoratePowerStation(lvl, s, rooms, rng, root); break;
            }

            // 5) Точки лута/спавна/выходов/зон стройки
            FillPoints(lvl, s, rooms, rng);
            return lvl;
        }

        enum Surface { Room, Water, Metal, Emissive }

        Material MaterialFor(LevelTheme theme, Surface surface)
        {
            switch (theme)
            {
                case LevelTheme.Corridors: return surface == Surface.Emissive ? emissiveLampMat : (surface == Surface.Water ? carpetMat : wallpaperMat);
                case LevelTheme.Poolrooms: return surface == Surface.Water ? poolWaterMat : tileMat;
                default: return surface == Surface.Metal ? metalMat : concreteMat;
            }
        }

        static void CarveCorridor(bool[,] solid, Vector2Int from, Vector2Int to, System.Random rng)
        {
            int x = from.x, z = from.y;
            while (x != to.x) { if (x >= 0 && x < solid.GetLength(0) && z >= 0 && z < solid.GetLength(1)) solid[x, z] = false; x += Math.Sign(to.x - x); }
            while (z != to.y) { if (x >= 0 && x < solid.GetLength(0) && z >= 0 && z < solid.GetLength(1)) solid[x, z] = false; z += Math.Sign(to.y - z); }
        }

        void AddPillar(MeshBuilder mb, Vector3 center, float height, float width)
        {
            float w = width * 0.5f;
            mb.AddWallSlab(center + new Vector3(-w, 0, -w), center + new Vector3(-w, 0, w), 0f, height, w * 2f);
            mb.AddWallSlab(center + new Vector3(w, 0, -w), center + new Vector3(w, 0, w), 0f, height, w * 2f);
            mb.AddWallSlab(center + new Vector3(-w, 0, -w), center + new Vector3(w, 0, -w), 0f, height, w * 2f);
            mb.AddWallSlab(center + new Vector3(-w, 0, w), center + new Vector3(w, 0, w), 0f, height, w * 2f);
        }

        // ---------------- LEVEL 0: жёлтые коридоры ----------------
        void DecorateCorridors(GeneratedLevel lvl, LevelGenSettings s, List<RectInt> rooms, System.Random rng, GameObject root)
        {
            // Лампы «офисной» панели — решётка как в референсе (каждые 6 м по сетке)
            for (float x = 3f; x < s.gridWidth * s.cellSize; x += 12f)
                for (float z = 3f; z < s.gridDepth * s.cellSize; z += 12f)
                {
                    var pos = new Vector3(x, s.wallHeight - 0.05f, z);
                    WorldEnvironment.Lights.Add(new WorldEnvironment.LightZone { position = pos, radius = 9f, intensity = 0.85f });
                    SpawnLampFixture(root, pos, new Vector3(1.2f, 0.08f, 0.6f));
                }
            // Гул ламп (постоянный шумовой фон — маскирует шаги игрока)
            WorldEnvironment.NoiseSources.Add(new WorldEnvironment.NoiseSource { position = new Vector3(s.gridWidth * s.cellSize * 0.5f, 0, s.gridDepth * s.cellSize * 0.5f), radius = 500f, intensity = 0.18f });

            // Свои вещи уровня L0: стулья, картотека, кулер, коробки, швабра, вентилятор, таблички
            var l0 = World.LevelProps.For(LevelTheme.Corridors);
            for (int i = 0; i < 16; i++)
            {
                var p = RandomPointInRooms(rooms, s, rng);
                SpawnProp(root, l0[rng.Next(l0.Length)], p + Vector3.up * 0.6f, new Vector3(1.0f, 1.1f, 1.0f));
            }

            // Мокрые пятна, лужи, плесень, «выход» на Level 1
            for (int i = 0; i < 6; i++)
            {
                var p = RandomPointInRooms(rooms, s, rng);
                lvl.exits.Add(p);
            }
        }

        // ---------------- LEVEL 1: бассейны ----------------
        void DecoratePoolrooms(GeneratedLevel lvl, LevelGenSettings s, List<RectInt> rooms, System.Random rng, GameObject root)
        {
            // Заливаем часть комнат водой: глубина от 0.4 до 2.6 м
            foreach (var room in rooms)
            {
                if (rng.NextDouble() > 0.45) continue;
                float x0 = room.xMin * s.cellSize, z0 = room.yMin * s.cellSize;
                float x1 = room.xMax * s.cellSize, z1 = room.yMax * s.cellSize;
                float depth = (float)(0.4 + rng.NextDouble() * 2.2);
                var bounds = new Bounds(new Vector3((x0 + x1) * 0.5f + s.originY * 0, s.originY - depth * 0.5f, (z0 + z1) * 0.5f),
                                        new Vector3(x1 - x0, depth, z1 - z0));
                WorldEnvironment.Water.Add(new WorldEnvironment.WaterVolume { bounds = bounds, surfaceY = s.originY });

                // Меш воды (полупрозрачный материал)
                var waterGo = new GameObject("WaterVolume");
                waterGo.transform.SetParent(root.transform, false);
                waterGo.transform.position = new Vector3((x0 + x1) * 0.5f, s.originY - depth * 0.5f, (z0 + z1) * 0.5f);
                var bc = waterGo.AddComponent<BoxCollider>();
                bc.size = new Vector3(x1 - x0, depth, z1 - z0);
                bc.isTrigger = true;
                waterGo.layer = Layers.Water;
                var wmr = waterGo.AddComponent<MeshRenderer>();
                var wmf = waterGo.AddComponent<MeshFilter>();
                wmf.sharedMesh = BuildBoxMesh(new Vector3(x1 - x0, depth, z1 - z0));
                wmr.sharedMaterial = poolWaterMat;
                waterGo.AddComponent<PoolWater>().surfaceY = s.originY;
            }

            // Насосные и трубы + эхо-источники
            for (int i = 0; i < 10; i++)
            {
                var p = RandomPointInRooms(rooms, s, rng);
                SpawnProp(root, "Pipe", p + Vector3.up * 2.6f, new Vector3(0.25f, 0.25f, 6f));
                WorldEnvironment.NoiseSources.Add(new WorldEnvironment.NoiseSource { position = p, radius = 18f, intensity = 0.35f });
            }

            // Свои вещи уровня L37: лестницы, круги, лежаки, насосы, вентили, лейки, знаки
            var l37 = World.LevelProps.For(LevelTheme.Poolrooms);
            for (int i = 0; i < 16; i++)
            {
                var p = RandomPointInRooms(rooms, s, rng);
                SpawnProp(root, l37[rng.Next(l37.Length)], p + Vector3.up * 0.4f, new Vector3(1.0f, 1.2f, 1.0f));
            }
        }

        // ---------------- LEVEL 2: электростанция ----------------
        void DecoratePowerStation(GeneratedLevel lvl, LevelGenSettings s, List<RectInt> rooms, System.Random rng, GameObject root)
        {
            // Реакторный зал в самой большой комнате + зона радиации и босс-арена
            RectInt reactorRoom = rooms.Count > 0 ? rooms[0] : new RectInt(2, 2, 6, 6);
            foreach (var r in rooms) if (r.width * r.height > reactorRoom.width * reactorRoom.height) reactorRoom = r;

            float cx = (reactorRoom.xMin + reactorRoom.xMax) * 0.5f * s.cellSize;
            float cz = (reactorRoom.yMin + reactorRoom.yMax) * 0.5f * s.cellSize;
            var reactorPos = new Vector3(cx, s.originY + 1.2f, cz);

            SpawnProp(root, "Reactor", reactorPos, new Vector3(4f, 5f, 4f), reactorMat);
            var emitter = new GameObject("ReactorRadiation");
            emitter.transform.SetParent(root.transform, false);
            emitter.transform.position = reactorPos;
            var re = emitter.AddComponent<RadiationEmitter>();
            re.radsPerSecond = 12f; re.radius = 26f; re.label = "реактор";
            WorldEnvironment.Radiation.Add(new WorldEnvironment.RadiationVolume
            {
                bounds = new Bounds(reactorPos, new Vector3(26f, 8f, 26f)),
                radsPerSecond = 6f,
                label = "реакторный зал"
            });
            lvl.monsterSpawns.Add(reactorPos + new Vector3(8f, 0, 8f));    // арена босса

            // Турбины, генераторы, щитовые, кабели
            for (int i = 0; i < 14; i++)
            {
                var p = RandomPointInRooms(rooms, s, rng);
                SpawnProp(root, "Turbine", p, new Vector3(3f, 2.2f, 2f), metalMat);
                if (i % 3 == 0)
                {
                    var rad = new GameObject("RadSource_Spill");
                    rad.transform.SetParent(root.transform, false);
                    rad.transform.position = p;
                    var r2 = rad.AddComponent<RadiationEmitter>();
                    r2.radsPerSecond = 4f; r2.radius = 8f; r2.label = "разлив ОЖ";
                    WorldEnvironment.Radiation.Add(new WorldEnvironment.RadiationVolume
                    { bounds = new Bounds(p, new Vector3(8f, 4f, 8f)), radsPerSecond = 4f, label = "разлив" });
                }
            }

            // Свои вещи уровня L3: щиты, рубильники, трансформаторы, вентили, баки, барабаны, знаки
            var l3 = World.LevelProps.For(LevelTheme.PowerStation);
            for (int i = 0; i < 16; i++)
            {
                var p = RandomPointInRooms(rooms, s, rng);
                SpawnProp(root, l3[rng.Next(l3.Length)], p, new Vector3(1.6f, 1.9f, 1.6f));
            }

            // Бетонный гул
            WorldEnvironment.NoiseSources.Add(new WorldEnvironment.NoiseSource { position = reactorPos, radius = 60f, intensity = 0.5f });

            // Выход-«нулевой»: лифт на верхние уровни (для возврата с лутом)
            lvl.exits.Add(reactorPos + new Vector3(-10f, 0, -10f));
        }

        void FillPoints(GeneratedLevel lvl, LevelGenSettings s, List<RectInt> rooms, System.Random rng)
        {
            // узлы лута (тир зависит от уровня — см. LootSpawner)
            for (int i = 0; i < s.lootNodeCount; i++) lvl.lootNodes.Add(RandomPointInRooms(rooms, s, rng));
            // спавны монстров (подальше от точек входа)
            for (int i = 0; i < s.monsterCount; i++) lvl.monsterSpawns.Add(RandomPointInRooms(rooms, s, rng));
            // спавны игроков
            for (int i = 0; i < 8; i++) lvl.playerSpawns.Add(RandomPointInRooms(rooms, s, rng));
            // 25_build_zones: строить можно везде — список зон оставлен только как «точки интереса»
            foreach (var r in rooms)
                lvl.buildingZones.Add(new Vector3((r.xMin + r.width * 0.5f) * s.cellSize, s.originY, (r.yMin + r.height * 0.5f) * s.cellSize));
            if (lvl.exits.Count == 0)
                for (int i = 0; i < s.exitCount; i++) lvl.exits.Add(RandomPointInRooms(rooms, s, rng));
        }

        static Vector3 RandomPointInRooms(List<RectInt> rooms, LevelGenSettings s, System.Random rng)
        {
            if (rooms.Count == 0) return new Vector3(s.cellSize, s.originY, s.cellSize);
            var r = rooms[rng.Next(0, rooms.Count)];
            float x = (r.xMin + (float)rng.NextDouble() * r.width) * s.cellSize;
            float z = (r.yMin + (float)rng.NextDouble() * r.height) * s.cellSize;
            return new Vector3(x, s.originY + 0.1f, z);
        }

        /// <summary>
        /// Переходы между уровнями — ЛИФТЫ ПО КЛЮЧ-КАРТАМ (решение 09_connect_levels):
        /// вверх нужна карта целевого уровня (зелёная → Level 37, синяя → Level 3),
        /// вниз — свободно (возврат с лутом). Шахты/провалы больше не используются.
        /// </summary>
        void ConnectLevels(List<GeneratedLevel> levels)
        {
            for (int i = 0; i + 1 < levels.Count; i++)
            {
                var from = levels[i];
                var to = levels[i + 1];
                if (from.exits.Count == 0 || to.playerSpawns.Count == 0) continue;

                // лифт вверх: только с ключ-картой
                var upGo = new GameObject($"Elevator_{from.theme}_to_{to.theme}");
                upGo.transform.SetParent(from.root.transform, false);
                upGo.transform.position = from.exits[0] + Vector3.up * 0.05f;
                var up = upGo.AddComponent<Subsistence.World.LevelElevator>();
                up.Setup(from.theme, to.theme, KeycardFor(to.theme), true);
                BuildElevatorCage(upGo.transform);

                // лифт вниз: свободный возврат
                var downGo = new GameObject($"Elevator_{to.theme}_down");
                downGo.transform.SetParent(to.root.transform, false);
                downGo.transform.position = to.playerSpawns[0] + Vector3.up * 0.05f;
                var down = downGo.AddComponent<Subsistence.World.LevelElevator>();
                down.Setup(to.theme, from.theme, null, false);
                BuildElevatorCage(downGo.transform);
            }
        }

        static string KeycardFor(LevelTheme target)
            => target == LevelTheme.Poolrooms ? "tool.keycard.green"
             : target == LevelTheme.PowerStation ? "tool.keycard.blue"
             : "tool.keycard.red";

        /// <summary>
        /// Кабина лифта: модель BD_elevator_car из Blender (решётчатые стены, поручни, лампа),
        /// а куб остаётся невидимым коллайдером-прокси (по нему игрок «заходит» в лифт).
        /// </summary>
        void BuildElevatorCage(Transform parent)
        {
            var cage = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cage.name = "Cage";
            cage.transform.SetParent(parent, false);
            cage.transform.localScale = new Vector3(2.6f, 3.0f, 2.6f);
            cage.transform.localPosition = new Vector3(0, 1.5f, 0);
            var mat = metalMat != null ? metalMat : null;
            var r = cage.GetComponent<Renderer>();
            if (mat != null) r.sharedMaterial = mat;
            if (cage.GetComponent<Collider>() == null) cage.AddComponent<BoxCollider>();

            var cabin = World.ModelLibrary.AttachFitted(World.ModelLibrary.Names.ElevatorCar, parent, 3.0f, 0f);
            if (cabin != null) r.enabled = false;      // кабина из модели, куб — только коллайдер

            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "KeycardPanel";
            panel.transform.SetParent(parent, false);
            panel.transform.localScale = new Vector3(0.25f, 0.4f, 0.12f);
            panel.transform.localPosition = new Vector3(1.45f, 1.3f, 0);
            if (emissiveLampMat != null) panel.GetComponent<Renderer>().sharedMaterial = emissiveLampMat;
            Destroy(panel.GetComponent<Collider>());

            var sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sign.name = "LevelSign";
            sign.transform.SetParent(parent, false);
            sign.transform.localScale = new Vector3(1.6f, 0.4f, 0.06f);
            sign.transform.localPosition = new Vector3(0, 3.2f, 1.2f);
            if (emissiveLampMat != null) sign.GetComponent<Renderer>().sharedMaterial = emissiveLampMat;
            Destroy(sign.GetComponent<Collider>());
        }

        void SpawnLampFixture(GameObject root, Vector3 pos, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "CeilingLamp";
            go.transform.SetParent(root.transform, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            if (emissiveLampMat != null) r.sharedMaterial = emissiveLampMat;
            go.isStatic = true;
        }

        System.Random _propRng = new System.Random(1337);   // повороты пропсов (детерминированно от сида)

        void SpawnProp(GameObject root, string name, Vector3 pos, Vector3 scale, Material mat = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root.transform, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (mat != null) r.sharedMaterial = mat;

            // Реальная модель из Blender: куб остаётся коллайдером/фолбэком, меш ставится рядом
            // (без масштаба куба — иначе модель деформируется).
            if (World.ModelLibrary.Has(name))
            {
                var holder = new GameObject(name + "_model");
                holder.transform.SetParent(go.transform.parent, false);
                holder.transform.position = pos;
                holder.transform.rotation = Quaternion.Euler(0f, (float)_propRng.NextDouble() * 360f, 0f);
                float h = Mathf.Max(scale.x, scale.y, scale.z);
                var visual = World.ModelLibrary.AttachFitted(name, holder.transform, h, 0f);
                if (visual != null && r != null) r.enabled = false;
            }
            go.isStatic = true;
        }

        /// <summary>Простой боксовый меш для воды.</summary>
        public static Mesh BuildBoxMesh(Vector3 size)
        {
            var mb = new MeshBuilder();
            float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
            mb.AddQuad(new Vector3(-hx, hy, -hz), new Vector3(hx, hy, -hz), new Vector3(hx, hy, hz), new Vector3(-hx, hy, hz), Vector2.one);
            mb.AddQuad(new Vector3(-hx, -hy, hz), new Vector3(hx, -hy, hz), new Vector3(hx, -hy, -hz), new Vector3(-hx, -hy, -hz), Vector2.one);
            mb.AddQuad(new Vector3(-hx, -hy, -hz), new Vector3(-hx, -hy, hz), new Vector3(-hx, hy, hz), new Vector3(-hx, hy, -hz), Vector2.one);
            mb.AddQuad(new Vector3(hx, -hy, hz), new Vector3(hx, -hy, -hz), new Vector3(hx, hy, -hz), new Vector3(hx, hy, hz), Vector2.one);
            mb.AddQuad(new Vector3(-hx, -hy, hz), new Vector3(-hx, hy, hz), new Vector3(hx, hy, hz), new Vector3(hx, -hy, hz), Vector2.one);
            mb.AddQuad(new Vector3(hx, -hy, -hz), new Vector3(hx, hy, -hz), new Vector3(-hx, hy, -hz), new Vector3(-hx, -hy, -hz), Vector2.one);
            return mb.Build("WaterBox");
        }
    }

    /// <summary>Вода в бассейнах: подводный эффект, замедление, шум брызг.</summary>
    public class PoolWater : MonoBehaviour
    {
        public float surfaceY;
        void OnTriggerEnter(Collider other)
        {
            NoiseSystem.Emit(other.transform.position, 20f, NoiseType.Footstep);
        }
    }

    /// <summary>Локальный «дышащий» эффект лампы: мигание, гул (клиентская атмосфера).</summary>
    public class FlickeringLight : MonoBehaviour
    {
        public float baseIntensity = 1f;
        public float flickerChance = 0.02f;
        Light _light;
        void Awake() => _light = GetComponent<Light>();
        void Update()
        {
            if (_light == null) return;
            if (UnityEngine.Random.value < flickerChance * Time.deltaTime)
                _light.intensity = baseIntensity * UnityEngine.Random.Range(0.3f, 1f);
            else
                _light.intensity = Mathf.Lerp(_light.intensity, baseIntensity, Time.deltaTime * 3f);
        }
    }
}
