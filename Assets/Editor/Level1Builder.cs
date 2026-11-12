using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Xunyi.Level;

namespace Xunyi.EditorTools
{
    /// <summary>
    /// 第一关：西安大雁塔 · 六重飞檐回廊
    ///
    /// 防穿模的几何根因解法：所有旋转件都以"塔中轴"为旋转轴，是套在塔外的
    /// 90° 环廊段 + 两条径向悬臂（局部夹角 90°）。绕中轴旋转保半径，
    /// 旋转件占据的环带 [9.5, 19.8] 与塔的最大外径 7.78 之间有约 1.7 的安全缝，
    /// 任意角度都不可能互相插入。
    ///
    /// 行走路径：沿真实踏面密采样（斜臂 6 点、环廊 5 点、平臂 4 点），
    /// 采样点是旋转层的子物体，跟着一起转，角色因此严格走在路上。
    /// </summary>
    public static class Level1Builder
    {
        private const string ScenePath = "Assets/Scenes/Level1.unity";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        private const string ArtFolder = "Assets/Art/UI";
        private const string LevelImageSource = @"E:\UNITY\寻遗\脸谱\第一关.jpg";
        private const string LevelImagePath = ArtFolder + "/第一关.jpg";

        private const string MatFolder = "Assets/Materials/Level1";
        private const string MeshFolder = "Assets/Meshes";

        private const string BgmSource = @"E:\UNITY\寻遗\音乐\背景音乐.mp3";
        private const string AudioFolder = "Assets/Audio";
        private const string BgmPath = AudioFolder + "/背景音乐.mp3";

        private const int TierCount = 6;

        // ---- 塔 ----
        private const float TowerTop = 47f;
        private const float TowerMaxRadius = 7.8f;

        // ---- 旋转飞檐层 ----
        private const float RingInner = 9.5f;
        private const float RingOuter = 12.5f;
        private const float RingMid = (RingInner + RingOuter) * 0.5f;
        private const float ArmOuter = 19.8f;
        private const float ArmWidth = 4.0f;
        private const float TierThickness = 0.7f;
        private const float ArcSpan = 90f;
        private const float Step = 4.5f;

        // ---- 落脚台 ----
        private const float LandingRadius = 21.9f;
        private const float LandingSize = 4.0f;
        private const float LandingTop = 0.4f;

        // ---- 角色 ----
        private const float CharacterModelHeight = 1.32f;
        private const float CharacterTargetHeight = 4.64f;

        private static readonly float[] ArmBeta = { 0f, 45f, 90f, 135f, 0f, 45f };
        private static readonly float[] LandingAngle = { 180f, 270f, 0f, 90f, 180f, 270f, 0f };
        private static readonly float[] StartAngle = { 115f, -70f, -30f, 15f, 115f, -70f };

        [MenuItem("Tools/寻遗/构建第一关")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Level1Builder] 正在 Play 模式（或切换中），请先停止运行再构建。");
                return;
            }

            EnsureFolders();

            Material matDark = GetMaterial("塔身深褐", "#726251");
            Material matSand = GetMaterial("塔身土黄", "#A4815D");
            Material matRed = GetMaterial("朱红", "#8B0001");
            Material matGround = GetMaterial("地面", "#726251");
            Material matWhite = GetMaterial("角色白", "#F5F5F5");
            Material matGray = GetMaterial("角色浅灰", "#D2D2D2");

            Sprite levelArt = ImportSprite(LevelImageSource, LevelImagePath);

            UnityEngine.SceneManagement.Scene scene =
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateLighting();

            GameObject root = new GameObject("Level1_Root");
            Box(root.transform, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(140f, 1f, 140f), matGround);

            GameObject tower = new GameObject("DaYanPagoda");
            tower.transform.SetParent(root.transform, false);
            BuildPagoda(tower.transform, matDark, matSand, matRed);

