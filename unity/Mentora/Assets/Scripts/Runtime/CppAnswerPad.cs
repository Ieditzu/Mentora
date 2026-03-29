using UnityEngine;

public class CppAnswerPad : MonoBehaviour
{
    public bool isCorrect;
    public CppQuestionTrigger questionTrigger;
    private bool submitted;
    private Collider padCollider;

    private void Awake()
    {
        padCollider = GetComponent<Collider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (questionTrigger == null || submitted)
        {
            return;
        }

        if (!TryGetPlayer(other, out BeanController sphere, out FirstPersonControllerSimple fps, out Collider playerCollider))
        {
            return;
        }

        if (playerCollider == null)
        {
            return;
        }

        if (!IsPlayerTouchingPad(playerCollider))
        {
            return;
        }

        submitted = true;
        questionTrigger.SubmitAnswer(isCorrect, sphere, fps);
    }

    private void OnDisable()
    {
        submitted = false;
    }

    private bool IsPlayerTouchingPad(Collider playerCollider)
    {
        if (padCollider == null || playerCollider == null)
        {
            return false;
        }

        Vector3 direction;
        float distance;
        bool overlapping = Physics.ComputePenetration(
            padCollider, padCollider.transform.position, padCollider.transform.rotation,
            playerCollider, playerCollider.transform.position, playerCollider.transform.rotation,
            out direction, out distance);

        return overlapping && distance > 0f;
    }

    private static bool TryGetPlayer(
        Collider other,
        out BeanController sphere,
        out FirstPersonControllerSimple fps,
        out Collider playerCollider)
    {
        sphere = other.GetComponent<BeanController>();
        if (sphere == null)
        {
            sphere = other.GetComponentInParent<BeanController>();
        }

        fps = other.GetComponent<FirstPersonControllerSimple>();
        if (fps == null)
        {
            fps = other.GetComponentInParent<FirstPersonControllerSimple>();
        }

        playerCollider = null;
        if (sphere != null)
        {
            playerCollider = sphere.GetComponent<Collider>();
        }

        if (playerCollider == null && fps != null)
        {
            playerCollider = fps.GetComponent<CharacterController>();
        }

        if (playerCollider == null && fps != null)
        {
            playerCollider = fps.GetComponent<Collider>();
        }

        if (playerCollider == null)
        {
            playerCollider = other;
        }

        return sphere != null || fps != null;
    }
}
