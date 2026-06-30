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

    private ConfigEntry<FlipDirection>? direction;
    private ConfigEntry<ControllerButton>? keybind;
    private ConfigEntry<float>? duration;
    private ConfigEntry<int>? presses;
    private ConfigEntry<bool>? oppositeFlip;

    private Flip flip;
    private bool wasPressed;
    private int count;
    private float lastPress = float.NegativeInfinity;

    private void Start()
    {
        direction = Config.Bind("Flip", "Direction", FlipDirection.Backflip, "A backflip or frontflip");
        duration = Config.Bind("Flip", "Duration", 0.4f, "How long a flip takes in seconds");
        keybind = Config.Bind("Input", "Keybind", ControllerButton.Y, "Controller bind that triggers the flip (A, B, X, Y)");
        presses = Config.Bind("Input", "Presses", 1, "How many times you have to press the button to trigger a flip (1-3)");
        oppositeFlip = Config.Bind("Input", "OppositeFlip", false, "Press the button under/above your keybind to flip the other direction");

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
        var altPressed = OtherButtonDown(poller);

        if (pressed && !wasPressed && !flip.active)
            Pressed(false);
        
        if (altPressed && !wasPressed && !flip.active)
            Pressed(true);

        wasPressed = pressed || altPressed;
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
        keybind?.Value switch
        {
            ControllerButton.A => input.rightControllerPrimaryButton,
            ControllerButton.B => input.rightControllerSecondaryButton,
            ControllerButton.X => input.leftControllerPrimaryButton,
            _                  => input.leftControllerSecondaryButton,
        };

    private bool OtherButtonDown(ControllerInputPoller input) =>
        keybind?.Value switch
        {
            ControllerButton.A => input.rightControllerSecondaryButton,
            ControllerButton.B => input.rightControllerPrimaryButton,
            ControllerButton.X => input.leftControllerSecondaryButton,
            _                  => input.leftControllerPrimaryButton,
        };

    private void Pressed(bool opposite = false)
    {
        var now = Time.unscaledTime;
        if (now - lastPress > Constants.PressWindow)
            count = 0;

        lastPress = now;
        count++;

        if (presses != null && count < presses.Value) return;

        Reset();
        Spin(opposite);
    }

    private void Spin(bool opposite = false)
    {
        var player = GTPlayer.Instance;
        var rig = GorillaTagger.Instance.offlineVRRig;
        if (!player || !player.playerRigidBody || !rig) return;

        var flipDir = 0f;

        if (direction?.Value == FlipDirection.Frontflip && opposite)
            flipDir = -1f;
        else if (direction?.Value == FlipDirection.Frontflip)
            flipDir = 1f;
        
        if (direction?.Value == FlipDirection.Backflip && opposite)
            flipDir = 1f;
        else if (direction?.Value == FlipDirection.Backflip)
            flipDir = -1f;

        flip = new Flip
        {
            active = true,
            rot = player.playerRigidBody.rotation,
            axis = rig.transform.right.normalized,
            start = Time.time,
            dir = flipDir,
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

    // bingus note: BepInEx configs reset these to default value if the field is not valid, you don't need to verify that they are valid
    private enum FlipDirection
    {
        Frontflip = 0,
        Backflip
    }

    private enum ControllerButton
    {
        A = 0,
        B,

        X = 10,
        Y
    }
}