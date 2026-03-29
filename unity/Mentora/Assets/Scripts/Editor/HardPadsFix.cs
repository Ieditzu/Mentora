using UnityEditor;
using UnityEngine;

public class HardPadsFix
{
    [MenuItem("Bila/Fix Hard Question Pads Specifically")]
    public static void FixHardPads()
    {
        var allTriggers = Object.FindObjectsOfType<CppQuestionTrigger>(true);
        int fixedCount = 0;

        foreach (var trigger in allTriggers)
        {
            if (trigger.answersRoot != null)
            {
                // Find anything named something like "Hard...PadTrigger" or "AnswerPad_" inside this tree
                var transforms = trigger.answersRoot.GetComponentsInChildren<Transform>(true);
                foreach (var t in transforms)
                {
                    if (t.name.Contains("PadTrigger") || t.name.StartsWith("AnswerPad_"))
                    {
                        var pad = t.GetComponent<CppAnswerPad>();
                        if (pad == null)
                        {
                            pad = t.gameObject.AddComponent<CppAnswerPad>();
                        }
                        if (pad.questionTrigger == null || pad.questionTrigger != trigger)
                        {
                            pad.questionTrigger = trigger;
                            EditorUtility.SetDirty(t.gameObject);
                            fixedCount++;
                        }
                    }
                }
            }
        }
        
        Debug.Log($"Aggressively Fixed {fixedCount} CppAnswerPads in Question areas.");
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }
}
