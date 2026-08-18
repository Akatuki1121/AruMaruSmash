using UnityEngine;

/// <summary>
/// TPS�p�F���E�̌X���Ő���A�O��̌X���őO��ړ�����X�N���v�g�B
/// </summary>
public class TPSPlayerMove : MonoBehaviour
{
    JoyconAccelReceiver JoyAccelRec;
    public Vector3 JoyAccel;

    [Header("���x�ݒ�")]
    public float maxSpeed = 8f;         // �ő呬�x
    public float acceleration = 16f;    // �����x
    public float currentSpeed = 0f;    // ���݂̑��x
    public float brakeSpeed = 40f;     // �����i�u���[�L�j���x
    public float rotationSpeed = 120f;  // ���񑬓x

    private Vector3 moveDirection = Vector3.zero;
    private const float MOVEMENT_VELOCITY_EPSILON = 0.0001f;

    public Rigidbody rb;

    void Start()
    {
        if (JoyAccelRec == null) JoyAccelRec = GetComponent<JoyconAccelReceiver>();
        if (rb == null) rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        Vector3 oldDir = moveDirection;

        // ����ƑO��ړ��̓��͂��擾
        GetMoveDirection();

        // ���������W�b�N�i�O��ړ��̔��]�E�u���[�L�p�j
        if (currentSpeed > 0.1f)
        {
            if (Vector3.Dot(oldDir, moveDirection) < -0.1f)
            {
                currentSpeed -= brakeSpeed * Time.deltaTime;
                currentSpeed = Mathf.Max(currentSpeed, 0f);
            }
            else if (moveDirection.sqrMagnitude > 0)
            {
                currentSpeed += acceleration * Time.deltaTime;
                currentSpeed = Mathf.Min(currentSpeed, maxSpeed);
            }
        }
        else
        {
            if (moveDirection.sqrMagnitude > 0)
            {
                currentSpeed += acceleration * Time.deltaTime;
                currentSpeed = Mathf.Min(currentSpeed, maxSpeed);
            }
        }

        if (moveDirection.sqrMagnitude == 0)
        {
            currentSpeed -= brakeSpeed * Time.deltaTime;
            currentSpeed = Mathf.Max(currentSpeed, 0f);
        }
    }

    private void FixedUpdate()
    {
        PlayerMove(currentSpeed);
    }

    public void PlayerMove(float speed)
    {
        if (moveDirection.sqrMagnitude > 0)
        {
            // �����̍��̌����i�����j�ɑ΂��đO�i�E��ނ̃x�N�g�����v�Z
            Vector3 localMoveDir = transform.rotation * moveDirection;
            Vector3 targetVelocity = localMoveDir * speed;

            // �⓹�Ή�
            Ray ray = new Ray(rb.position + Vector3.up * 0.2f, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, 0.6f))
            {
                targetVelocity = Vector3.ProjectOnPlane(targetVelocity, hit.normal);
            }

            rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        }
        else
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }
    }

    public void GetMoveDirection()
    {
        float moveZ = 0;
        float rotationInput = 0f;

        // �O��̌X���iY���j�ŁA�O�i�i+1�j�܂��͌�ށi-1�j
        if (GetTiltY() > 0.2f) moveZ -= 1f;
        if (GetTiltY() < -0.2f) moveZ += 1f;

        // ���E�̌X���iX���j�ŁA�E����i+1�j�܂��͍�����i-1�j
        if (GetTiltX() > 0.2f) rotationInput += 1f;
        if (GetTiltX() < -0.2f) rotationInput -= 1f;

        // ���̏�ŃL�����N�^�[�̌�������]������
        transform.Rotate(0f, rotationInput * rotationSpeed * Time.deltaTime, 0f);

        // �ړ������͏����ɑO��iZ���j�̓��݂͂̂ɂ���i���E�̉�����͂����Ȃ��j
        Vector3 inputVector = new Vector3(0f, 0f, moveZ);

        if (inputVector.sqrMagnitude > 0)
        {
            moveDirection = inputVector.normalized;
        }
        else
        {
            moveDirection = Vector3.zero;
        }
    }

    public float GetTiltX()
    {
        JoyAccel = JoyAccelRec.GetAccel();
        return JoyAccel.x;
    }

    public float GetTiltY()
    {
        JoyAccel = JoyAccelRec.GetAccel();
        return JoyAccel.y;
    }

    public Vector3 GetInputMoveVelocity()
    {
        return moveDirection * currentSpeed;
    }
}