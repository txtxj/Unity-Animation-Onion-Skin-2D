using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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
            if (Selection.objects.Length > 1)
            {
                Debug.LogError("Only One GameObject Should Be Chosen");
                return;
            }

            if (!(Selection.activeObject is GameObject o && (o.TryGetComponent(out Animation _) || o.TryGetComponent(out Animator __))))
            {
                Debug.LogError("Only One GameObject With Component Animation or Animator Can Be Chosen");
                return;
            }

            Debug.LogWarning("The simultaneous use of two onion skin solutions is strictly prohibited.");
            OnionSkinPreviewWindow window = CreateWindow<OnionSkinPreviewWindow>();
            window.selectedObject = Selection.activeObject;
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
    }
}
