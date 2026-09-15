using UnityEngine;

/// <summary>
/// 接地判定のみを担当するクラス。
/// 「Ground」タグを持つコライダーと接触している間はIsGroundedがtrueになる。
///
/// 現時点では平面のみを想定しており、接触点の法線方向（上面か側面か）のチェックは行わない。
/// 将来、壁や斜面との区別が必要になった場合は、OnCollisionStay内で
/// collision.GetContact(0).normal のY成分をチェックする処理を追加すること。
/// </summary>
public class GroundChecker : MonoBehaviour
{
    // 接地判定の閾値
    private const int NO_GROUND_CONTACT = 0;

    [Header("接地判定")]
    [Tooltip("地面と判定するオブジェクトのタグ名")]
    public string groundTag = "Ground";

    // 何個の地面コライダーと同時に接触しているかを数える。
    // 複数の地面オブジェクトに同時に触れている場合でも、
    // 1つでも触れていればIsGroundedはtrueになる。
    private int groundContactCount = 0;

    public bool IsGrounded => groundContactCount > NO_GROUND_CONTACT;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag(groundTag))
        {
            groundContactCount++;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.collider.CompareTag(groundTag))
        {
            groundContactCount = Mathf.Max(NO_GROUND_CONTACT, groundContactCount - 1);
        }
    }
}
