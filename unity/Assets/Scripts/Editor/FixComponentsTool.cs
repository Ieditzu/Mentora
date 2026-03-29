using UnityEditor;
using UnityEngine;

public class FixComponentsTool
{
    [MenuItem("Bila/Fix Question Pads")]
    public static void FixQuestionPads()
    {
        var allPads = Object.FindObjectsOfType<CppAnswerPad>(true);
        int fixedCount = 0;
        foreach (var pad in allPads)
        {
            if (pad.questionTrigger == null)
            {
                // Attempt to find the parent trigger
                var parentTrigger = pad.GetComponentInParent<CppQuestionTrigger>();
                if (parentTrigger == null)
                {
                    // Maybe it's a sibling of the root?
                    if (pad.transform.parent != null && pad.transform.parent.parent != null)
                    {
                        parentTrigger = pad.transform.parent.parent.GetComponentInChildren<CppQuestionTrigger>();
                    }
                }
                
                if (parentTrigger != null)
                {
                    pad.questionTrigger = parentTrigger;
                    EditorUtility.SetDirty(pad);
                    fixedCount++;
                }
                else 
                {
                    Debug.LogWarning($"Could not find CppQuestionTrigger for pad {pad.gameObject.name}");
                }
            }
        }
        
        Debug.Log($"Fixed {fixedCount} CppAnswerPads.");
    }
}
