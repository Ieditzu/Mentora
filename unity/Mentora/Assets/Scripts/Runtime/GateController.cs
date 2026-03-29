using UnityEngine;
using System.Collections.Generic;

public class GateController : MonoBehaviour
{
    public Transform leftPivot;
    public Transform rightPivot;
    public Collider leftLeafCollider;
    public Collider rightLeafCollider;
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float rotateSpeed = 180f;
    [SerializeField] private float closeDelay = 0.2f;

    private bool isBallNear;
    private float leaveTime;
    private Quaternion leftClosedRotation;
    private Quaternion rightClosedRotation;
    private float openDirectionSign = 1f;
    private readonly HashSet<Collider> trackedPlayers = new HashSet<Collider>();

    private void Start()
    {
        if (leftPivot == null || rightPivot == null)
        {
            enabled = false;
            return;
        }

        leftClosedRotation = leftPivot.localRotation;
        rightClosedRotation = rightPivot.localRotation;
    }

    private void Update()
    {
        bool shouldOpen = isBallNear || (Time.time - leaveTime) < closeDelay;
        Quaternion targetLeftOpen = leftClosedRotation * Quaternion.Euler(0f, openAngle * openDirectionSign, 0f);
        Quaternion targetRightOpen = rightClosedRotation * Quaternion.Euler(0f, -openAngle * openDirectionSign, 0f);

        if (leftPivot != null)
        {
            Quaternion leftTarget = shouldOpen ? targetLeftOpen : leftClosedRotation;
            leftPivot.localRotation = Quaternion.RotateTowards(leftPivot.localRotation, leftTarget, rotateSpeed * Time.deltaTime);
        }

        if (rightPivot != null)
        {
            Quaternion rightTarget = shouldOpen ? targetRightOpen : rightClosedRotation;
            rightPivot.localRotation = Quaternion.RotateTowards(rightPivot.localRotation, rightTarget, rotateSpeed * Time.deltaTime);
        }

        bool openedEnough = shouldOpen &&
            Quaternion.Angle(leftPivot.localRotation, targetLeftOpen) < 5f &&
            Quaternion.Angle(rightPivot.localRotation, targetRightOpen) < 5f;

        if (leftLeafCollider != null)
        {
            leftLeafCollider.enabled = !openedEnough;
        }

        if (rightLeafCollider != null)
        {
            rightLeafCollider.enabled = !openedEnough;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other))
        {
            trackedPlayers.Add(other);
            UpdateOpenDirection(other.transform.position);
            isBallNear = true;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (IsPlayer(other))
        {
            UpdateOpenDirection(other.transform.position);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
        {
            trackedPlayers.Remove(other);
            if (trackedPlayers.Count == 0)
            {
                isBallNear = false;
                leaveTime = Time.time;
            }
        }
    }

    private bool IsPlayer(Collider other)
    {
        return other.GetComponent<BeanController>() != null
               || other.GetComponent<CharacterController>() != null
               || other.GetComponentInParent<FirstPersonControllerSimple>() != null;
    }

    private void UpdateOpenDirection(Vector3 playerPosition)
    {
        Vector3 toPlayer = playerPosition - transform.position;
        float side = Vector3.Dot(transform.forward, toPlayer);
        // If player is in front (+), open away to front; if behind (-), mirror the swing.
        openDirectionSign = side >= 0f ? 1f : -1f;
    }
}
