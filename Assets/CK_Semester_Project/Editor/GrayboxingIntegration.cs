using System;
using System.IO;
using System.Linq;
using CK.SemesterProject.Tutorial;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CK.SemesterProject.Editor
{
    public static class GrayboxingIntegration
    {
        private const string SourceScene = "Assets/CK_Semester_Project/Scenes/SampleScene.unity";
        private const string OutputScene = "Assets/CK_Semester_Project/Prototype/Scenes/MapGrayboxingFuchsia.unity";
        private const string ModelPrefab = "Assets/CK_Semester_Project/Prototype/Sandbox/Junmo/FuchsiaShading/Prefabs/FuchsiaToon.prefab";
        private const string ProbeKey = "CK.Grayboxing.PlayProbe";
        private static double _probeStart;
        private static double _probeLastTime;
        private static int _probePhase;
        private static Vector3 _probePlayerPosition;
        private static Vector3 _probeCameraPosition;
        private static bool _hasProbeError;

        public static void RunPlayProbe()
        {
            Validate();
            SessionState.SetBool(ProbeKey, true);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void RegisterProbe()
        {
            if (!SessionState.GetBool(ProbeKey, false))
            {
                return;
            }
            _probeStart = -1d;
            EditorApplication.update += UpdateProbe;
            Application.logMessageReceived += OnProbeLog;
        }

        private static void OnProbeLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                _hasProbeError = true;
            }
        }

        private static void UpdateProbe()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                return;
            }
            Application.runInBackground = true;
            Time.captureDeltaTime = 1f / 60f;
            EditorApplication.isPaused = false;
            EditorApplication.QueuePlayerLoopUpdate();
            if (_probeStart < 0d)
            {
                _probeStart = Time.time;
                return;
            }
            // 도메인 재로드 시간이 아닌 실제 Play 프레임 시간을 기준으로 검사합니다.
            double elapsed = Time.time - _probeStart;
            if (elapsed < 3d)
            {
                return;
            }
            GameObject player = GameObject.Find("Fuchsia Player");
            CharacterController controller = player.GetComponent<CharacterController>();
            if (_probePhase == 0)
            {
                _probePlayerPosition = player.transform.position;
                _probeCameraPosition = Camera.main.transform.position;
                Debug.Log("GRAYBOX_PROBE_START position=" + player.transform.position + " grounded=" + controller.isGrounded
                    + " enabled=" + player.GetComponent<PlayerMovement>().isActiveAndEnabled + " frame=" + Time.frameCount);
                _probePhase = 1;
                _probeLastTime = elapsed;
            }
            if (elapsed < 4d)
            {
                float probeDelta = Mathf.Clamp((float)(elapsed - _probeLastTime), 0f, 0.1f);
                _probeLastTime = elapsed;
                controller.Move((Vector3.forward * 0.75f + Vector3.down * 2f) * probeDelta);
                return;
            }
            if (elapsed < 7d)
            {
                return;
            }
            float playerDistance = Vector3.Distance(player.transform.position, _probePlayerPosition);
            float cameraDistance = Vector3.Distance(Camera.main.transform.position, _probeCameraPosition);
            CollisionFlags groundFlags = controller.Move(Vector3.down * 0.05f);
            bool isGrounded = (groundFlags & CollisionFlags.Below) != 0;
            if (playerDistance < 0.1f || cameraDistance < 0.05f || !isGrounded)
            {
                _hasProbeError = true;
                Debug.LogWarning("GRAYBOX_PROBE: 이동/추적/접지 확인 실패 position=" + player.transform.position);
            }
            Capture();
            Debug.Log("GRAYBOX_PLAY_PROBE " + (_hasProbeError ? "FAIL" : "PASS")
                + " playerDistance=" + playerDistance + " cameraDistance=" + cameraDistance
                + " grounded=" + isGrounded);
            SessionState.SetBool(ProbeKey, false);
            EditorApplication.update -= UpdateProbe;
            Application.logMessageReceived -= OnProbeLog;
            EditorApplication.Exit(_hasProbeError ? 1 : 0);
        }

        public static void Inspect()
        {
            EditorSceneManager.OpenScene(SourceScene);
            foreach (Transform item in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (item.parent == null || item.name.StartsWith("player") || item.name == "floor")
                {
                    Debug.Log("GRAYBOX_OBJECT " + item.name + " parent=" + (item.parent == null ? "root" : item.parent.name)
                        + " position=" + item.position + " scale=" + item.lossyScale);
                }
            }
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrefab));
            Bounds bounds = GetBounds(model);
            Debug.Log("FUCHSIA_BOUNDS " + bounds);
            Debug.Log("GRAYBOX_INSPECT_COMPLETE");
            Build();
        }

        public static void Build()
        {
            Scene scene = EditorSceneManager.OpenScene(SourceScene);
            RepairBackground(scene);
            GameObject marker = GameObject.Find("player");
            if (marker == null)
            {
                throw new InvalidOperationException("Grayboxing 시작 위치 player를 찾을 수 없습니다.");
            }
            Vector3 spawn = marker.transform.position;
            foreach (Transform item in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (item.name == "player" || item.name == "player (1)")
                {
                    item.gameObject.SetActive(false);
                }
            }
            Physics.SyncTransforms();
            RaycastHit ground;
            if (!Physics.Raycast(spawn, Vector3.down, out ground, 5f, ~0, QueryTriggerInteraction.Ignore))
            {
                throw new InvalidOperationException("시작 위치 아래에 충돌 가능한 바닥이 없습니다.");
            }
            spawn = ground.point + Vector3.up * 0.08f;
            string groundName = ground.collider.name;
            foreach (Camera camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
            }

            // 프로토타입 카메라의 렌즈·감쇠·입력 설정을 그대로 복제합니다.
            Scene prototype = EditorSceneManager.OpenScene(
                "Assets/CK_Semester_Project/Prototype/Scenes/TutorialDemo.unity", OpenSceneMode.Additive);
            CameraController sourceOrbit = prototype.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CameraController>(true)).Single();
            Camera sourceCamera = prototype.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Single(camera => camera.CompareTag("MainCamera"));
            GameObject mainCamera = UnityEngine.Object.Instantiate(sourceCamera.gameObject);
            GameObject orbitCamera = UnityEngine.Object.Instantiate(sourceOrbit.gameObject);
            mainCamera.name = "Main Camera";
            orbitCamera.name = "Fuchsia Follow Camera";
            SceneManager.MoveGameObjectToScene(mainCamera, scene);
            SceneManager.MoveGameObjectToScene(orbitCamera, scene);
            EditorSceneManager.CloseScene(prototype, true);
            SceneManager.SetActiveScene(scene);

            var player = new GameObject("Fuchsia Player");
            player.tag = "Player";
            player.transform.position = spawn;
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrefab), scene);
            visual.name = "Fuchsia Visual";
            visual.transform.SetParent(player.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            Bounds bounds = GetBounds(visual);
            const float CharacterHeight = 1.7f;
            visual.transform.localScale *= CharacterHeight / bounds.size.y;
            bounds = GetBounds(visual);
            visual.transform.position += new Vector3(spawn.x - bounds.center.x, spawn.y - bounds.min.y, spawn.z - bounds.center.z);

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = CharacterHeight;
            controller.radius = 0.28f;
            controller.center = Vector3.up * (CharacterHeight * 0.5f);
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 45f;
            controller.skinWidth = 0.03f;
            PlayerMovement movement = player.AddComponent<PlayerMovement>();
            var movementSettings = new SerializedObject(movement);
            movementSettings.FindProperty("_cameraTransform").objectReferenceValue = mainCamera.transform;
            movementSettings.ApplyModifiedPropertiesWithoutUndo();

            CinemachineCamera virtualCamera = orbitCamera.GetComponent<CinemachineCamera>();
            virtualCamera.Follow = player.transform;
            virtualCamera.LookAt = player.transform;
            mainCamera.transform.position = spawn + new Vector3(0f, 2.1f, -3.85f);
            mainCamera.transform.LookAt(spawn + Vector3.up);
            orbitCamera.transform.SetPositionAndRotation(mainCamera.transform.position, mainCamera.transform.rotation);

            Light sun = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .First(light => light.type == LightType.Directional);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.intensity = 1f;
            RenderSettings.sun = sun;

            EditorSceneManager.SaveScene(scene, OutputScene);
            AssetDatabase.SaveAssets();
            Validate();
            Capture();
            Debug.Log("GRAYBOX_BUILD_COMPLETE spawn=" + spawn + " floor=" + groundName);
        }

        private static void RepairBackground(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (PrefabUtility.GetPrefabAssetType(root) == PrefabAssetType.MissingAsset)
                {
                    Debug.Log("GRAYBOX_REPAIR missing prefab placeholder removed: " + root.name);
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
            const string MaterialPath = "Assets/CK_Semester_Project/Prototype/Materials/GrayboxingNeutral.mat";
            Material neutral = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (neutral == null)
            {
                neutral = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                neutral.SetColor("_BaseColor", new Color(0.62f, 0.65f, 0.68f, 1f));
                neutral.SetFloat("_Smoothness", 0.15f);
                AssetDatabase.CreateAsset(neutral, MaterialPath);
            }
            int repaired = 0;
            foreach (Renderer renderer in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Renderer>(true)))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                {
                    Material material = materials[index];
                    if (material == null || material.shader == null || !material.shader.isSupported
                        || material.shader.name == "Hidden/InternalErrorShader" || material.shader.name.StartsWith("ProBuilder/"))
                    {
                        materials[index] = neutral;
                        repaired++;
                    }
                }
                renderer.sharedMaterials = materials;
            }
            Debug.Log("GRAYBOX_REPAIR material slots=" + repaired);
        }

        public static void Capture()
        {
            Camera camera = Camera.main;
            GameObject player = GameObject.Find("Fuchsia Player");
            Directory.CreateDirectory("Docs/Images");
            SaveImage(camera, "Docs/Images/MapGrayboxingFuchsia.png");
            Vector3 position = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            camera.transform.position = player.transform.position + new Vector3(2f, 1.5f, 3f);
            camera.transform.LookAt(player.transform.position + Vector3.up * 0.85f);
            SaveImage(camera, "Docs/Images/MapGrayboxingFuchsiaFront.png");
            camera.transform.SetPositionAndRotation(position, rotation);
        }

        private static void SaveImage(Camera camera, string path)
        {
            var target = new RenderTexture(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(image);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        public static void Validate()
        {
            Scene scene = EditorSceneManager.OpenScene(OutputScene);
            int missing = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Sum(item => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject));
            if (missing != 0)
            {
                throw new InvalidOperationException("Missing scripts: " + missing);
            }
            foreach (Renderer renderer in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Renderer>(true)))
            {
                if (renderer.sharedMaterials.Any(material => material == null || material.shader == null
                    || material.shader.name == "Hidden/InternalErrorShader" || !material.shader.isSupported))
                {
                    throw new InvalidOperationException("배경 머티리얼 연결 오류: " + renderer.name);
                }
            }
            GameObject player = GameObject.Find("Fuchsia Player");
            if (player == null || player.GetComponent<PlayerMovement>() == null || player.GetComponent<Animator>() != null)
            {
                throw new InvalidOperationException("정적 푸시아 이동 구성 오류");
            }
            Renderer[] renderers = player.GetComponentsInChildren<Renderer>();
            if (renderers.Length != 35 || renderers.Any(renderer => renderer.sharedMaterials.Any(material => material == null || material.shader == null || !material.shader.isSupported)))
            {
                throw new InvalidOperationException("푸시아 모델/머티리얼 연결 오류");
            }
            CinemachineCamera camera = GameObject.Find("Fuchsia Follow Camera").GetComponent<CinemachineCamera>();
            if (camera.Follow != player.transform || camera.LookAt != player.transform || Camera.main == null)
            {
                throw new InvalidOperationException("카메라 연결 오류");
            }
            Debug.Log("GRAYBOX_VALIDATION_PASS renderers=" + renderers.Length + " colliders="
                + UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Length);
        }

        private static Bounds GetBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }
    }
}
