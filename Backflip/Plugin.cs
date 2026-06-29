using System;
using BepInEx;
using BepInEx.Configuration;
using GorillaLocomotion;
using UnityEngine;

namespace Backflip;

[BepInPlugin(Constants.Guid, Constants.ModName, Constants.Version)]
public sealed class Plugin : BaseUnityPlugin
{
    private struct Flip
    {
        public bool active;
        public float start, dir;
        public Quaternion rot;
        public Vector3 axis;
    }

    private ConfigEntry<string>? direction, keybind;
    private ConfigEntry<float>? duration;
    private ConfigEntry<int>? presses;

    private Flip flip;
    private bool wasPressed;
    private int count;
    private float lastPress = float.NegativeInfinity;

    private void Start()
    {
        direction = Config.Bind("Flip", "Direction", "Backflip", "A backflip or frontflip");
        duration = Config.Bind("Flip", "Duration", 0.4f, "How long a flip takes in seconds");
        keybind = Config.Bind("Input", "Keybind", "Y", "Controller bind that triggers the flip (A, B, X, Y)");
        presses = Config.Bind("Input", "Presses", 1, "How many times you have to press the button to trigger a flip (1-3)");
        Config.Save();
    }

    private void OnDisable() => Cancel();
    private void OnDestroy() => Cancel();

    private void Update()
    {
        var poller = ControllerInputPoller.instance;
        if (!poller)
        {
            wasPressed = false;
            Reset();
            Cancel();
            return;
        }

        if (count > 0 && Time.unscaledTime - lastPress > Constants.PressWindow)
            Reset();

        var pressed = ButtonDown(poller);

        if (pressed && !wasPressed && !flip.active)
            Pressed();

        wasPressed = pressed;
    }

    private void LateUpdate()
    {
        if (!flip.active) return;

        var t = (Time.time - flip.start) / Mathf.Max(Constants.MinFlipDuration, duration!.Value);
        if (t >= 1f)
        {
            flip.active = false;
            GTPlayerTransform.ApplyRotationOverride(flip.rot, Time.frameCount);
            return;
        }

        GTPlayerTransform.ApplyRotationOverride(Quaternion.AngleAxis(flip.dir * 360f * t, flip.axis) * flip.rot, Time.frameCount);
    }

    private bool ButtonDown(ControllerInputPoller input) =>
        keybind?.Value.Trim().ToUpperInvariant() switch
        {
            "A" => input.rightControllerPrimaryButton,
            "B" => input.rightControllerSecondaryButton,
            "X" => input.leftControllerPrimaryButton,
            _   => input.leftControllerSecondaryButton,
        };

    private void Pressed()
    {
        var now = Time.unscaledTime;
        if (now - lastPress > Constants.PressWindow)
            count = 0;

        lastPress = now;
        count++;

        if (presses != null && count < presses.Value) return;

        Reset();
        Spin();
    }

    private void Spin()
    {
        var player = GTPlayer.Instance;
        var rig = GorillaTagger.Instance.offlineVRRig;
        if (!player || !player.playerRigidBody || !rig) return;

        flip = new Flip
        {
            active = true,
            rot = player.playerRigidBody.rotation,
            axis = rig.transform.right.normalized,
            start = Time.time,
            dir = string.Equals(direction?.Value, "Frontflip", StringComparison.OrdinalIgnoreCase) ? 1f : -1f,
        };
    }

    private void Cancel()
    {
        if (!flip.active) return;
        
        flip.active = false;
        GTPlayerTransform.ApplyRotationOverride(flip.rot, Time.frameCount);
    }

    private void Reset()
    {
        count = 0;
        lastPress = float.NegativeInfinity;
    }
}