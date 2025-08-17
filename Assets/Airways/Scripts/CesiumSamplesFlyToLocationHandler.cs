using CesiumForUnity;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System;


#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CesiumSamplesFlyToLocationHandler : MonoBehaviour
{
    [Tooltip("The CesiumFlyToController that will take flight at runtime.")]
    public CesiumFlyToController flyToController;

    [InspectorName("Locations (Longitude Latitude Height")]
    [Tooltip("The locations in Longitude Latitude Height to fly between at runtime." +
             "\n\n" +
             "This script handles up to eight locations. These subscene locations can " +
             "be flown to by pressing the 1-10 keys on the keyboard.")]
    public List<double3> locations = new List<double3>();

    [Tooltip("The desired yaw and pitch angles that the camera should have upon " +
        "flying to the target location." +
        "\n\n" +
        "The first element represents yaw, i.e. horizontal rotation or " +
        "rotation around the Y-axis.\n" +
        "The second element represents yaw, i.e. vertical rotation or " +
        "rotation around the Y-axis.\n" +
        "If no value is provided for a location, Vector2.zero is used by default.")]
    public List<Vector2> yawAndPitchAngles = new List<Vector2>();

    const int locationLimit = 10;

    private void OnValidate()
    {
        if (this.locations.Count > locationLimit)
        {
            this.locations.RemoveRange(locationLimit, this.locations.Count - locationLimit);
        }

        if (this.yawAndPitchAngles.Count > this.locations.Count)
        {
            this.yawAndPitchAngles.RemoveRange(
                this.locations.Count - 1,
                this.yawAndPitchAngles.Count - this.locations.Count);
        }
    }

    void Update()
    {
        if (this.locations.Count == 0)
        {
            return;
        }

        int? keyboardInput = GetKeyboardInput();
        if (keyboardInput == null)
        {
            return;
        }

        int index = (int)keyboardInput - 1;
        this.FlyToLocation(index);
    }

    #region Inputs

    static bool GetKey1Down()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current.digit1Key.isPressed || Keyboard.current.numpad1Key.isPressed;
#else
        return Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
#endif
    }

    static bool GetKey2Down()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current.digit2Key.isPressed || Keyboard.current.numpad2Key.isPressed;
#else
        return Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
#endif
    }

    static int? GetKeyboardInput()
    {
        if (GetKey1Down())
        {
            return 1;
        }

        if (GetKey2Down())
        {
            return 2;
        }

        return null;
    }

    #endregion

    void FlyToLocation(int index)
    {
        if (index >= this.locations.Count)
        {
            return;
        }

        double3 coordinatesLLH = this.locations[index];

        Vector2 yawAndPitch = Vector2.zero;
        if (index < this.yawAndPitchAngles.Count)
        {
            yawAndPitch = this.yawAndPitchAngles[index];
        }

        if (this.flyToController != null)
        {
            this.flyToController.FlyToLocationLongitudeLatitudeHeight(
                coordinatesLLH,
                yawAndPitch.x,
                yawAndPitch.y,
                true);
        }
    }
}
