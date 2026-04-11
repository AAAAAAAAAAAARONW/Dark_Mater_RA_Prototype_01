using System.Collections;
using UnityEngine;
using Cinemachine;

/// <summary>
/// 基于 DarkMatterPlayerControllerTest 的探索版：玩家在水平面内自由移动（非自动走直线）。
/// 输入：WASD 与 左摇杆（Unity Input Manager 的 Horizontal / Vertical）。
/// 移动方向相对相机水平朝向；右摇杆专管视角旋转，鼠标为键鼠方案；A / Space 复位视角。
/// 除 FreeLook 外，可在 Inspector 中指定 CinemachineVirtualCamera（Aim=POV 或 Body=OrbitalTransposer），由同一套右摇杆/鼠标驱动。
/// 保留暗物质路径弯曲与外部速度 API，便于与过场脚本配合。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class DarkMatterPlayerExplorerController : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("最大水平移动速度（米/秒）")]
    [SerializeField] float moveSpeed = 5f;
    [Tooltip("输入死区（左摇杆 / 键轴）")]
    [SerializeField] float moveInputDeadband = 0.12f;
    [Tooltip("用于计算「前后左右」的相机；不填则用 Camera.main")]
    [SerializeField] Transform cameraFacingReference;

    [Header("Cinemachine - FreeLook Cameras")]
    [SerializeField] CinemachineFreeLook vcamMacro;
    [SerializeField] CinemachineFreeLook vcamMicro;

    [Header("Cinemachine - VirtualCamera (POV / Orbital)")]
    [Tooltip("普通 Cinemachine Virtual Camera：右摇杆/鼠标会驱动 Aim=POV 的水平/俯仰，或仅 Body=OrbitalTransposer 的水平轨道角。"
             + "（本脚本屏蔽了 GetInputAxis，不拖到这里则 VCam 不会自己吃到输入。）")]
    [SerializeField] CinemachineVirtualCamera[] virtualCamerasLook;
    [Tooltip("POV 俯仰轴增量（度/秒）；与 FreeLook 的 Y（0~1）分开，避免手感不对")]
    [SerializeField] float povPitchDegreesPerSec = 90f;
    [Tooltip("按 A / Space 复位视角时，POV 俯仰回到的角度（度）")]
    [SerializeField] float povResetVerticalAngle;
    [Tooltip("POV 俯仰角复位角速度（度/秒）")]
    [SerializeField] float povResetVerticalDegreesPerSec = 90f;

    [Header("Cinemachine - Right stick & mouse look")]
    [Tooltip("水平视角灵敏度（右摇杆 X、鼠标 X）")]
    [SerializeField] float xSensitivity = 300f;
    [Tooltip("垂直视角灵敏度（右摇杆 Y、鼠标 Y；FreeLook Y 为 0~1，宜保持较小）")]
    [SerializeField] float ySensitivity = 2f;
    [Tooltip("右摇杆向量模长超过此值时，本帧只用右摇杆控制相机、不用鼠标，避免两路输入互相抢")]
    [SerializeField] float rightStickLookDeadband = 0.12f;
    [Tooltip("部分手柄驱动下右摇杆可能映射到别的轴：填 Input Manager 里的轴名作为备用 X（留空不启用）")]
    [SerializeField] string alternateRightStickXAxis = "";
    [Tooltip("同上，备用右摇杆纵轴名")]
    [SerializeField] string alternateRightStickYAxis = "";

    [Header("Cinemachine - Input Deadband")]
    [Tooltip("右摇杆单轴死区（与 rightStickLookDeadband 配合）；也用于与旧逻辑一致")]
    [SerializeField] float stickDeadband = 0.1f;

    [Header("Cinemachine - Reset View (A Button / Space)")]
    [SerializeField] float resetXSpeed = 180f;
    [SerializeField] float resetYSpeed = 1.5f;
    [SerializeField] float normalDeadZoneWidth = 0.3f;
    [SerializeField] float normalDeadZoneHeight = 0.3f;

    [Header("Dark Matter Path Bending")]
    [SerializeField] float darkMatterReturnSpeedMinRadPerSec = 1.5f;
    [SerializeField] float darkMatterReturnMaxTime = 3f;
    [SerializeField] float darkMatterHeightReturnMinDuration = 0.3f;

    CharacterController _controller;

    float _externalSpeedMultiplier = 1f;
    bool _isResettingView;
    bool _cameraInputLocked;

    Vector3 _defaultMovementDirection;
    Vector3 _movementDirection;
    float _currentInputMagnitude;
    bool _isReturningFromDarkMatter;
    bool _hadDarkMatterInfluence;
    float _influenceTime;
    float _maxBendAngleRad;
    float _returnAngularSpeedRadPerSec;
    float _darkMatterEntryHeight;
    float _darkMatterEntryTime;
    float _darkMatterMaxHeight;
    float _darkMatterMaxHeightTime;
    float _heightReturnStartTime;
    float _heightReturnDuration;
    bool _isReturningToEntryHeight;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _movementDirection = transform.forward;
        _movementDirection.y = 0f;
        if (_movementDirection.sqrMagnitude < 0.0001f) _movementDirection = Vector3.forward;
        _movementDirection.Normalize();
        _defaultMovementDirection = _movementDirection;

        CinemachineCore.GetInputAxis = (axisName) => 0f;
    }

    void OnEnable()
    {
        CinemachineCore.GetInputAxis = (axisName) => 0f;
    }

    void OnDisable()
    {
        CinemachineCore.GetInputAxis = null;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        Vector3 planarIntent = ReadPlanarMoveIntent();
        _currentInputMagnitude = planarIntent.magnitude;
        Vector3 intentDir = _currentInputMagnitude > moveInputDeadband ? planarIntent.normalized : Vector3.zero;

        ApplyDarkMatterBend(dt, intentDir);

        float finalSpeed = moveSpeed * Mathf.Clamp01(_currentInputMagnitude) * _externalSpeedMultiplier;
        if (_movementDirection.sqrMagnitude > 0.0001f && finalSpeed > 0f)
            _controller.Move(_movementDirection * (finalSpeed * dt));

        if (!_cameraInputLocked && !_isResettingView)
            DriveCameraLookInput(dt);

        if (Input.GetButtonDown("Submit") || Input.GetKeyDown(KeyCode.Space))
            _isResettingView = true;

        if (_isResettingView)
            TickResetView(dt);
    }

    /// <summary>
    /// 相机水平面内的前后左右：Vertical 对应前后，Horizontal 对应左右（WASD + 左摇杆）。
    /// </summary>
    Vector3 ReadPlanarMoveIntent()
    {
        Vector2 raw = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        if (raw.sqrMagnitude > 1f) raw.Normalize();

        Transform camTf = cameraFacingReference;
        if (camTf == null && Camera.main != null) camTf = Camera.main.transform;
        if (camTf == null) camTf = transform;

        Vector3 forward = camTf.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;
        forward.Normalize();

        Vector3 right = camTf.right;
        right.y = 0f;
        if (right.sqrMagnitude < 0.0001f) right = transform.right;
        right.Normalize();

        return forward * raw.y + right * raw.x;
    }

    void DriveCameraLookInput(float dt)
    {
        float rawStickX = ReadRightStickAxisRaw("RightStickX", alternateRightStickXAxis);
        float rawStickY = ReadRightStickAxisRaw("RightStickY", alternateRightStickYAxis);

        float stickMagSq = rawStickX * rawStickX + rawStickY * rawStickY;
        float lookDead = Mathf.Max(rightStickLookDeadband, stickDeadband);
        bool useRightStick = stickMagSq >= lookDead * lookDead;

        float yawDelta;
        float freeLookYDelta;
        float rawLookY;
        if (useRightStick)
        {
            yawDelta = Mathf.Abs(rawStickX) > stickDeadband ? rawStickX * xSensitivity * dt : 0f;
            freeLookYDelta = Mathf.Abs(rawStickY) > stickDeadband ? rawStickY * ySensitivity * dt : 0f;
            rawLookY = rawStickY;
        }
        else
        {
            yawDelta = Input.GetAxisRaw("Mouse X") * xSensitivity * dt;
            freeLookYDelta = Input.GetAxisRaw("Mouse Y") * ySensitivity * dt;
            rawLookY = Input.GetAxisRaw("Mouse Y");
        }

        if (vcamMacro != null)
        {
            vcamMacro.m_XAxis.Value += yawDelta;
            vcamMacro.m_YAxis.Value = Mathf.Clamp01(vcamMacro.m_YAxis.Value + freeLookYDelta);
        }

        if (vcamMicro != null)
        {
            vcamMicro.m_XAxis.Value += yawDelta;
            vcamMicro.m_YAxis.Value = Mathf.Clamp01(vcamMicro.m_YAxis.Value + freeLookYDelta);
        }

        float povPitchDelta = Mathf.Abs(rawLookY) > stickDeadband ? rawLookY * povPitchDegreesPerSec * dt : 0f;
        DriveVirtualCameraLook(yawDelta, povPitchDelta);
    }

    void DriveVirtualCameraLook(float yawDeltaDegrees, float povPitchDeltaDegrees)
    {
        if (virtualCamerasLook == null) return;

        for (int i = 0; i < virtualCamerasLook.Length; i++)
        {
            CinemachineVirtualCamera vcam = virtualCamerasLook[i];
            if (vcam == null) continue;

            CinemachinePOV pov = vcam.GetCinemachineComponent<CinemachinePOV>();
            if (pov != null)
            {
                pov.m_HorizontalAxis.Value += yawDeltaDegrees;
                pov.m_VerticalAxis.Value = Mathf.Clamp(
                    pov.m_VerticalAxis.Value + povPitchDeltaDegrees,
                    pov.m_VerticalAxis.m_MinValue,
                    pov.m_VerticalAxis.m_MaxValue);
                continue;
            }

            CinemachineOrbitalTransposer orbital = vcam.GetCinemachineComponent<CinemachineOrbitalTransposer>();
            if (orbital != null)
                orbital.m_XAxis.Value += yawDeltaDegrees;
        }
    }

    /// <summary>
    /// 使用 Raw 轴减少平滑延迟；主轴接近零时再尝试备用轴名（兼容不同手柄映射）。
    /// </summary>
    static float ReadRightStickAxisRaw(string primaryAxis, string alternateAxis)
    {
        float v = Input.GetAxisRaw(primaryAxis);
        if (Mathf.Abs(v) > 0.0001f || string.IsNullOrEmpty(alternateAxis))
            return v;
        return Input.GetAxisRaw(alternateAxis);
    }

    void TickResetView(float dt)
    {
        bool xDone = true;
        bool yDone = true;

        if (vcamMacro != null)
        {
            SetDeadZones(vcamMacro, 0f, 0f);
            vcamMacro.m_XAxis.Value = Mathf.MoveTowardsAngle(vcamMacro.m_XAxis.Value, 0f, resetXSpeed * dt);
            vcamMacro.m_YAxis.Value = Mathf.MoveTowards(vcamMacro.m_YAxis.Value, 0.5f, resetYSpeed * dt);
            xDone &= Mathf.Abs(Mathf.DeltaAngle(vcamMacro.m_XAxis.Value, 0f)) < 0.5f;
            yDone &= Mathf.Abs(vcamMacro.m_YAxis.Value - 0.5f) < 0.01f;
        }

        if (vcamMicro != null)
        {
            SetDeadZones(vcamMicro, 0f, 0f);
            vcamMicro.m_XAxis.Value = Mathf.MoveTowardsAngle(vcamMicro.m_XAxis.Value, 0f, resetXSpeed * dt);
            vcamMicro.m_YAxis.Value = Mathf.MoveTowards(vcamMicro.m_YAxis.Value, 0.5f, resetYSpeed * dt);
            xDone &= Mathf.Abs(Mathf.DeltaAngle(vcamMicro.m_XAxis.Value, 0f)) < 0.5f;
            yDone &= Mathf.Abs(vcamMicro.m_YAxis.Value - 0.5f) < 0.01f;
        }

        if (virtualCamerasLook != null)
        {
            for (int i = 0; i < virtualCamerasLook.Length; i++)
            {
                CinemachineVirtualCamera vcam = virtualCamerasLook[i];
                if (vcam == null) continue;

                CinemachinePOV pov = vcam.GetCinemachineComponent<CinemachinePOV>();
                if (pov != null)
                {
                    pov.m_HorizontalAxis.Value = Mathf.MoveTowardsAngle(pov.m_HorizontalAxis.Value, 0f, resetXSpeed * dt);
                    pov.m_VerticalAxis.Value = Mathf.MoveTowards(
                        pov.m_VerticalAxis.Value,
                        povResetVerticalAngle,
                        povResetVerticalDegreesPerSec * dt);
                    xDone &= Mathf.Abs(Mathf.DeltaAngle(pov.m_HorizontalAxis.Value, 0f)) < 0.5f;
                    yDone &= Mathf.Abs(pov.m_VerticalAxis.Value - povResetVerticalAngle) < 0.5f;
                    continue;
                }

                CinemachineOrbitalTransposer orbital = vcam.GetCinemachineComponent<CinemachineOrbitalTransposer>();
                if (orbital != null)
                {
                    orbital.m_XAxis.Value = Mathf.MoveTowardsAngle(orbital.m_XAxis.Value, 0f, resetXSpeed * dt);
                    xDone &= Mathf.Abs(Mathf.DeltaAngle(orbital.m_XAxis.Value, 0f)) < 0.5f;
                }
            }
        }

        if (xDone && yDone)
        {
            if (vcamMacro != null) SetDeadZones(vcamMacro, normalDeadZoneWidth, normalDeadZoneHeight);
            if (vcamMicro != null) SetDeadZones(vcamMicro, normalDeadZoneWidth, normalDeadZoneHeight);
            _isResettingView = false;
        }
    }

    void SetDeadZones(CinemachineFreeLook vcam, float width, float height)
    {
        for (int i = 0; i < 3; i++)
        {
            var rig = vcam.GetRig(i);
            if (rig == null) continue;
            var composer = rig.GetCinemachineComponent<CinemachineComposer>();
            if (composer == null) continue;
            composer.m_DeadZoneWidth = width;
            composer.m_DeadZoneHeight = height;
        }
    }

    void ApplyDarkMatterBend(float dt, Vector3 intentDir)
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
                Debug.Log("[Dark Matter] Encountered dark matter influence.");
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
            _movementDirection = bent;
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
                    _movementDirection = _defaultMovementDirection;
                    _isReturningToEntryHeight = false;
                    _isReturningFromDarkMatter = false;
                }
                else
                {
                    float spd = Mathf.Max(moveSpeed * Mathf.Clamp01(_currentInputMagnitude), 0.01f);
                    float desiredVy = (_darkMatterEntryHeight - pos.y) / timeRemaining;
                    float dy = Mathf.Clamp(desiredVy / spd, -1f, 1f);
                    Vector3 hor = new Vector3(_defaultMovementDirection.x, 0f, _defaultMovementDirection.z);
                    float horLen = hor.magnitude;
                    if (horLen < 0.0001f) hor = Vector3.forward;
                    else hor /= horLen;
                    float k = Mathf.Sqrt(Mathf.Max(0f, 1f - dy * dy));
                    _movementDirection = new Vector3(hor.x * k, dy, hor.z * k).normalized;

                    float angleRad = Mathf.Acos(Mathf.Clamp01(Vector3.Dot(_movementDirection, _defaultMovementDirection)));
                    if (angleRad < 0.001f) _isReturningFromDarkMatter = false;
                }
            }
            else if (_isReturningFromDarkMatter)
            {
                float angleRad = Mathf.Acos(Mathf.Clamp01(Vector3.Dot(_movementDirection, _defaultMovementDirection)));
                float step = angleRad < 0.001f ? 1f : Mathf.Clamp01((_returnAngularSpeedRadPerSec * dt) / angleRad);
                _movementDirection = Vector3.Slerp(_movementDirection, _defaultMovementDirection, step);

                if (Vector3.Dot(_movementDirection, _defaultMovementDirection) >= 0.9995f)
                {
                    _movementDirection = _defaultMovementDirection;
                    _isReturningFromDarkMatter = false;
                }
            }
            else
            {
                if (intentDir.sqrMagnitude > 0.0001f)
                {
                    _movementDirection = intentDir;
                    _defaultMovementDirection = _movementDirection;
                }
                else
                {
                    _movementDirection = Vector3.zero;
                }
            }
        }

        _hadDarkMatterInfluence = hasInfluence;
    }

    public void SetMovementDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude > 0.001f)
        {
            _movementDirection = direction.normalized;
            _defaultMovementDirection = _movementDirection;
        }
    }

    public void SetExternalSpeedMultiplier(float multiplier)
    {
        _externalSpeedMultiplier = Mathf.Max(0f, multiplier);
    }

    public IEnumerator TweenExternalSpeedMultiplier(float targetMultiplier, float duration)
    {
        float from = _externalSpeedMultiplier;
        float to = Mathf.Max(0f, targetMultiplier);
        float d = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < d)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / d);
            float eased = t * t * (3f - 2f * t);
            _externalSpeedMultiplier = Mathf.Lerp(from, to, eased);
            yield return null;
        }

        _externalSpeedMultiplier = to;
    }

    public void SetCameraInputLocked(bool locked)
    {
        _cameraInputLocked = locked;
        if (locked) _isResettingView = false;
    }

    /// <summary>当前用于位移的方向（含暗物质弯曲）；无输入且不在影响区内时可能为零向量。</summary>
    public Vector3 MovementDirection => _movementDirection;

    /// <summary>配置的最大移动速度（不乘输入幅度与外部倍率）。</summary>
    public float MoveSpeed => moveSpeed;

    /// <summary>与 Test 脚本兼容：当前水平速度标量（含输入幅度与外部倍率）。</summary>
    public float Speed => moveSpeed * Mathf.Clamp01(_currentInputMagnitude) * _externalSpeedMultiplier;
}
