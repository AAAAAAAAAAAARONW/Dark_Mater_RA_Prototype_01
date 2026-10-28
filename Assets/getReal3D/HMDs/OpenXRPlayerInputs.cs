#if ENABLE_INPUT_SYSTEM

using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// This class is an OpenXR implementation of PlayerInputs interface.
/// </summary>
public class OpenXRPlayerInputs : MonoBehaviour, PlayerInputs {

    [Tooltip("This must be set to the HMD head in order to retrieve the user head position.")]
    public Transform headTransform;

    [Tooltip("This must be set to the HMD right controller.")]
    public Transform rightControllerTransform;

    public MonoBehaviour behaviour { get { return this; } }

    public InputAction RotateAction;
    public InputAction WandLookButtonAction;
    public InputAction WandDriveButtonAction;
    public InputAction NavigateAction;
    public InputAction TreadmillAction;
    public InputAction NavSpeedButtonAction;
    public InputAction JumpButtonAction;
    public InputAction WandButtonAction;
    public InputAction ChangeWandButtonAction;
    public InputAction ResetButtonAction;

    private void Reset()
    {
        RotateAction = new InputAction(type: InputActionType.Value);
        WandLookButtonAction = new InputAction(type: InputActionType.Button, interactions: "press(behavior=2)");
        WandDriveButtonAction = new InputAction(type: InputActionType.Button, interactions: "press(behavior=2)");
        NavigateAction = new InputAction(type: InputActionType.Value);
        TreadmillAction = new InputAction(type: InputActionType.Value);
        NavSpeedButtonAction = new InputAction(type: InputActionType.Button, interactions: "press(behavior=2)");
        JumpButtonAction = new InputAction(type: InputActionType.Button, interactions: "press(behavior=2)");
        WandButtonAction = new InputAction(type: InputActionType.Button, interactions: "press(behavior=2)");
        ChangeWandButtonAction = new InputAction(type: InputActionType.Button, interactions: "press(behavior=2)");
        ResetButtonAction = new InputAction(type: InputActionType.Button, interactions: "press(behavior=2)");
    }

    private class InputActionBinding {
        public InputActionBinding(InputAction action)
        {
            if (action != null) {
                action.performed += ctx => {
                    bool newState = ctx.ReadValueAsButton();
                    if (state && !newState) {
                        up = true;
                    }
                    else if (!state && newState) {
                        down = true;
                    }
                    state = newState;
                };
                action.Enable();
            }
        }
        public void Update()
        {
            down = false;
            up = false;
        }
        public bool down { get; private set; } = false;
        public bool up { get; private set; } = false;
        public bool state { get; private set; } = false;
    };

    private InputActionBinding WandLookButtonBinding;
    private InputActionBinding WandDriveButtonBinding;
    private InputActionBinding NavSpeedButtonBinding;
    private InputActionBinding JumpButtonBinding;
    private InputActionBinding WandButtonBinding;
    private InputActionBinding ChangeWandButtonBinding;
    private InputActionBinding ResetButtonBinding;

    public void Awake()
    {
        WandLookButtonBinding = new InputActionBinding(WandLookButtonAction);
        WandDriveButtonBinding = new InputActionBinding(WandDriveButtonAction);
        NavSpeedButtonBinding = new InputActionBinding(NavSpeedButtonAction);
        JumpButtonBinding = new InputActionBinding(JumpButtonAction);
        WandButtonBinding = new InputActionBinding(WandButtonAction);
        ChangeWandButtonBinding = new InputActionBinding(ChangeWandButtonAction);
        ResetButtonBinding = new InputActionBinding(ResetButtonAction);

        NavigateAction.Enable();
        NavigateAction.performed += ctx => {
            var v = ctx.ReadValue<Vector2>();
            StrafeAxis = v.x;
            ForwardAxis = v.y;
        };
        NavigateAction.canceled += ctx => { StrafeAxis = 0; ForwardAxis = 0; };

        RotateAction.Enable();
        RotateAction.performed += ctx => {
            var v = ctx.ReadValue<Vector2>();
            YawAxis = v.x;
            PitchAxis = v.y;
        };
        RotateAction.canceled += ctx => { YawAxis = 0; PitchAxis = 0; };

        TreadmillAction.Enable();
        TreadmillAction.performed += ctx => {
            var v = ctx.ReadValue<Vector2>();
            TreadmillRightAxis = v.x;
            TreadmillForwardAxis = v.y;
        };
        TreadmillAction.canceled += ctx => {
            TreadmillRightAxis = 0;
            TreadmillForwardAxis = 0;
        };
    }

    public void Update()
    {
        WandLookButtonBinding.Update();
        WandDriveButtonBinding.Update();
        NavSpeedButtonBinding.Update();
        JumpButtonBinding.Update();
        WandButtonBinding.Update();
        ChangeWandButtonBinding.Update();
        ResetButtonBinding.Update();
    }

    public float YawAxis { get; private set; } = 0;

    public float PitchAxis { get; private set; } = 0;

    public bool WandLookButtonDown { get { return WandLookButtonBinding.down; } }

    public bool WandLookButtonUp { get { return WandLookButtonBinding.up; } }

    public bool WandLookButton { get { return WandLookButtonBinding.state; } }

    public bool WandDriveButtonDown { get { return WandDriveButtonBinding.down; } }

    public bool WandDriveButtonUp { get { return WandDriveButtonBinding.up; } }

    public bool WandDriveButton { get { return WandDriveButtonBinding.state; } }

    public float StrafeAxis { get; private set; } = 0;

    public float ForwardAxis { get; private set; } = 0;

    public float TreadmillRightAxis { get; private set; } = 0;

    public float TreadmillForwardAxis { get; private set; } = 0;

    public bool NavSpeedButtonDown { get { return NavSpeedButtonBinding.down; } }

    public bool NavSpeedButtonUp { get { return NavSpeedButtonBinding.up; } }

    public bool NavSpeedButton { get { return NavSpeedButtonBinding.state; } }

    public bool JumpButtonDown { get { return JumpButtonBinding.down; } }

    public bool JumpButtonUp { get { return JumpButtonBinding.up; } }

    public bool JumpButton { get { return JumpButtonBinding.state; } }

    public bool WandButtonDown { get { return WandButtonBinding.down; } }

    public bool WandButtonUp { get { return WandButtonBinding.up; } }

    public bool WandButton { get { return WandButtonBinding.state; } }

    public bool ChangeWandButtonDown { get { return ChangeWandButtonBinding.down; } }

    public bool ChangeWandButtonUp { get { return ChangeWandButtonBinding.up; } }

    public bool ChangeWandButton { get { return ChangeWandButtonBinding.state; } }

    public bool ResetButtonDown { get { return ResetButtonBinding.down; } }

    public bool ResetButtonUp { get { return ResetButtonBinding.up; } }

    public bool ResetButton { get { return ResetButtonBinding.state; } }

    private getReal3D.Sensor m_identitySensor = new getReal3D.Sensor();

    public getReal3D.Sensor Head { get { return GetSensor(headTransform); } }

    public getReal3D.Sensor Treadmill { get { return m_identitySensor; } }

    public getReal3D.Sensor Wand { get { return GetSensor(rightControllerTransform); } }

    private getReal3D.Sensor GetSensor(Transform t)
    {
        getReal3D.Sensor res;
        res.position = t.localPosition;
        res.rotation = t.localRotation;
        return res;
    }

    public float UpDownAxis {
        get {
            return 0;
        }
    }
}
#else
public class OpenXRPlayerInputs : UnityEngine.MonoBehaviour {
}
#endif
