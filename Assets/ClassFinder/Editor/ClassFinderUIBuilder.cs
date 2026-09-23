using ClassFinder.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ClassFinder.EditorTools
{
    /// <summary>
    /// Optional editor helpers. Not needed to run: NextClassController builds its own UI at runtime.
    /// Use "Build Widget In Scene" only if you want to see / restyle the widget in the Hierarchy.
    /// </summary>
    public static class ClassFinderUIBuilder
    {
        [MenuItem("ClassFinder/Build Widget In Scene (optional)")]
        public static void BuildInScene()
        {
            var controller = Object.FindFirstObjectByType<NextClassController>();
            if (controller == null)
            {
                var go = new GameObject("ClassFinder");
                Undo.RegisterCreatedObjectUndo(go, "Create ClassFinder");
                controller = go.AddComponent<NextClassController>();
            }

            var old = controller.transform.Find(NextClassWidgetBuilder.CanvasName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var canvas = NextClassWidgetBuilder.Build(controller);
            canvas.transform.Find("Next Class Card").gameObject.SetActive(true); // visible for editing
            Undo.RegisterCreatedObjectUndo(canvas, "Build ClassFinder Widget");
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            Selection.activeGameObject = canvas;
        }

        [MenuItem("ClassFinder/Clear Saved Student ID")]
        public static void ClearSavedId()
        {
            PlayerPrefs.DeleteKey(NextClassController.StudentIdPrefKey);
            PlayerPrefs.Save();
            Debug.Log("[ClassFinder] Saved student ID cleared.");
        }
    }
}
