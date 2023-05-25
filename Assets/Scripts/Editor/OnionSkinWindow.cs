using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Utils.Citrine.AnimationOnionSkin
{
    [InitializeOnLoad]
    public class OnionSkinWindow : EditorWindow
    {
        private static bool _enable = false;
        private static bool _dirtyFlag = true;
        private static float _alpha = 0.8f;
        private static int _step = 1;
        private static int _maxStep = 1;
        private static bool _alwaysRepaint = true;

        private static AnimationWindow _window = null;
        private static GameObject _activeObject = null;
        private static int _frame = 0;
        private static AnimationClip _clip = null;

        private static readonly List<RenderTexture> RenderTextures = new();

        private static bool DirtyFlag
        {
            get => _dirtyFlag | _alwaysRepaint;
            set => _dirtyFlag = value;
        }

        private static float Alpha
        {
            get => _alpha;
            set
            {
                DirtyFlag |= Mathf.Abs(_alpha - value) > 0.00001f;
                _alpha = value;
            }
        }

        private static int Step
        {
            get => _step;
            set
            {
                DirtyFlag |= _step != value;
                _step = value;
            }
        }

        private static AnimationWindow Window
        {
            get => _window;
            set
            {
                DirtyFlag |= _window != value;
                _window = value;
            }
        }

        private static GameObject ActiveObject
        {
            get => _activeObject;
            set
            {
                DirtyFlag |= _activeObject != value;
                _activeObject = value;
            }
        }

        private static int Frame
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

        private static AnimationClip Clip
        {
            get => _clip;
            set
            {
                DirtyFlag |= _clip != value;
                DirtyFlag &= value != null;
                _clip = value;
            }
        }

        static OnionSkinWindow()
        {
            SceneView.duringSceneGui += OnGUISceneView;
        }

        [MenuItem("Animation Tools/OnionSkin")]
        private static void InitWindow()
        {
            OnionSkinWindow window = GetWindow<OnionSkinWindow>("动画洋葱皮效果设置");
            window.Show();
        }

        private void OnGUI()
        {
            _enable = EditorGUILayout.Toggle("启用", _enable);
            Alpha = EditorGUILayout.Slider("Alpha 衰减系数", Alpha, 0.1f, 0.9f);
            _maxStep = Mathf.FloorToInt(1f / _alpha);
            Step = EditorGUILayout.IntSlider("最大显示步数", Step, 1, _maxStep);
            _alwaysRepaint = EditorGUILayout.Toggle("是否总是重绘", _alwaysRepaint);
        }

        private static void RefreshProperties()
        {
            Window = GetWindow<AnimationWindow>(false, null, false);
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

            ActiveObject = Selection.activeObject as GameObject;
        }

        private static RenderTexture Capture(Camera camera)
        {
            RenderTexture tmp = camera.activeTexture;
            RenderTexture texture = RenderTexture.GetTemporary(tmp.descriptor);
            CameraClearFlags clearFlags = camera.clearFlags;
            int mask = camera.cullingMask;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.cullingMask = -1;
            camera.backgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
            camera.targetTexture = texture;
            camera.Render();
            camera.targetTexture = tmp;
            camera.clearFlags = clearFlags;
            camera.cullingMask = mask;
            return texture;
        }

        private static void DrawRenderTexture(RenderTexture rt, float alpha)
        {
            Handles.BeginGUI();
            if (rt != null)
            {
                Color color = GUI.color;
                GUI.color = new Color(1, 1, 1, alpha);
                GUI.DrawTexture(new Rect(0, 1, rt.width, rt.height), rt);
                GUI.color = color;
            }

            Handles.EndGUI();
        }
        
        private static int CalculateKeyframe(ObjectReferenceKeyframe[] keyframes)
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

        private static void SetOffsetToGameObject(int offset)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(Clip);
            foreach (EditorCurveBinding binding in bindings)
            {
                ObjectReferenceKeyframe[] frames = AnimationUtility.GetObjectReferenceCurve(Clip, binding);
                int keyframe = CalculateKeyframe(frames) + offset;
                Object value = frames[(keyframe + frames.Length * Mathf.Abs(offset)) % frames.Length].value;
                Debug.Log(binding.path);
                GameObject target = binding.path.Length < 1 ? ActiveObject : ActiveObject.transform.Find(binding.path)?.gameObject;
                if (target != null)
                {
                    Component component = target.GetComponent(binding.type);
                    PropertyInfo item = binding.type.GetProperty(binding.propertyName[2..].ToLower());
                    if (item != null)
                    {
                        item.SetValue(component, value);
                    }
                }
            }
        }

        private static void OnGUISceneView(SceneView sceneView)
        {
            if (!_enable)
            {
                DirtyFlag = true;
                return;
            }

            if (Event.current.type is EventType.ScrollWheel or EventType.MouseDown or
                EventType.MouseUp or EventType.MouseDrag or EventType.KeyDown or EventType.KeyUp)
            {
                DirtyFlag = true;
            }

            RefreshProperties();
            if (DirtyFlag && Window != null && Clip != null)
            {
                DirtyFlag = false;
                foreach (RenderTexture rt in RenderTextures)
                {
                    if (rt != null)
                    {
                        RenderTexture.ReleaseTemporary(rt);
                    }
                }

                RenderTextures.Clear();

                for (int i = -Step; i <= Step; i++)
                {
                    if (i != 0)
                    {
                        SetOffsetToGameObject(i);
                        RenderTextures.Add(Capture(sceneView.camera));
                    }
                }

                Window.frame = Frame;
                SetOffsetToGameObject(0);
            }

            int index = -Step;
            foreach (RenderTexture rt in RenderTextures)
            {
                if (index == 0)
                {
                    index++;
                }

                DrawRenderTexture(rt, 1f - Mathf.Abs(index) * Alpha);
                index++;
            }
        }
    }
}