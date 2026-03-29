using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ReattachMissingScripts
{
    [MenuItem("Bila/Reattach Important Scripts")]
    public static void Reattach()
    {
        int count = 0;
        var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();

        foreach (var root in rootObjects)
        {
            // Re-attach triggers to Question Pads
            var triggers = root.GetComponentsInChildren<CppQuestionTrigger>(true);
            foreach (var trigger in triggers)
            {
                if (trigger.answersRoot != null)
                {
                    var pads = trigger.answersRoot.GetComponentsInChildren<Transform>(true);
                    foreach (var padT in pads)
                    {
                        if (padT.name.StartsWith("AnswerPad_") || padT.name.Contains("PadTrigger"))
                        {
                            var answerPad = padT.GetComponent<CppAnswerPad>();
                            if (answerPad == null)
                            {
                                answerPad = padT.gameObject.AddComponent<CppAnswerPad>();
                                answerPad.questionTrigger = trigger;
                                EditorUtility.SetDirty(padT.gameObject);
                                count++;
                            }
                            else if (answerPad.questionTrigger == null)
                            {
                                answerPad.questionTrigger = trigger;
                                EditorUtility.SetDirty(padT.gameObject);
                                count++;
                            }
                        }
                    }
                }
            }
        }
        
        Debug.Log($"Reattached {count} missing scripts!");
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
}
