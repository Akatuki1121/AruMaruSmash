using UnityEngine;

/// <summary>
/// Scales the character animation playback rate with its commanded movement speed,
/// and turns the model toward its travel direction (yaw only).
/// The yaw is applied to a separate pivot so it never fights with the rotation the animation writes.
/// </summary>
[DisallowMultipleComponent]
public sealed class AnimatorSpeedByPlayerVelocity : MonoBehaviour
{
    [SerializeField] private TopDownPlayerMove movement;
    [SerializeField] private Animator targetAnimator;
    [Tooltip("Yaw only の回転を受け持つ親Transform。未設定ならAwakeでAnimatorの親として自動生成する")]
    [SerializeField] private Transform facingPivot;
    [SerializeField, Min(0.01f)] private float rotationSmoothTime = 0.12f;
    [SerializeField, Min(0f)] private float minFacingSpeed = 0.05f;
    [Tooltip("モデルの前方(+Z)と進行方向のずれを補正する角度。転がる軸(ローカルX)が進行方向と直角になるよう、見た目を見て調整する")]
    [SerializeField] private float yawOffset = 0f;

    private float currentYaw;
    private float rotationVelocity;

    private void Awake()
    {
        if (movement == null) movement = GetComponent<TopDownPlayerMove>();
        if (targetAnimator == null) targetAnimator = GetComponentInChildren<Animator>(true);

        if (targetAnimator == null) return;

        targetAnimator.speed = 0f;
        EnsureFacingPivot();
        currentYaw = transform.eulerAngles.y;
    }

    /// <summary>Animatorの1つ上に、Y回転だけを担当する空のTransformを挟む。</summary>
    private void EnsureFacingPivot()
    {
        if (facingPivot != null) return;

        Transform animatorTransform = targetAnimator.transform;
        if (animatorTransform == transform) return; // 自分自身にAnimatorがある場合は挟めない

        Transform parent = animatorTransform.parent;
        facingPivot = new GameObject("FacingPivot").transform;
        facingPivot.SetParent(parent, false);
        facingPivot.SetSiblingIndex(animatorTransform.GetSiblingIndex());
        animatorTransform.SetParent(facingPivot, false);
    }

    private void Update()
    {
        if (movement == null || targetAnimator == null) return;

        float baseSpeed = movement.GroundMaxSpeed;
        float currentVelocity = movement.GetInputMoveVelocity().magnitude;
        targetAnimator.speed = baseSpeed > 0.001f && currentVelocity > 0f
            ? currentVelocity / baseSpeed
            : 0f;
    }

    private void LateUpdate()
    {
        if (facingPivot == null) return;

        if (TryGetTravelDirection(out Vector3 direction))
        {
            float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            currentYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref rotationVelocity, rotationSmoothTime);
        }

        // ワールド基準のYawのみ。ルートが傾いたり転がったりしても見た目は進行方向を向く
        facingPivot.rotation = Quaternion.Euler(0f, currentYaw + yawOffset, 0f);
    }

    /// <summary>実際に動いている方向（ワールド・水平）を返す。止まっている間はfalseで、直前の向きを保つ。</summary>
    private bool TryGetTravelDirection(out Vector3 direction)
    {
        direction = Vector3.zero;

        if (movement != null)
        {
            direction = movement.GetInputMoveVelocity();
            direction.y = 0f;
            if (direction.sqrMagnitude > minFacingSpeed * minFacingSpeed) return true;
        }

        return false;
    }
}