            // ---------- 落脚台 ----------
            Transform[] landings = new Transform[7];
            for (int i = 0; i < 7; i++)
            {
                landings[i] = BuildLanding(root.transform, "Landing_" + (i + 1),
                    LandingAngle[i], 5f + Step * i, matSand, matRed, matDark);
            }

            // ---------- 六层旋转飞檐 ----------
            RotatingRamp[] tiers = new RotatingRamp[TierCount];
            List<Transform> nodes = new List<Transform>();
            List<int> gates = new List<int>();

            nodes.Add(MakeNode(root.transform, "Node_L1", landings[0].position + new Vector3(0f, LandingTop, 0f)));
            gates.Add(-1);

            for (int t = 0; t < TierCount; t++)
            {
                float hBase = 5f + Step * t;
                float hRing = hBase + Step;

                RotatingRamp tier = BuildTier(root.transform, "Tier_" + (t + 1), hBase, hRing, ArmBeta[t],
                    matRed, matSand);
                tiers[t] = tier;

                List<Transform> pts = BuildTierPath(tier.transform, ArmBeta[t], hBase, hRing);

                // 进入该层的第一段由该层把守
                gates[gates.Count - 1] = t;
                for (int i = 0; i < pts.Count; i++)
                {
                    nodes.Add(pts[i]);
                    gates.Add(-1);
                }

                nodes.Add(MakeNode(root.transform, "Node_L" + (t + 2),
                    landings[t + 1].position + new Vector3(0f, LandingTop, 0f)));
                gates.Add(-1);

                float target = Normalize(LandingAngle[t] - ArmBeta[t]);
                tier.Configure(StartAngle[t], target);
            }

            // ---------- 角色 ----------
            Vector3 spawn = landings[0].position + new Vector3(0f, LandingTop, 0f);
            GameObject player = BuildCharacter(root.transform, spawn, matWhite);
            CharacterWalker walker = player.AddComponent<CharacterWalker>();
            SetFloat(walker, "_speed", 15f);

            // ---------- 相机 ----------
            Bounds designBounds = ComputeDesignBounds();
            Camera camera = BuildCamera(root.transform, designBounds);
            CameraOrbit orbit = camera.gameObject.AddComponent<CameraOrbit>();
            orbit.Configure(designBounds.center, 320f, 26f, 45f);

            CreateAudioManager(ImportBgm());

            // ---------- UI ----------
            Canvas canvas = BuildCanvas();
            LevelIntro intro = BuildIntro(canvas.transform);
            VictoryPanel victory = BuildVictory(canvas.transform, levelArt);
            BuildHint(canvas.transform);
            BuildBrightnessOverlay(canvas.transform);

            // ---------- 总控 ----------
            GameObject directorGo = new GameObject("Level1_Director");
            directorGo.transform.SetParent(root.transform, false);
            Level1Director director = directorGo.AddComponent<Level1Director>();

            SetRef(director, "_walker", walker);
            SetRefArray(director, "_nodes", nodes.ToArray());
            SetRefArray(director, "_ramps", new Object[] { tiers[0], tiers[1], tiers[2], tiers[3], tiers[4], tiers[5] });
            SetRef(director, "_intro", intro);
            SetRef(director, "_victory", victory);
            SetIntArray(director, "_segmentGate", gates.ToArray());

            AddTierColliders(tiers);

            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterScenesInBuildSettings();

            Debug.Log("[Level1Builder] 六重飞檐回廊构建完成：节点 " + nodes.Count + "，旋转层 " + TierCount
                + "，落脚台 7，正交 size=" + camera.orthographicSize.ToString("F1")
                + "，角色高 " + CharacterTargetHeight);

