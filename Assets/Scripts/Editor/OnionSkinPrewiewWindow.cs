using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Utils.Citrine.AnimationOnionSkin
{
    [EditorWindowTitle(title = "Onion Skin", icon = "GameObject Icon")]
    public class OnionSkinPreviewWindow : SceneView
    {
        public AnimationOnionStage stage;
        public Object selectedObject;

        [MenuItem("Animation Tools/Onion Skin Stage")]
        public static void ShowWindow()
        {
            if (Selection.objects.Length > 1 || Selection.activeObject is not GameObject o)
            {
                Debug.LogError("Only One GameObject Should Be Chosen");
                return;
            }
            
            if (EditorUtility.IsPersistent(o) || (o.hideFlags & HideFlags.NotEditable) != 0)
            {
                Debug.LogError("The Selected GameObject Is Not Editable");
                return;
            }
            
            Component anim = GetClosestAnimationPlayerComponentInParents(o.transform);

            if (anim == null)
            {
                Debug.LogError("Only One GameObject With Component Animation or Animator Can Be Chosen");
                return;
            }

            Debug.LogWarning("The Simultaneous Use of Two Onion Skin Solutions Is Strictly Prohibited.");
            OnionSkinPreviewWindow window = CreateWindow<OnionSkinPreviewWindow>();
            window.selectedObject = anim.gameObject;
            window.drawGizmos = false;
            window.SetupWindow();
            window.Close();
        }

        private void SetupWindow()
        {
            titleContent = new GUIContent
            {
                text = selectedObject.name,
                image = EditorGUIUtility.IconContent("GameObject Icon").image
            };

            stage = CreateInstance<AnimationOnionStage>();
            StageUtility.GoToStage(stage, true);
            
            stage.ownerWindow = this;
            stage.titleContent = titleContent;
            stage.SetupScene();

            FrameSelected();
        }
        
        /// <summary>
        /// import from UnityCsReference/Editor/Mono/Animation/AnimationWindow/AnimationWindowUtility.cs
        /// </summary>
        private static Component GetClosestAnimationPlayerComponentInParents(Transform tr)
        {
            while (true)
            {
                if (tr.TryGetComponent(out Animator animator))
                {
                    return animator;
                }

                if (tr.TryGetComponent(out Animation animation))
                {
                    return animation;
                }

                if (tr.TryGetComponent(out IAnimationClipSource clipPlayer))
                {
                    if (clipPlayer is Component clipPlayerComponent)
                    {
                        return clipPlayerComponent;
                    }
                }

                if (tr == tr.root)
                    break;

                tr = tr.parent;
            }
            return null;
        }
    }
}
