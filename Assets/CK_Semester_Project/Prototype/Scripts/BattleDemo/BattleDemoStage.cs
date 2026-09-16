using System.Collections.Generic;
using UnityEngine;

namespace CK.SemesterProject.Battle.Demo
{
    [RequireComponent(typeof(BattleDemoController))]
    public sealed class BattleDemoStage : MonoBehaviour
    {
        private readonly string[] _ids = { "player", "sentinel_a", "sentinel_b" };
        private readonly float[] _positions = { -5.4f, 0.9f, 5.4f };
        private readonly List<Material> _materials = new List<Material>();
        private readonly List<Transform> _figures = new List<Transform>();
        private BattleDemoController _controller;
        private Camera _camera;

        private void Awake()
        {
            _controller = GetComponent<BattleDemoController>();
            _camera = Camera.main;
            if (_camera == null)
            {
                Debug.LogError("BattleCoreDemo: MainCamera 참조가 필요합니다.", this);
                enabled = false;
                return;
            }
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.04f, 0.06f, 0.09f);
            _camera.orthographic = true;
            _camera.transform.position = new Vector3(0, 6, -11);
            _camera.transform.LookAt(new Vector3(0, 0.8f, 0));
            RenderSettings.ambientLight = new Color(0.58f, 0.63f, 0.7f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;

            Material ground = MakeMaterial(new Color(0.07f, 0.105f, 0.15f));
            Material dark = MakeMaterial(new Color(0.10f, 0.14f, 0.20f));
            Material cyan = MakeMaterial(new Color(0.25f, 0.85f, 0.77f));
            Material pink = MakeMaterial(new Color(0.9f, 0.28f, 0.39f));
            Material orange = MakeMaterial(new Color(0.98f, 0.55f, 0.25f));
            Material eye = MakeMaterial(new Color(0.85f, 0.96f, 1f));
            CreateShape("Arena", PrimitiveType.Cube, transform, new Vector3(0, -0.25f, 0), new Vector3(30, 0.2f, 18), ground);
            for (int i = 0; i < _ids.Length; i++)
            {
                Material accent = i == 0 ? cyan : i == 1 ? pink : orange;
                CreateShape("Platform rim", PrimitiveType.Cylinder, transform, new Vector3(_positions[i], -0.09f, 0), new Vector3(2.65f, 0.06f, 2.65f), accent);
                CreateShape("Platform", PrimitiveType.Cylinder, transform, new Vector3(_positions[i], 0f, 0), new Vector3(2.48f, 0.07f, 2.48f), dark);
                var figure = new GameObject(_ids[i]);
                figure.transform.SetParent(transform, false);
                figure.transform.localPosition = new Vector3(_positions[i], 0.18f, 0);
                _figures.Add(figure.transform);
                if (i == 0)
                {
                    CreateShape("Body", PrimitiveType.Capsule, figure.transform, new Vector3(0, 1, 0), new Vector3(0.85f, 0.95f, 0.85f), cyan);
                    CreateShape("Visor", PrimitiveType.Cube, figure.transform, new Vector3(0, 1.55f, -0.38f), new Vector3(0.61f, 0.13f, 0.18f), dark);
                    CreateShape("Blade", PrimitiveType.Cube, figure.transform, new Vector3(0.7f, 1.1f, -0.1f), new Vector3(0.12f, 1.7f, 0.18f), eye);
                }
                else
                {
                    Transform body = CreateShape("Shell", PrimitiveType.Cube, figure.transform, new Vector3(0, 1.1f, 0), new Vector3(1.18f, 1.65f, 1.05f), accent);
                    body.localRotation = Quaternion.Euler(0, i == 1 ? -12 : 12, 0);
                    CreateShape("Eye", PrimitiveType.Cube, figure.transform, new Vector3(0, 1.4f, -0.59f), new Vector3(0.78f, 0.14f, 0.12f), eye);
                    CreateShape("Core", PrimitiveType.Sphere, figure.transform, new Vector3(0, 0.85f, -0.59f), new Vector3(0.25f, 0.25f, 0.12f), dark);
                }
            }
        }

        private void LateUpdate()
        {
            float scale = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
            float left = (Screen.width - 1440f * scale) * 0.5f;
            float bottom = (Screen.height - 900f * scale) * 0.5f;
            _camera.pixelRect = new Rect(left, bottom + 279f * scale, 1440f * scale, 416f * scale);
            _camera.orthographicSize = 8f / _camera.aspect;

            BattleActionResult result = _controller.PendingResult;
            for (int i = 0; i < _figures.Count; i++)
            {
                CombatantState state = _controller.GetDisplayedState(_ids[i]);
                float pulse = 0;
                if (result != null && result.Request.ActorId == _ids[i] && !result.WasSkipped
                    && result.Request.Kind == BattleActionKind.Skill)
                {
                    pulse = Mathf.Sin(Mathf.Clamp01(_controller.PresentationProgress / 0.55f) * Mathf.PI);
                }
                _figures[i].localPosition = new Vector3(_positions[i] + pulse * (i == 0 ? 0.75f : -0.75f), 0.18f, 0);
                _figures[i].localRotation = state.IsDead ? Quaternion.Euler(0, 0, 83) : Quaternion.identity;
                _figures[i].localScale = state.IsDead ? Vector3.one * 0.65f : Vector3.one;
            }
        }

        private void OnDestroy()
        {
            foreach (Material material in _materials)
            {
                Destroy(material);
            }
        }

        private Material MakeMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.32f);
            _materials.Add(material);
            return material;
        }

        private static Transform CreateShape(string name, PrimitiveType type, Transform parent,
            Vector3 position, Vector3 scale, Material material)
        {
            GameObject shape = GameObject.CreatePrimitive(type);
            shape.name = name;
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position;
            shape.transform.localScale = scale;
            shape.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(shape.GetComponent<Collider>());
            return shape.transform;
        }
    }
}