            EditorSceneManager.OpenScene(SampleScenePath);
        }

        private static float Normalize(float deg)
        {
            while (deg > 180f) deg -= 360f;
            while (deg <= -180f) deg += 360f;
            return deg;
        }

        // ==================================================================
        // 塔
        // ==================================================================

        private static void BuildPagoda(Transform parent, Material dark, Material sand, Material red)
        {
            Box(parent, "Plinth_A", new Vector3(0f, 2f, 0f), new Vector3(11f, 4f, 11f), red);
            Box(parent, "Plinth_B", new Vector3(0f, 5f, 0f), new Vector3(9.5f, 2f, 9.5f), sand);

            Box(parent, "Tier1_Body", new Vector3(0f, 15f, 0f), new Vector3(8.5f, 18f, 8.5f), dark);
            Box(parent, "Tier1_Roof", new Vector3(0f, 24.75f, 0f), new Vector3(10.5f, 1.5f, 10.5f), red);
            BuildEaves(parent, "Eave1", 24.75f, 5.25f, 9.2f, 1.3f, red);

            Box(parent, "Tier2_Body", new Vector3(0f, 30f, 0f), new Vector3(6.5f, 9f, 6.5f), sand);
            Box(parent, "Tier2_Roof", new Vector3(0f, 35.15f, 0f), new Vector3(8f, 1.3f, 8f), red);
            BuildEaves(parent, "Eave2", 35.15f, 4f, 7f, 1.1f, red);

            Box(parent, "Tier3_Body", new Vector3(0f, 37.8f, 0f), new Vector3(4f, 4f, 4f), dark);
            Box(parent, "Tier3_Roof", new Vector3(0f, 40.3f, 0f), new Vector3(5.5f, 1f, 5.5f), red);
            BuildEaves(parent, "Eave3", 40.3f, 2.75f, 4.8f, 0.9f, red);

            Cylinder(parent, "Finial_Shaft", new Vector3(0f, 43.05f, 0f), 1f, 4.5f, dark);
            Sphere(parent, "Finial_Ball", new Vector3(0f, 46f, 0f), 1.8f, red);

            Vector3[] corners =
            {
                new Vector3(3.8f, 15f, 3.8f), new Vector3(-3.8f, 15f, 3.8f),
                new Vector3(3.8f, 15f, -3.8f), new Vector3(-3.8f, 15f, -3.8f)
            };
            foreach (Vector3 c in corners)
                Box(parent, "Pillar", c, new Vector3(0.8f, 18f, 0.8f), red);
        }

        private static void BuildEaves(Transform parent, string prefix, float y, float halfSide,
            float length, float overhang, Material mat)
        {
            const float tilt = 12f;
            float offset = halfSide + overhang * 0.5f;

            for (int i = 0; i < 4; i++)
            {
                bool alongX = (i % 2) == 0;
                float sign = (i < 2) ? 1f : -1f;

                Vector3 pos = alongX
                    ? new Vector3(0f, y - 0.2f, sign * offset)
                    : new Vector3(sign * offset, y - 0.2f, 0f);
                Vector3 size = alongX
                    ? new Vector3(length, 0.45f, overhang)
                    : new Vector3(overhang, 0.45f, length);

                GameObject eave = Box(parent, prefix + "_" + i, pos, size, mat);
                if (alongX) eave.transform.Rotate(sign * -tilt, 0f, 0f);
                else eave.transform.Rotate(0f, 0f, sign * tilt);
            }
        }

        // ==================================================================
        // 落脚台
        // ==================================================================

        private static Transform BuildLanding(Transform parent, string name, float angleDeg, float height,
            Material sand, Material red, Material dark)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);

            float rad = angleDeg * Mathf.Deg2Rad;
            Vector3 center = new Vector3(Mathf.Cos(rad) * LandingRadius, height, -Mathf.Sin(rad) * LandingRadius);

            GameObject deck = Box(group.transform, "Deck", center,
                new Vector3(LandingSize, LandingTop * 2f, LandingSize), sand);
            deck.transform.Rotate(0f, angleDeg, 0f);

            Box(group.transform, "Trim", center + new Vector3(0f, LandingTop + 0.05f, 0f),
                new Vector3(LandingSize - 0.4f, 0.1f, LandingSize - 0.4f), red)
                .transform.Rotate(0f, angleDeg, 0f);

            Box(group.transform, "Column", new Vector3(center.x, height * 0.5f, center.z),
                new Vector3(1.4f, height, 1.4f), dark).transform.Rotate(0f, angleDeg, 0f);

            group.transform.position = Vector3.zero;
            return deck.transform;
        }

        // ==================================================================
        // 旋转飞檐层
        // ==================================================================

        private static RotatingRamp BuildTier(Transform parent, string name, float hBase, float hRing,
            float beta, Material deckMat, Material trimMat)
        {
            GameObject rootGo = new GameObject(name);
            rootGo.transform.SetParent(parent, false);

            // 90° 环廊
            const int arcSegments = 4;
            float segAngle = ArcSpan / arcSegments;
            float chord = 2f * RingMid * Mathf.Sin(segAngle * 0.5f * Mathf.Deg2Rad) * 1.22f;

            for (int i = 0; i < arcSegments; i++)
            {
                float a = beta + segAngle * (i + 0.5f);
                float rad = a * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(Mathf.Cos(rad) * RingMid, hRing, -Mathf.Sin(rad) * RingMid);
                GameObject seg = Box(rootGo.transform, "Ring_" + i, pos,
                    new Vector3(chord, TierThickness, RingOuter - RingInner), deckMat);
                seg.transform.Rotate(0f, a + 90f, 0f);

                Vector3 trimPos = new Vector3(Mathf.Cos(rad) * (RingOuter - 0.15f), hRing + 0.5f,
                    -Mathf.Sin(rad) * (RingOuter - 0.15f));
                GameObject trim = Box(rootGo.transform, "RingTrim_" + i, trimPos,
                    new Vector3(chord, 0.18f, 0.3f), trimMat);
                trim.transform.Rotate(0f, a + 90f, 0f);
            }

            // 斜臂
            float tiltRad = Mathf.Atan2(Step, ArmOuter - RingOuter);
            float tilt = tiltRad * Mathf.Rad2Deg;
            float armMid = (RingOuter + ArmOuter) * 0.5f;
            // 关键：箱体整体倾斜后，水平投影会缩短为 L·cosθ，
            // 所以要按 1/cosθ 补偿长度，两端才能正好落在 (19.8,hBase) 与 (12.5,hRing)。
            float armLen = (ArmOuter - RingOuter) / Mathf.Cos(tiltRad);

            float br = beta * Mathf.Deg2Rad;
            Vector3 slopeDir = new Vector3(Mathf.Cos(br), 0f, -Mathf.Sin(br));
            Vector3 slopePos = slopeDir * armMid + new Vector3(0f, (hRing + hBase) * 0.5f, 0f);
            GameObject slope = Box(rootGo.transform, "Arm_Slope", slopePos,
                new Vector3(armLen, TierThickness, ArmWidth), deckMat);
            slope.transform.Rotate(0f, beta, 0f);
            slope.transform.Rotate(0f, 0f, -tilt);

            // 平臂
            float betaB = beta + ArcSpan;
            float bb = betaB * Mathf.Deg2Rad;
            Vector3 flatDir = new Vector3(Mathf.Cos(bb), 0f, -Mathf.Sin(bb));
            Vector3 flatPos = flatDir * armMid + new Vector3(0f, hRing, 0f);
            Box(rootGo.transform, "Arm_Flat", flatPos, new Vector3(armLen, TierThickness, ArmWidth), deckMat)
                .transform.Rotate(0f, betaB, 0f);

            RotatingRamp ramp = rootGo.AddComponent<RotatingRamp>();
            ramp.Configure(0f, 0f);
            return ramp;
        }

        /// <summary>沿真实踏面生成行走采样点：斜臂 6 + 环廊 4 + 平臂 4。</summary>
        private static List<Transform> BuildTierPath(Transform tierRoot, float beta, float hBase, float hRing)
        {
            List<Transform> points = new List<Transform>();
            const float surfaceUp = 0.45f;

            const int slopeSamples = 5;
            for (int i = 0; i <= slopeSamples; i++)
            {
                float t = i / (float)slopeSamples;
                // 两端各收 0.25，避免角色脚踩在悬臂/环廊的几何边界上
                float r = Mathf.Lerp(ArmOuter - 0.25f, RingOuter - 0.2f, t);
                float y = Mathf.Lerp(hBase + 0.45f, hRing + 0.38f, t);
                float rad = beta * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Cos(rad) * r, y, -Mathf.Sin(rad) * r);
                points.Add(MakeLocalNode(tierRoot, "P_Slope" + i, p));
            }

            const int arcSamples = 4;
            for (int i = 1; i <= arcSamples; i++)
            {
                float a = (beta + ArcSpan * i / arcSamples) * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Cos(a) * RingMid, hRing + surfaceUp - 0.1f, -Mathf.Sin(a) * RingMid);
                points.Add(MakeLocalNode(tierRoot, "P_Arc" + i, p));
            }

            const int flatSamples = 4;
            float betaB = beta + ArcSpan;
            for (int i = 1; i <= flatSamples; i++)
            {
                float t = i / (float)flatSamples;
                float r = Mathf.Lerp(RingOuter - 0.2f, ArmOuter - 0.25f, t);
                float a = betaB * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Cos(a) * r, hRing + surfaceUp - 0.1f, -Mathf.Sin(a) * r);
                points.Add(MakeLocalNode(tierRoot, "P_Flat" + i, p));
            }

            return points;
        }

        private static Transform MakeLocalNode(Transform parent, string name, Vector3 localPos)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        private static void AddTierColliders(RotatingRamp[] tiers)
        {
            for (int i = 0; i < tiers.Length; i++)
            {
                foreach (MeshRenderer mr in tiers[i].GetComponentsInChildren<MeshRenderer>())
                {
                    if (mr.name != "Arm_Slope" && mr.name != "Arm_Flat" && !mr.name.StartsWith("Ring_")) continue;
                    if (mr.GetComponent<Collider>() == null) mr.gameObject.AddComponent<BoxCollider>();
                }
            }
        }

        // ==================================================================
        // 角色
        // ==================================================================

        private static GameObject BuildCharacter(Transform parent, Vector3 worldPosition, Material white)
        {
            GameObject player = new GameObject("Player");
            player.transform.SetParent(parent, false);
            player.transform.position = worldPosition;

            float scale = CharacterTargetHeight / CharacterModelHeight;
            player.transform.localScale = Vector3.one * scale;

            GameObject head = Sphere(player.transform, "Head", worldPosition, 0.42f, white);
            head.transform.SetParent(player.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.11f, 0f);

            Mesh coneMesh = GetConeMesh(0.15f, 0.9f, 24);
            GameObject body = new GameObject("Body");
            body.transform.SetParent(player.transform, false);
            body.transform.localPosition = Vector3.zero;
            body.AddComponent<MeshFilter>().sharedMesh = coneMesh;
            body.AddComponent<MeshRenderer>().sharedMaterial = white;

            return player;
        }

        private static Mesh GetConeMesh(float radius, float height, int segments)
        {
            string path = MeshFolder + "/PlayerCone.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            vertices.Add(new Vector3(0f, height, 0f));
            vertices.Add(Vector3.zero);

            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.PI * 2f * i / segments;
                vertices.Add(new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }

            const int ringStart = 2;
            for (int i = 0; i < segments; i++)
            {
                int a = ringStart + i;
                int b = ringStart + i + 1;
                triangles.Add(0); triangles.Add(b); triangles.Add(a);
                triangles.Add(1); triangles.Add(a); triangles.Add(b);
            }

            Mesh mesh = new Mesh { name = "PlayerCone" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
            return mesh;
        }

        // ==================================================================
        // 相机 / 灯光
        // ==================================================================

        private static Camera BuildCamera(Transform root, Bounds bounds)
        {
            GameObject go = new GameObject("Main Camera");
            go.transform.SetParent(root, false);
            go.tag = "MainCamera";

            Camera cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.945f, 0.918f, 0.871f, 1f);
            cam.nearClipPlane = 1.5f;
            cam.farClipPlane = 1200f;
            cam.allowHDR = false;

            go.AddComponent<AudioListener>();

            const float aspect = 1080f / 1920f;
            const float pitch = 26f;
            const float yaw = 45f;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 forward = rotation * Vector3.forward;
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;

            float halfW = 0f, halfH = 0f;
            Vector3 ext = bounds.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 d = new Vector3(
                    ((i & 1) == 0 ? -1f : 1f) * ext.x,
                    ((i & 2) == 0 ? -1f : 1f) * ext.y,
                    ((i & 4) == 0 ? -1f : 1f) * ext.z);
                halfW = Mathf.Max(halfW, Mathf.Abs(Vector3.Dot(d, right)));
                halfH = Mathf.Max(halfH, Mathf.Abs(Vector3.Dot(d, up)));
            }

            cam.orthographicSize = Mathf.Max(halfH, halfW / aspect) * 1.06f;
            cam.transform.rotation = rotation;
            cam.transform.position = bounds.center - forward * 320f;

            return cam;
        }

        private static Bounds ComputeDesignBounds()
        {
            float r = LandingRadius + LandingSize * 0.5f;
            Vector3[] points =
            {
                new Vector3(-r, 0f, -r),
                new Vector3(r, 0f, r),
                new Vector3(-r, 32f, -r),
                new Vector3(r, 32f, r),
                new Vector3(-r, 32f, r),
                new Vector3(r, 32f, -r),
                new Vector3(0f, TowerTop, 0f),
            };

            Bounds b = new Bounds(points[0], Vector3.zero);
            for (int i = 1; i < points.Length; i++) b.Encapsulate(points[i]);
            return b;
        }

        private static void CreateLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.86f, 0.82f, 0.74f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.56f, 0.47f);
            RenderSettings.ambientGroundColor = new Color(0.33f, 0.28f, 0.23f);
            RenderSettings.fog = false;

            GameObject keyGo = new GameObject("Key Light");
            Light key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.95f, 0.86f);
            key.intensity = 1.15f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.75f;
            keyGo.transform.rotation = Quaternion.Euler(46f, -34f, 0f);

            GameObject fillGo = new GameObject("Fill Light");
            Light fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.72f, 0.78f, 0.95f);
            fill.intensity = 0.28f;
            fill.shadows = LightShadows.None;
            fillGo.transform.rotation = Quaternion.Euler(22f, 148f, 0f);
        }

        // ==================================================================
        // UI
        // ==================================================================

        private static Canvas BuildCanvas()
        {
            GameObject go = new GameObject("Level Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            return canvas;
        }

        private static LevelIntro BuildIntro(Transform canvas)
        {
            GameObject root = new GameObject("IntroOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            RectTransform rect = (RectTransform)root.transform;
            rect.SetParent(canvas, false);
            Stretch(rect);

            Image bg = root.GetComponent<Image>();
            bg.color = Color.black;
            bg.raycastTarget = true;

            Text title = CreateText(root.transform, "Title", "西安大雁塔", 96, TextAnchor.MiddleCenter,
                new Color(0.91f, 0.86f, 0.76f));
            RectTransform tr = title.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.pivot = new Vector2(0.5f, 0.5f);
            tr.sizeDelta = new Vector2(900f, 200f);
            tr.anchoredPosition = Vector2.zero;

            LevelIntro intro = root.AddComponent<LevelIntro>();
            SetRef(intro, "_group", root.GetComponent<CanvasGroup>());
            SetRef(intro, "_title", title);
            return intro;
        }

        private static VictoryPanel BuildVictory(Transform canvas, Sprite levelArt)
        {
            GameObject root = new GameObject("VictoryPanel", typeof(RectTransform), typeof(CanvasGroup));
            RectTransform rect = (RectTransform)root.transform;
            rect.SetParent(canvas, false);
            Stretch(rect);

            GameObject dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            RectTransform dimRect = (RectTransform)dim.transform;
            dimRect.SetParent(rect, false);
            Stretch(dimRect);
            dim.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.03f, 0.78f);

            GameObject artBand = new GameObject("ArtBand_50", typeof(RectTransform));
            RectTransform artRect = (RectTransform)artBand.transform;
            artRect.SetParent(rect, false);
            artRect.anchorMin = new Vector2(0f, 0.5f);
            artRect.anchorMax = new Vector2(1f, 1f);
            artRect.offsetMin = Vector2.zero;
            artRect.offsetMax = Vector2.zero;

            GameObject art = new GameObject("LevelArt", typeof(RectTransform), typeof(Image));
            RectTransform artImgRect = (RectTransform)art.transform;
            artImgRect.SetParent(artRect, false);
            artImgRect.anchorMin = new Vector2(0f, 0f);
            artImgRect.anchorMax = new Vector2(1f, 1f);
            artImgRect.offsetMin = new Vector2(70f, 70f);
            artImgRect.offsetMax = new Vector2(-70f, -70f);
            Image artImage = art.GetComponent<Image>();
            artImage.sprite = levelArt;
            artImage.preserveAspect = true;
            artImage.raycastTarget = false;

            GameObject textBand = new GameObject("TextBand_30", typeof(RectTransform));
            RectTransform textRect = (RectTransform)textBand.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = new Vector2(0f, 0.2f);
            textRect.anchorMax = new Vector2(1f, 0.5f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text verse = CreateText(textRect, "Verse",
                "面敷青灰，墨缕分眉，额缀杂彩，鼻吻仿兽形，浓髯垂胸类虎鬃，蓝冠饰云作武姿。",
                40, TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.85f));
            RectTransform verseRect = verse.rectTransform;
            verseRect.anchorMin = Vector2.zero;
            verseRect.anchorMax = Vector2.one;
            verseRect.offsetMin = new Vector2(90f, 20f);
            verseRect.offsetMax = new Vector2(-90f, -20f);
            verse.lineSpacing = 1.25f;

            GameObject buttonBand = new GameObject("ButtonBand_20", typeof(RectTransform));
            RectTransform buttonRect = (RectTransform)buttonBand.transform;
            buttonRect.SetParent(rect, false);
            buttonRect.anchorMin = new Vector2(0f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 0.2f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;

            GameObject btnGo = new GameObject("BtnNext", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform btnRect = (RectTransform)btnGo.transform;
            btnRect.SetParent(buttonRect, false);
            btnRect.anchorMin = btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.pivot = new Vector2(0.5f, 0.5f);
            btnRect.sizeDelta = new Vector2(420f, 140f);
            btnRect.anchoredPosition = new Vector2(0f, 6f);

            Image btnImage = btnGo.GetComponent<Image>();
            btnImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            btnImage.type = Image.Type.Sliced;
            btnImage.color = new Color(0.545f, 0.001f, 0.004f, 0.95f);

            Button button = btnGo.GetComponent<Button>();
            button.targetGraphic = btnImage;

            Text label = CreateText(btnRect, "Label", "下一关", 48, TextAnchor.MiddleCenter,
                new Color(0.98f, 0.95f, 0.88f));
            Stretch(label.rectTransform, 10f);

            VictoryPanel panel = root.AddComponent<VictoryPanel>();
            SetRef(panel, "_group", root.GetComponent<CanvasGroup>());
            UnityEventTools.AddPersistentListener(button.onClick, panel.OnNextLevel);

            root.SetActive(false);
            return panel;
        }

        private static void BuildHint(Transform canvas)
        {
            Text hint = CreateText(canvas, "Hint", "拖动空白处环绕视角 · 拖动飞檐廊改变朝向 · 点击画面前进",
                34, TextAnchor.MiddleCenter, new Color(0.22f, 0.18f, 0.14f, 0.85f));
            RectTransform rect = hint.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.9f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(40f, 0f);
            rect.offsetMax = new Vector2(-40f, -30f);
        }

        private static void BuildBrightnessOverlay(Transform canvas)
        {
            GameObject go = new GameObject("BrightnessOverlay", typeof(RectTransform), typeof(Image), typeof(BrightnessApplier));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(canvas, false);
            Stretch(rect);
            rect.SetAsLastSibling();

            Image image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = false;

            SetRef(go.GetComponent<BrightnessApplier>(), "_overlay", image);
        }

        private static void CreateAudioManager(AudioClip clip)
        {
            GameObject go = new GameObject("AudioManager", typeof(AudioSource), typeof(Xunyi.Audio.AudioManager));

            AudioSource source = go.GetComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = true;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0.8f;

            SetRef(go.GetComponent<Xunyi.Audio.AudioManager>(), "_bgmSource", source);
        }

        private static AudioClip ImportBgm()
        {
            if (!AssetDatabase.IsValidFolder(AudioFolder))
                AssetDatabase.CreateFolder("Assets", "Audio");

            if (File.Exists(BgmSource))
            {
                File.Copy(BgmSource, BgmPath, true);
                AssetDatabase.ImportAsset(BgmPath, ImportAssetOptions.ForceUpdate);
            }

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(BgmPath);
            if (clip == null) Debug.LogError("[Level1Builder] BGM 载入失败：" + BgmPath);
            return clip;
        }

        // ==================================================================
        // 基础工具
        // ==================================================================

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
            if (!AssetDatabase.IsValidFolder(ArtFolder)) AssetDatabase.CreateFolder("Assets/Art", "UI");
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/Materials", "Level1");
            if (!AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.CreateFolder("Assets", "Meshes");
        }

        private static Material GetMaterial(string name, string hex)
        {
            string path = MatFolder + "/" + name + ".mat";
            Color color;
            ColorUtility.TryParseHtmlString(hex, out color);

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.08f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.08f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static Sprite ImportSprite(string sourcePath, string assetPath)
        {
            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, assetPath, true);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null) Debug.LogError("[Level1Builder] 插画载入失败：" + assetPath);
            return sprite;
        }

        private static GameObject Box(Transform parent, string name, Vector3 worldPos, Vector3 size, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.localScale = size;

            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject Cylinder(Transform parent, string name, Vector3 worldPos, float diameter,
            float height, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);

            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject Sphere(Transform parent, string name, Vector3 worldPos, float diameter, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.localScale = Vector3.one * diameter;

            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static Transform MakeNode(Transform parent, string name, Vector3 worldPos)
        {
            GameObject node = new GameObject(name);
            node.transform.SetParent(parent, false);
            node.transform.position = worldPos;
            return node.transform;
        }

        private static Text CreateText(Transform parent, string name, string content, int size,
            TextAnchor anchor, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            Text text = go.GetComponent<Text>();
            // 必须用工程内置中文字体：WebGL 无系统字体，内置字体无中文字形
            Font cjk = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/XunyiCJK.otf");
            text.font = cjk != null ? cjk : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            return text;
        }

        private static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void SetRef(Object target, string fieldName, Object value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(fieldName);
            if (p == null) { Debug.LogError("[Level1Builder] 字段不存在：" + fieldName); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRefArray(Object target, string fieldName, Object[] values)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(fieldName);
            if (p == null) { Debug.LogError("[Level1Builder] 字段不存在：" + fieldName); return; }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetIntArray(Object target, string fieldName, int[] values)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(fieldName);
            if (p == null) { Debug.LogError("[Level1Builder] 字段不存在：" + fieldName); return; }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).intValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string fieldName, float value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(fieldName);
            if (p == null) { Debug.LogError("[Level1Builder] 字段不存在：" + fieldName); return; }
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RegisterScenesInBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
            scenes.Add(new EditorBuildSettingsScene(SampleScenePath, true));
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
