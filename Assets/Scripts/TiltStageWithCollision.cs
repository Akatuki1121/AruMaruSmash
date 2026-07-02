using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 円形シーソー状の傾く床。
/// 乗っているオブジェクトの質量と位置からトルクを疑似計算し、
/// maxTiltAngle の範囲内で傾く。
/// 4人プレイ・押し合いがある環境向けにガタつき対策済み。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class TiltStageWithCollision : MonoBehaviour
{
    [Header("傾きパラメータ")]

    /// <summary>
    /// トルクを角度に変換する際の感度。
    /// 値が大きいほど、少し乗っただけでも大きく傾くようになる。
    /// </summary>
    public float tiltSensitivity = 0.3f;

    /// <summary>
    /// ステージが傾くことのできる最大角度（度）。
    /// X軸・Z軸を合成したベクトルの大きさでクランプされるため、
    /// 斜め方向に傾いてもこの値を超えない。
    /// </summary>
    public float maxTiltAngle = 30f;

    /// <summary>
    /// 現在の傾きから目標の傾き(targetRot)へ追従する速さ。
    /// 大きいほど反応が速くなるが、入力(torque)のブレにも敏感になる。
    /// </summary>
    public float stabilizationSpeed = 10f;

    [Header("ガタつき対策")]

    /// <summary>
    /// OnCollisionExit が呼ばれてから、実際に接触リストから削除するまでの猶予フレーム数。
    /// 接触判定が一瞬だけ Exit→Enter を繰り返すチラつきを吸収するためのバッファ。
    /// 値を大きくするほどチラつきには強くなるが、本当に離れた際の反応がその分遅れる。
    /// </summary>
    [Tooltip("OnCollisionExit後、実際に接触リストから削除するまでの猶予フレーム数")]
    public int exitGraceFrames = 3;

    /// <summary>
    /// 乗っているオブジェクトごとの位置を平滑化する強さ。
    /// 0に近いほど過去の位置を維持しなめらかになるが追従が遅れ、
    /// 1に近いほど毎フレームの実位置をそのまま反映し追従は速いが振動しやすい。
    /// </summary>
    [Tooltip("個々のオブジェクト位置を平滑化する強さ（0=平滑化なし、1=即反映）")]
    [Range(0f, 1f)] public float positionSmoothing = 0.3f;

    /// <summary>
    /// 全オブジェクト分を合算した後のトルク自体を平滑化する速度。
    /// プレイヤー同士の押し合いによる急激な位置変化や、
    /// 複数人分のトルクが同時に合算されることで生じるノイズを吸収する。
    /// 値を下げるほど滑らかになるが、傾きの反応がワンテンポ遅れる。
    /// </summary>
    [Tooltip("合成トルク自体を平滑化する速度。押し合いによる急変化を吸収する")]
    public float torqueSmoothSpeed = 8f;

    private Rigidbody stageRb;
    private readonly Dictionary<GameObject, float> objectMasses = new();
    private readonly Dictionary<GameObject, Vector3> objectPositions = new();
    private readonly Dictionary<GameObject, int> exitGraceCounters = new();

    private Vector3 smoothedTorque = Vector3.zero;

    private void Awake()
    {
        stageRb = GetComponent<Rigidbody>();
        stageRb.useGravity = false;
        stageRb.isKinematic = true;
        stageRb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    // 子のコライダーは親の Rigidbody の複合コライダー扱いになるため、
    // 衝突コールバックはこの親スクリプトに届く
    private void OnCollisionStay(Collision collision)
    {
        GameObject obj = collision.gameObject;

        if (!objectMasses.ContainsKey(obj))
        {
            if (!obj.TryGetComponent<Rigidbody>(out Rigidbody rb)) return;
            objectMasses[obj] = rb.mass;
        }

        // 再接触したら猶予カウンターは解除（消える予定だったが復帰した扱い）
        exitGraceCounters.Remove(obj);

        // 接触点ではなくオブジェクト自身の位置を使う（MeshColliderの接触点ジッター回避）
        Vector3 newPos = collision.transform.position;
        if (objectPositions.TryGetValue(obj, out Vector3 oldPos))
        {
            objectPositions[obj] = Vector3.Lerp(oldPos, newPos, Mathf.Max(positionSmoothing, 0.0001f));
        }
        else
        {
            objectPositions[obj] = newPos;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        GameObject obj = collision.gameObject;
        // 即削除せず、猶予フレームを設定する（接触判定の一瞬のチラつき対策）
        if (objectMasses.ContainsKey(obj))
        {
            exitGraceCounters[obj] = exitGraceFrames;
        }
    }

    private void FixedUpdate()
    {
        CleanupStaleEntries();

        // --- 生のトルクを計算 ---
        Vector3 rawTorque = Vector3.zero;
        Vector3 center = transform.position;
        foreach (var kvp in objectMasses)
        {
            if (!objectPositions.TryGetValue(kvp.Key, out Vector3 pos)) continue;
            Vector3 offset = pos - center;
            offset.y = 0f;
            rawTorque += Vector3.Cross(offset, kvp.Value * Physics.gravity);
        }

        // --- トルク自体を平滑化（押し合いによる急変化・複数人分の合算ノイズを吸収）---
        smoothedTorque = Vector3.Lerp(smoothedTorque, rawTorque, torqueSmoothSpeed * Time.fixedDeltaTime);

        // --- 角度に変換し、合成ベクトルとしてクランプ（斜め傾き時の超過を防ぐ）---
        float rawX = smoothedTorque.x * tiltSensitivity;
        float rawZ = smoothedTorque.z * tiltSensitivity;
        Vector2 tilt = new(rawX, rawZ);
        if (tilt.magnitude > maxTiltAngle)
        {
            tilt = tilt.normalized * maxTiltAngle;
        }

        Quaternion targetRot = Quaternion.Euler(tilt.x, 0f, tilt.y);
        Quaternion newRot = Quaternion.Slerp(
            stageRb.rotation, targetRot, stabilizationSpeed * Time.fixedDeltaTime);

        // Kinematic Rigidbody は必ず MoveRotation で動かす（テレポート扱いを防ぐ）
        stageRb.MoveRotation(newRot);
    }

    /// <summary>
    /// 猶予切れのオブジェクト、および Destroy/SetActive(false)/コンポーネント無効化などで
    /// OnCollisionExit が呼ばれずに残留したオブジェクトを掃除する。
    /// </summary>
    private void CleanupStaleEntries()
    {
        if (objectMasses.Count == 0) return;

        List<GameObject> toRemove = null;

        foreach (var key in objectMasses.Keys)
        {
            if (!ShouldRemove(key)) continue;
            (toRemove ??= new List<GameObject>()).Add(key);
        }

        if (toRemove == null) return;

        foreach (var key in toRemove)
        {
            objectMasses.Remove(key);
            objectPositions.Remove(key);
            exitGraceCounters.Remove(key);
        }

        // 破棄済み／非アクティブ、または猶予切れなら true を返す
        bool ShouldRemove(GameObject obj)
        {
            // 破棄済み、または非アクティブ化されたオブジェクト
            if (obj == null || !obj.activeInHierarchy) return true;

            // 接触が継続中なら削除対象ではない
            if (!exitGraceCounters.TryGetValue(obj, out int frames)) return false;

            // Exit猶予期間が切れたオブジェクト
            frames--;
            if (frames > 0)
            {
                exitGraceCounters[obj] = frames;
                return false;
            }
            return true;
        }
    }
}