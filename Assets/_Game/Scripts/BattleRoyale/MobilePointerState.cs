using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public readonly struct MobileTouchSample
    {
        public MobileTouchSample(int fingerId, Vector2 position, Vector2 delta, TouchPhase phase)
        {
            FingerId = fingerId;
            Position = position;
            Delta = delta;
            Phase = phase;
        }

        public int FingerId { get; }
        public Vector2 Position { get; }
        public Vector2 Delta { get; }
        public TouchPhase Phase { get; }
    }

    public readonly struct MobileControlRects
    {
        public MobileControlRects(Vector2 moveCenter, float moveRadius, Rect fire, Rect aim, Rect jump,
            Rect interact, Rect reload, Rect heal, Rect swap, float lookStartX,
            bool moveEnabled = true, bool fireEnabled = true, bool aimEnabled = true,
            bool jumpEnabled = true, bool interactEnabled = true, bool reloadEnabled = true,
            bool healEnabled = true, bool swapEnabled = true)
        {
            MoveCenter = moveCenter;
            MoveRadius = moveRadius;
            Fire = fire;
            Aim = aim;
            Jump = jump;
            Interact = interact;
            Reload = reload;
            Heal = heal;
            Swap = swap;
            LookStartX = lookStartX;
            MoveEnabled = moveEnabled;
            FireEnabled = fireEnabled;
            AimEnabled = aimEnabled;
            JumpEnabled = jumpEnabled;
            InteractEnabled = interactEnabled;
            ReloadEnabled = reloadEnabled;
            HealEnabled = healEnabled;
            SwapEnabled = swapEnabled;
        }

        public Vector2 MoveCenter { get; }
        public float MoveRadius { get; }
        public Rect Fire { get; }
        public Rect Aim { get; }
        public Rect Jump { get; }
        public Rect Interact { get; }
        public Rect Reload { get; }
        public Rect Heal { get; }
        public Rect Swap { get; }
        public float LookStartX { get; }
        public bool MoveEnabled { get; }
        public bool FireEnabled { get; }
        public bool AimEnabled { get; }
        public bool JumpEnabled { get; }
        public bool InteractEnabled { get; }
        public bool ReloadEnabled { get; }
        public bool HealEnabled { get; }
        public bool SwapEnabled { get; }

        public bool IsButton(Vector2 position) => (FireEnabled && Fire.Contains(position))
            || (AimEnabled && Aim.Contains(position)) || (JumpEnabled && Jump.Contains(position))
            || (InteractEnabled && Interact.Contains(position)) || (ReloadEnabled && Reload.Contains(position))
            || (HealEnabled && Heal.Contains(position)) || (SwapEnabled && Swap.Contains(position));
    }

    public readonly struct MobileInputFrame
    {
        public MobileInputFrame(Vector2 move, Vector2 look, bool sprint, bool aim, bool fire, bool firePressed,
            bool jump, bool reload, bool interact, bool interactHeld, bool heal, bool swap, bool drop,
            Vector2 fireDragDelta = default)
        {
            Move = move;
            Look = look;
            Sprint = sprint;
            Aim = aim;
            Fire = fire;
            FirePressed = firePressed;
            Jump = jump;
            Reload = reload;
            Interact = interact;
            InteractHeld = interactHeld;
            Heal = heal;
            Swap = swap;
            Drop = drop;
            FireDragDelta = fireDragDelta;
        }

        public Vector2 Move { get; }
        public Vector2 Look { get; }
        public bool Sprint { get; }
        public bool Aim { get; }
        public bool Fire { get; }
        public bool FirePressed { get; }
        public bool Jump { get; }
        public bool Reload { get; }
        public bool Interact { get; }
        public bool InteractHeld { get; }
        public bool Heal { get; }
        public bool Swap { get; }
        public bool Drop { get; }
        public Vector2 FireDragDelta { get; }
    }

    public sealed class MobilePointerState
    {
        public const float FireDragDeadzone = 3.5f;
        public const float LookScale = 0.055f;

        public int FireFingerId { get; private set; } = -1;
        public int LookFingerId { get; private set; } = -1;
        public int MovementFingerId { get; private set; } = -1;
        public int AimFingerId { get; private set; } = -1;
        public int InteractFingerId { get; private set; } = -1;

        public bool TryBeginFire(int fingerId, Vector2 position, Rect fireRect)
        {
            if (fingerId < 0 || FireFingerId >= 0 || IsOwned(fingerId) || !fireRect.Contains(position)) return false;
            FireFingerId = fingerId;
            return true;
        }

        public bool TryBeginLook(int fingerId, bool isOverButton)
        {
            if (fingerId < 0 || isOverButton || IsOwned(fingerId) || LookFingerId >= 0) return false;
            LookFingerId = fingerId;
            return true;
        }

        public bool IsFiring(int fingerId) => FireFingerId == fingerId;
        public bool IsLooking(int fingerId) => LookFingerId == fingerId;

        public Vector2 FilterFireLook(int fingerId, Vector2 delta, TouchPhase phase, float sensitivity)
        {
            if (!IsFiring(fingerId) || phase == TouchPhase.Began || delta.magnitude <= FireDragDeadzone)
                return Vector2.zero;
            return delta * LookScale * sensitivity;
        }

        public MobileInputFrame RouteTouches(IReadOnlyList<MobileTouchSample> samples,
            MobileControlRects controls, float fireDragSensitivity, float normalizationDimension = 0f)
        {
            var move = Vector2.zero;
            var look = Vector2.zero;
            var sprint = false;
            var aim = false;
            var fire = false;
            var firePressed = false;
            var jump = false;
            var reload = false;
            var interact = false;
            var interactHeld = false;
            var heal = false;
            var swap = false;
            var drop = false;
            var fireDragDelta = Vector2.zero;
            var dragDimension = normalizationDimension > 0f
                ? normalizationDimension : Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height));
            var count = samples?.Count ?? 0;

            for (var index = 0; index < count; index++)
            {
                var sample = samples[index];
                if (IsTerminal(sample.Phase)) End(sample.FingerId);
            }
            ReleaseDisabledRoles(controls);

            for (var index = 0; index < count; index++)
            {
                var sample = samples[index];
                var id = sample.FingerId;
                if (IsTerminal(sample.Phase)) continue;

                var began = sample.Phase == TouchPhase.Began;
                if (MovementFingerId == id)
                {
                    move = MovementFor(sample.Position, controls);
                    sprint = move.magnitude > 0.86f;
                    continue;
                }
                if (IsFiring(id))
                {
                    fire = true;
                    var fireLook = FilterFireLook(id, sample.Delta, sample.Phase, fireDragSensitivity);
                    look += fireLook;
                    if (fireLook.sqrMagnitude > 0f)
                        fireDragDelta += NormalizeScreenDelta(sample.Delta, dragDimension) * fireDragSensitivity;
                    continue;
                }
                if (IsLooking(id))
                {
                    if (!began) look += sample.Delta * LookScale;
                    continue;
                }
                if (AimFingerId == id)
                {
                    aim = true;
                    continue;
                }
                if (InteractFingerId == id)
                {
                    interactHeld = true;
                    continue;
                }

                if (!began) continue;

                if (controls.MoveEnabled && TryBeginMovement(id, sample.Position, controls))
                {
                    move = MovementFor(sample.Position, controls);
                    sprint = move.magnitude > 0.86f;
                }
                else if (controls.FireEnabled && TryBeginFire(id, sample.Position, controls.Fire))
                {
                    fire = true;
                    firePressed = true;
                }
                else if (controls.AimEnabled && controls.Aim.Contains(sample.Position) && TryBeginAim(id)) aim = true;
                else if (controls.InteractEnabled && controls.Interact.Contains(sample.Position) && TryBeginInteract(id))
                {
                    interact = true;
                    interactHeld = true;
                }
                else if (controls.JumpEnabled && controls.Jump.Contains(sample.Position)) { jump = true; drop = true; }
                else if (controls.ReloadEnabled && controls.Reload.Contains(sample.Position)) reload = true;
                else if (controls.HealEnabled && controls.Heal.Contains(sample.Position)) heal = true;
                else if (controls.SwapEnabled && controls.Swap.Contains(sample.Position)) swap = true;
                else if (sample.Position.x >= controls.LookStartX
                    && TryBeginLook(id, controls.IsButton(sample.Position)))
                {
                    // Capture without applying the legacy Began delta.
                }
            }

            if (count == 0) Reset();
            return new MobileInputFrame(move, look, sprint, aim, fire, firePressed, jump, reload,
                interact, interactHeld, heal, swap, drop, fireDragDelta);
        }

        public static Vector2 NormalizeScreenDelta(Vector2 delta, float minimumScreenDimension) =>
            delta / Mathf.Max(1f, minimumScreenDimension);

        private static bool IsTerminal(TouchPhase phase) =>
            phase == TouchPhase.Ended || phase == TouchPhase.Canceled;

        private bool TryBeginMovement(int fingerId, Vector2 position, MobileControlRects controls)
        {
            if (MovementFingerId >= 0 || IsOwned(fingerId) || controls.IsButton(position)
                || Vector2.Distance(position, controls.MoveCenter) > controls.MoveRadius * 1.45f) return false;
            MovementFingerId = fingerId;
            return true;
        }

        private bool TryBeginAim(int fingerId)
        {
            if (AimFingerId >= 0 || IsOwned(fingerId)) return false;
            AimFingerId = fingerId;
            return true;
        }

        private bool TryBeginInteract(int fingerId)
        {
            if (InteractFingerId >= 0 || IsOwned(fingerId)) return false;
            InteractFingerId = fingerId;
            return true;
        }

        private bool IsOwned(int fingerId) => fingerId == MovementFingerId || fingerId == FireFingerId
            || fingerId == LookFingerId || fingerId == AimFingerId || fingerId == InteractFingerId;

        private static Vector2 MovementFor(Vector2 position, MobileControlRects controls) =>
            Vector2.ClampMagnitude((position - controls.MoveCenter) / Mathf.Max(1f, controls.MoveRadius), 1f);

        private void ReleaseDisabledRoles(MobileControlRects controls)
        {
            if (!controls.MoveEnabled) MovementFingerId = -1;
            if (!controls.FireEnabled) FireFingerId = -1;
            if (!controls.AimEnabled) AimFingerId = -1;
            if (!controls.InteractEnabled) InteractFingerId = -1;
        }

        public void End(int fingerId)
        {
            if (FireFingerId == fingerId) FireFingerId = -1;
            if (LookFingerId == fingerId) LookFingerId = -1;
            if (MovementFingerId == fingerId) MovementFingerId = -1;
            if (AimFingerId == fingerId) AimFingerId = -1;
            if (InteractFingerId == fingerId) InteractFingerId = -1;
        }

        public void Reset()
        {
            FireFingerId = -1;
            LookFingerId = -1;
            MovementFingerId = -1;
            AimFingerId = -1;
            InteractFingerId = -1;
        }
    }
}
