using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Utils.Citrine.AnimationOnionSkin
{
    public class AnimationOnionStage : PreviewSceneStage
    {
        public OnionSkinPreviewWindow ownerWindow;
        public GUIContent titleContent;

        private readonly List<GameObject> _gameObjects = new();
        private GameObject _mainGameObject;

        private float _alpha = 0.5f;
        private int _step = 1;
        private int _frame = 0;
        private AnimationClip _clip = null;
        private Vector2 _positionOffset = Vector2.zero;

        private bool DirtyFlag { get; set; } = true;

        private float Alpha
        {
            get => _alpha;
            set
            {
                DirtyFlag |= Mathf.Abs(_alpha - value) > 0.00001f;
                _alpha = value;
            }
        }

        private int Step
        {
            get => _step;
            set
            {
                DirtyFlag |= _step != value;
                _step = Mathf.Max(0, value);
            }
        }

        private bool AlwaysRepaint { get; set; } = true;

        private AnimationWindow Window { get; set; } = null;

        private int Frame
        {
            get => _frame;
            set
            {
                DirtyFlag |= _frame != value;
                if (Clip != null)
                {
                    _frame = value % Mathf.RoundToInt(Clip.length * Clip.frameRate);
                }
                else
                {
                    _frame = value;
                }
            }
        }

        private AnimationClip Clip
        {
            get => _clip;
            set
            {
                DirtyFlag |= _clip != value;
                DirtyFlag &= value != null;
                _clip = value;
            }
        }
        
        private bool FreelyDrag { get; set; }

        private Vector2 PositionOffset
        {
            get => _positionOffset;
            set
            {
                DirtyFlag |= _positionOffset != value;
                _positionOffset = value;
            }
        }

        private void RefreshProperties(bool focusOnAnimationWindow)
        {
            Window = EditorWindow.GetWindow<AnimationWindow>(false, null, focusOnAnimationWindow);
            if (Window != null)
            {
                Frame = Window.frame;
                Clip = Window.animationClip;
            }
            else
            {
                Frame = 0;
                Clip = null;
            }
        }

        public void SetupScene()
        {
            _mainGameObject = Instantiate(ownerWindow.selectedObject as GameObject);
            StageUtility.PlaceGameObjectInCurrentStage(_mainGameObject);
            Selection.activeObject = _mainGameObject;
            
            RefreshProperties(true);
        }

        protected override GUIContent CreateHeaderContent()
        {
            GUIContent headerContent = new GUIContent
            {
                text = ownerWindow.selectedObject.name,
                image = EditorGUIUtility.IconContent("GameObject Icon").image
            };

            return headerContent;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            OnionSkinWindow.Enable = false;
            
            Handles.BeginGUI();

            Alpha = EditorGUI.Slider(new Rect(50, 20, 200, 20), "Alpha 衰减系数", Alpha, 0, 1);

            Step = EditorGUI.IntField(new Rect(50, 45, 200, 20), "步数", Step);

            AlwaysRepaint = EditorGUI.Toggle(new Rect(50, 70, 200, 20), "总是重绘", AlwaysRepaint);

            FreelyDrag = !EditorGUI.Toggle(new Rect(50, 95, 200, 20), "固定位置偏移", !FreelyDrag);

            if (!FreelyDrag)
            {
                PositionOffset = new Vector2(
                    EditorGUI.FloatField(new Rect(60, 120, 190, 20), "x", PositionOffset.x),
                    EditorGUI.FloatField(new Rect(60, 145, 190, 20), "y", PositionOffset.y)
                );
            }
            else if (GUI.Button(new Rect(50, 120, 200, 15), "重置") && _mainGameObject != null)
            {
                FreelyDrag = false;
                PositionOffset = Vector2.zero;
                DirtyFlag = true;
            }

            Handles.EndGUI();

            RefreshProperties(false);

            if (DirtyFlag)
            {
                Selection.activeObject = _mainGameObject;
            }

            if (DirtyFlag | AlwaysRepaint && Window != null && Clip != null)
            {
                UpdateOnionSkin();
            }
        }

        private void UpdateOnionSkin()
        {
            DirtyFlag = false;

            if (_gameObjects.Count != Step * 2 + 1)
            {
                for (int i = 0; i < _gameObjects.Count; i++)
                {
                    if (_gameObjects[i] != null && _gameObjects[i] != _mainGameObject)
                    {
                        DestroyImmediate(_gameObjects[i]);
                    }

                    _gameObjects[i] = null;
                }

                _gameObjects.Clear();
                _gameObjects.Capacity = Step * 2 + 1;

                for (int i = 0; i < 2 * Step + 1; i++)
                {
                    if (Step == i)
                    {
                        _gameObjects.Add(_mainGameObject);
                    }
                    else
                    {
                        _gameObjects.Add(Instantiate(_mainGameObject));
                        StageUtility.PlaceGameObjectInCurrentStage(_gameObjects[i]);
                        if (_gameObjects[i].TryGetComponent(out Animation animation))
                        {
                            DestroyImmediate(animation);
                        }
                        if (_gameObjects[i].TryGetComponent(out Animator animator))
                        {
                            DestroyImmediate(animator);
                        }
                    }
                }
            }

            for (int i = -Step; i <= Step; i++)
            {
                _gameObjects[Step + i].name = $"Current Keyframe {(i == 0 ? "" : i > 0 ? $"+{i}" : i)}";
                if (i != 0)
                {
                    SetOffsetToGameObject(i, _gameObjects[Step + i]);
                }
            }
        }

        private void SetOffsetToGameObject(int offset, GameObject go)
        {
            if (!FreelyDrag)
            {
                go.transform.position = _mainGameObject.transform.position + (Vector3) (PositionOffset * offset);
            }
            EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(Clip);
            foreach (EditorCurveBinding binding in bindings)
            {
                ObjectReferenceKeyframe[] frames = AnimationUtility.GetObjectReferenceCurve(Clip, binding);
                int keyframe = CalculateKeyframe(frames) + offset;
                Object value = frames[(keyframe + frames.Length * Mathf.Abs(offset)) % frames.Length].value;
                GameObject target = binding.path.Length < 1 ? go : go.transform.Find(binding.path)?.gameObject;
                if (target != null)
                {
                    Component component = target.GetComponent(binding.type);
                    PropertyInfo item = binding.type.GetProperty(binding.propertyName[2..].ToLower());
                    if (item != null)
                    {
                        item.SetValue(component, value);
                    }

                    SpriteRenderer[] spriteRenderers = go.GetComponentsInChildren<SpriteRenderer>();
                    foreach (SpriteRenderer sr in spriteRenderers)
                    {
                        Color color = sr.color;
                        sr.color = new Color(color.r, color.g, color.b, 1f * Mathf.Pow(Alpha, Mathf.Abs(offset)));
                    }
                }
            }
        }

        private int CalculateKeyframe(ObjectReferenceKeyframe[] keyframes)
        {
            if (keyframes is { Length: < 1 })
            {
                return 0;
            }

            for (int index = 0; index < keyframes.Length; index++)
            {
                if (Mathf.RoundToInt(keyframes[index].time * Clip.frameRate) <= Frame &&
                    ((index + 1 < keyframes.Length && Mathf.RoundToInt(keyframes[index + 1].time * Clip.frameRate) > Frame) ||
                     (index + 1 >= keyframes.Length && Mathf.RoundToInt(Clip.length * Clip.frameRate) >= Frame)))
                {
                    return index;
                }
            }

            return 0;
        }

        protected override bool OnOpenStage()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            return base.OnOpenStage();
        }

        protected override void OnCloseStage()
        {
            base.OnCloseStage();
            SceneView.duringSceneGui -= OnSceneGUI;
        }
    }
}
