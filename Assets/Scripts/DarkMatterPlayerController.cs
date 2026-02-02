using UnityEngine;

/// <summary>
/// 暗物质模拟游戏 - 玩家基础控制。
/// 玩家持续向前移动；相机与玩家保持固定距离，绕玩家旋转视角时始终按该距离、看向玩家。
/// 支持键鼠（WASD）与手柄（双摇杆），Z/手柄A 切回默认跟随视角。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class DarkMatterPlayerController : MonoBehaviour
{
    [Header("移动")]
    [Tooltip("当前前进速度")]
    [SerializeField] float speed = 5f;
    [Tooltip("最小/最大速度")]
    [SerializeField] float minSpeed = 0.5f;
    [SerializeField] float maxSpeed = 20f;
    [Tooltip("每秒速度变化量（W/摇杆上加速，S/摇杆下减速）")]
    [SerializeField] float speedChangePerSecond = 8f;
    [Tooltip("世界空间前进方向（归一化）；碰撞等可修改此向量）")]
    [SerializeField] Vector3 movementDirection = Vector3.forward;

    [Header("相机与玩家相对关系")]
    [Tooltip("相机（应为本物体的子物体）；不填则自动查找子物体上的 Camera")]
    [SerializeField] Transform cameraTransform;
    [Tooltip("相机与玩家的距离；旋转视角时始终保持此距离并看向玩家")]
    [SerializeField] float orbitDistance = 4f;
    [Tooltip("相机相对玩家的水平角（度），可在 Inspector 中调节；运行时会被输入更新")]
    [SerializeField] float cameraYaw;
    [Tooltip("相机相对玩家的俯仰角（度），可在 Inspector 中调节；运行时会被输入更新")]
    [SerializeField] float cameraPitch;
    [Tooltip("勾选后，游戏启动时使用下方「初始水平角/俯仰角」；不勾选则与前进方向一致")]
    [SerializeField] bool useCustomInitialAngles;
    [Tooltip("启动时的水平角（度），仅在「使用自定义初始角度」勾选时生效")]
    [SerializeField] float initialCameraYaw;
    [Tooltip("启动时的俯仰角（度），仅在「使用自定义初始角度」勾选时生效")]
    [SerializeField] float initialCameraPitch;

    [Header("相机轨道 - 旋转速度")]
    [Tooltip("轨道水平旋转速度（度/秒）- 键鼠 A/D")]
    [SerializeField] float keyboardYawSpeed = 90f;
    [Tooltip("轨道垂直旋转速度（度/秒）- 键鼠暂无垂直，手柄右摇杆")]
    [SerializeField] float keyboardPitchSpeed = 60f;
    [Tooltip("手柄右摇杆灵敏度（仅右摇杆旋转视角）")]
    [SerializeField] float controllerLookSensitivity = 120f;
    [Tooltip("俯仰角限制（度）")]
    [SerializeField] float pitchMin = -80f;
    [SerializeField] float pitchMax = 80f;

    [Header("默认跟随视角")]
    [Tooltip("按 Z（键鼠）或 A 键（手柄）后，相机平滑回到默认视角的速度（度/秒）")]
    [SerializeField] float followViewReturnSpeed = 90f;

    [Header("暗物质路径弯曲（球体排斥，3D 含高度；离开后回到原轨道）")]
    [Tooltip("恢复原轨道时的角速度下限（弧度/秒），保证一定能在合理时间内回到原轨道")]
    [SerializeField] float darkMatterReturnSpeedMinRadPerSec = 1.5f;
    [Tooltip("恢复时最多用多少秒回到原方向（避免因在影响区内待太久而导致恢复极慢）")]
    [SerializeField] float darkMatterReturnMaxTime = 3f;
    [Tooltip("高度回落最少用时（秒），避免「从碰到到最高点」时间过短导致除零或过猛")]
    [SerializeField] float darkMatterHeightReturnMinDuration = 0.3f;

    CharacterController _controller;
    bool _isReturningToFollowView;
    Vector3 _defaultMovementDirection;
    bool _isReturningFromDarkMatter;
    bool _hadDarkMatterInfluence;
    float _influenceTime;
    float _maxBendAngleRad;
    float _returnAngularSpeedRadPerSec;
    // 碰到暗物质那一瞬的高度、时间；最高高度及达到时间；离开最高点后花相同时间落回进入高度
    float _darkMatterEntryHeight;
    float _darkMatterEntryTime;
    float _darkMatterMaxHeight;
    float _darkMatterMaxHeightTime;
    float _heightReturnStartTime;
    float _heightReturnDuration;
    bool _isReturningToEntryHeight;
    float _targetFollowYaw;
    float _targetFollowPitch;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        if (cameraTransform == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
        }
        movementDirection = movementDirection.normalized;
        _defaultMovementDirection = movementDirection;
        if (useCustomInitialAngles)
        {
            cameraYaw = initialCameraYaw;
            cameraPitch = initialCameraPitch;
        }
        else
        {
            float moveYaw = Mathf.Atan2(movementDirection.x, movementDirection.z) * Mathf.Rad2Deg;
            cameraYaw = moveYaw;
            cameraPitch = 10f; // 相机一开始跟随玩家时带 10 度俯仰
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // 1. 速度输入：键鼠 W/S，手柄左摇杆仅竖向（Vertical）
        float speedInput = Input.GetAxis("Vertical");
        speed += speedInput * speedChangePerSecond * dt;
        speed = Mathf.Clamp(speed, minSpeed, maxSpeed);

        // 2. 暗物质路径弯曲：前方有暗物质时路线弯曲成弧，离开后平滑恢复
        ApplyDarkMatterBend(dt);

        // 3. 持续向前移动（后续碰撞/重力可在此处或 FixedUpdate 中影响 movementDirection）
        Vector3 move = movementDirection * (speed * dt);
        _controller.Move(move);

        // 4. 切回默认跟随视角：Z 或 手柄 A，触发后平滑过渡（不闪现）
        if (Input.GetKeyDown(KeyCode.Z) || Input.GetButtonDown("Submit"))
            StartReturnToFollowView();

        // 5. 相机旋转：键鼠仅 A/D（水平）；手柄仅右摇杆（水平+垂直），左摇杆不参与视角
        if (_isReturningToFollowView)
        {
            float step = followViewReturnSpeed * dt;
            cameraYaw = Mathf.MoveTowardsAngle(cameraYaw, _targetFollowYaw, step);
            cameraPitch = Mathf.MoveTowards(cameraPitch, _targetFollowPitch, step);
            cameraPitch = Mathf.Clamp(cameraPitch, pitchMin, pitchMax);
            if (Mathf.Abs(Mathf.DeltaAngle(cameraYaw, _targetFollowYaw)) < 0.5f && Mathf.Abs(cameraPitch - _targetFollowPitch) < 0.5f)
                _isReturningToFollowView = false;
        }
        else
        {
            float keyboardYaw = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            float yawInput = keyboardYaw * keyboardYawSpeed * dt
                + Input.GetAxis("RightStickX") * controllerLookSensitivity * dt;
            float pitchInput = Input.GetAxis("RightStickY") * controllerLookSensitivity * dt;
            cameraYaw += yawInput;
            cameraPitch += pitchInput;
            cameraPitch = Mathf.Clamp(cameraPitch, pitchMin, pitchMax);
        }

        // 6. 应用相机位置与朝向（绕玩家轨道，看向玩家）
        ApplyCameraOrbit();
    }

    /// <summary>
    /// 开始平滑回到默认跟随视角（与当前前进方向一致，俯仰为 0）。按 Z 或手柄 A 触发。
    /// </summary>
    public void StartReturnToFollowView()
    {
        _targetFollowYaw = Mathf.Atan2(movementDirection.x, movementDirection.z) * Mathf.Rad2Deg;
        _targetFollowPitch = 0f;
        _isReturningToFollowView = true;
    }

    /// <summary>
    /// 立即将相机设为默认跟随视角（无过渡）。用于初始化等。
    /// </summary>
    public void ResetCameraToFollowView()
    {
        cameraYaw = Mathf.Atan2(movementDirection.x, movementDirection.z) * Mathf.Rad2Deg;
        cameraPitch = 0f;
        _isReturningToFollowView = false;
    }

    /// <summary>
    /// 球体暗物质排斥：路径 3D 弯曲（含高度）。记录碰到瞬间的高度、最高高度及到达时间；
    /// 离开影响后，方向恢复原轨道，高度在「从碰到到最高点」的相同时长内落回碰到时的高度。
    /// </summary>
    void ApplyDarkMatterBend(float dt)
    {
        DarkMatter[] allDarkMatter = FindObjectsOfType<DarkMatter>();
        Vector3 totalDeflection = Vector3.zero;
        Vector3 pos = transform.position;
        float t = Time.time;
        foreach (DarkMatter dm in allDarkMatter)
        {
            if (!dm.IsInInfluence(pos)) continue;
            totalDeflection += dm.GetDeflectionAt(pos, _defaultMovementDirection);
        }

        bool hasInfluence = totalDeflection.sqrMagnitude > 0.0001f;

        if (hasInfluence)
        {
            if (!_hadDarkMatterInfluence)
            {
                Debug.Log("[Dark Matter] 遇到暗物质 / Dark Matter encountered");
                _darkMatterEntryHeight = pos.y;
                _darkMatterEntryTime = t;
                _darkMatterMaxHeight = pos.y;
                _darkMatterMaxHeightTime = t;
            }
            _isReturningFromDarkMatter = false;
            _isReturningToEntryHeight = false;

            if (pos.y > _darkMatterMaxHeight)
            {
                _darkMatterMaxHeight = pos.y;
                _darkMatterMaxHeightTime = t;
            }

            Vector3 bent = (_defaultMovementDirection + totalDeflection).normalized;
            movementDirection = bent;
            _influenceTime += dt;
            float angleRad = Mathf.Acos(Mathf.Clamp01(Vector3.Dot(_defaultMovementDirection, bent)));
            if (angleRad > _maxBendAngleRad) _maxBendAngleRad = angleRad;
        }
        else
        {
            if (_hadDarkMatterInfluence)
            {
                _isReturningFromDarkMatter = true;
                float effectiveTime = Mathf.Max(Mathf.Min(_influenceTime, darkMatterReturnMaxTime), 0.05f);
                float matchedSpeed = _maxBendAngleRad / effectiveTime;
                float speedToFinishInMaxTime = _maxBendAngleRad / darkMatterReturnMaxTime;
                _returnAngularSpeedRadPerSec = Mathf.Max(matchedSpeed, speedToFinishInMaxTime, darkMatterReturnSpeedMinRadPerSec);

                _heightReturnDuration = Mathf.Max(_darkMatterMaxHeightTime - _darkMatterEntryTime, darkMatterHeightReturnMinDuration);
                _heightReturnStartTime = t;
                _isReturningToEntryHeight = true;
            }
            _influenceTime = 0f;
            _maxBendAngleRad = 0f;

            if (_isReturningToEntryHeight)
            {
                float elapsed = t - _heightReturnStartTime;
                float timeRemaining = _heightReturnDuration - elapsed;
                if (timeRemaining <= 0f || Mathf.Abs(pos.y - _darkMatterEntryHeight) < 0.02f)
                {
                    movementDirection = _defaultMovementDirection;
                    _isReturningToEntryHeight = false;
                    _isReturningFromDarkMatter = false;
                }
                else
                {
                    float desiredVy = (_darkMatterEntryHeight - pos.y) / timeRemaining;
                    float dy = Mathf.Clamp(desiredVy / speed, -1f, 1f);
                    Vector3 hor = new Vector3(_defaultMovementDirection.x, 0f, _defaultMovementDirection.z);
                    float horLen = hor.magnitude;
                    if (horLen < 0.0001f) hor = Vector3.forward;
                    else hor /= horLen;
                    float k = Mathf.Sqrt(Mathf.Max(0f, 1f - dy * dy));
                    movementDirection = new Vector3(hor.x * k, dy, hor.z * k).normalized;

                    float angleRad = Mathf.Acos(Mathf.Clamp01(Vector3.Dot(movementDirection, _defaultMovementDirection)));
                    if (angleRad < 0.001f) _isReturningFromDarkMatter = false;
                }
            }
            else if (_isReturningFromDarkMatter)
            {
                float angleRad = Mathf.Acos(Mathf.Clamp01(Vector3.Dot(movementDirection, _defaultMovementDirection)));
                float step = angleRad < 0.001f ? 1f : Mathf.Clamp01((_returnAngularSpeedRadPerSec * dt) / angleRad);
                movementDirection = Vector3.Slerp(movementDirection, _defaultMovementDirection, step);
                if (Vector3.Dot(movementDirection, _defaultMovementDirection) >= 0.9995f)
                {
                    movementDirection = _defaultMovementDirection;
                    _isReturningFromDarkMatter = false;
                }
            }
            else
            {
                _defaultMovementDirection = movementDirection;
            }
        }

        _hadDarkMatterInfluence = hasInfluence;
    }

    void ApplyCameraOrbit()
    {
        if (cameraTransform == null) return;

        // 世界空间：相机始终在离玩家 orbitDistance 的位置，方向由 yaw/pitch 决定
        Vector3 offsetDir = Quaternion.Euler(cameraPitch, cameraYaw, 0f) * Vector3.back;
        Vector3 cameraWorldPos = transform.position + offsetDir * orbitDistance;

        // 相机是子物体：把目标世界位置转成相对玩家的本地位置，随玩家一起移动
        cameraTransform.localPosition = transform.InverseTransformPoint(cameraWorldPos);
        // 始终看向玩家（父物体）
        cameraTransform.localRotation = Quaternion.LookRotation(-cameraTransform.localPosition);
    }

    /// <summary>
    /// 供外部（如碰撞、引力）修改前进方向。请传入归一化方向。
    /// </summary>
    public void SetMovementDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude > 0.001f)
            movementDirection = direction.normalized;
    }

    /// <summary>
    /// 当前前进方向（只读）。
    /// </summary>
    public Vector3 MovementDirection => movementDirection;

    /// <summary>
    /// 当前速度（只读）。
    /// </summary>
    public float Speed => speed;
}
