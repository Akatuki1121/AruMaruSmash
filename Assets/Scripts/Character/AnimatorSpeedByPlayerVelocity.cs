using UnityEngine;

/// <summary>Scales the character animation playback rate with its commanded movement speed.</summary>
[DisallowMultipleComponent]
public sealed class AnimatorSpeedByPlayerVelocity : MonoBehaviour
{
    [SerializeField] private TopDownPlayerMove movement;
    [SerializeField] private Animator targetAnimator;

    private void Awake()
    {
        if (movement == null) movement = GetComponent<TopDownPlayerMove>();
        if (targetAnimator == null) targetAnimator = GetComponentInChildren<Animator>(true);
        if (targetAnimator != null) targetAnimator.speed = 0f;
    }

    private void Update()
    {
        if (movement == null || targetAnimator == null) return;

        float baseSpeed = movement.groundMoveSpeed.Resolve(
            movement.stats != null ? movement.stats.maxSpeed : CharacterStats.Fallback.maxSpeed);
        float currentVelocity = movement.GetInputMoveVelocity().magnitude;
        targetAnimator.speed = baseSpeed > 0.001f ? currentVelocity / baseSpeed : 0f;
    }
}
