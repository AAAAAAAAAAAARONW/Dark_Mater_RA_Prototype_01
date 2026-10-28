using getReal3D;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DummyHmdInputs : MonoBehaviour, PlayerInputs {
    public MonoBehaviour behaviour {
        get {
            return this;
        }
    }

    public float YawAxis {
        get {
            return 0;
        }
    }

    public float PitchAxis {
        get {
            return 0;
        }
    }

    public bool WandLookButtonDown {
        get {
            return false;
        }
    }

    public bool WandLookButtonUp {
        get {
            return false;
        }
    }

    public bool WandLookButton {
        get {
            return false;
        }
    }

    public bool WandDriveButtonDown {
        get {
            return false;
        }
    }

    public bool WandDriveButtonUp {
        get {
            return false;
        }
    }

    public bool WandDriveButton {
        get {
            return false;
        }
    }

    public float StrafeAxis {
        get {
            return 0;
        }
    }

    public float ForwardAxis {
        get {
            return 0;
        }
    }

    public float TreadmillRightAxis {
        get {
            return 0;
        }
    }

    public float TreadmillForwardAxis {
        get {
            return 0;
        }
    }

    public bool NavSpeedButtonDown {
        get {
            return false;
        }
    }

    public bool NavSpeedButtonUp {
        get {
            return false;
        }
    }

    public bool NavSpeedButton {
        get {
            return false;
        }
    }

    public bool JumpButtonDown {
        get {
            return false;
        }
    }

    public bool JumpButtonUp {
        get {
            return false;
        }
    }

    public bool JumpButton {
        get {
            return false;
        }
    }

    public bool WandButtonDown {
        get {
            return false;
        }
    }

    public bool WandButtonUp {
        get {
            return false;
        }
    }

    public bool WandButton {
        get {
            return false;
        }
    }

    public bool ChangeWandButtonDown {
        get {
            return false;
        }
    }

    public bool ChangeWandButtonUp {
        get {
            return false;
        }
    }

    public bool ChangeWandButton {
        get {
            return false;
        }
    }

    public bool ResetButtonDown {
        get {
            return false;
        }
    }

    public bool ResetButtonUp {
        get {
            return false;
        }
    }

    public bool ResetButton {
        get {
            return false;
        }
    }

    private Sensor identity = new Sensor();

    public Sensor Wand {
        get {
            return identity;
        }
    }

    public Sensor Head {
        get {
            return identity;
        }
    }

    public Sensor Treadmill {
        get {
            return identity;
        }
    }

    public float UpDownAxis {
        get {
            return 0;
        }
    }
}
