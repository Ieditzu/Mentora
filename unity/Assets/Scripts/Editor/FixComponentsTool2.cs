using UnityEditor;
using UnityEngine;

public class FixComponentsTool2
{
    [MenuItem("Bila/Fix Question Pads (Aggressive)")]
    public static void FixQuestionPads()
    {
        var allTriggers = Object.FindObjectsOfType<CppQuestionTrigger>(true);
        int fixedCount = 0;

        foreach (var trigger in allTriggers)
        {
            if (trigger.answersRoot != null)
            {
                var pads = trigger.answersRoot.GetComponentsInChildren<CppAnswerPad>(true);
                foreach (var pad in pads)
                {
                    if (pad.questionTrigger == null || pad.questionTrigger != trigger)
                    {
                        pad.questionTrigger = trigger;
                        EditorUtility.SetDirty(pad);
                        fixedCount++;
                    }
                }
            }
        }
        
        Debug.Log($"Fixed {fixedCount} CppAnswerPads by linking them to their known parent triggers.");
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }
}
