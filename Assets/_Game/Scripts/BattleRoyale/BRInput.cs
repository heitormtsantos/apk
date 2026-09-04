using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public readonly struct BRInputFrame
    {
        public BRInputFrame(Vector2 move, Vector2 look, bool jump, bool crouch, bool sprint, bool aim,
            bool fire, bool firePressed, bool reload, bool interact, bool heal, bool swap, int weaponSlot,
            bool shoulderSwap, bool drop, bool interactHeld = false, Vector2 fireDragDelta = default,
            AimInputDevice inputDevice = AimInputDevice.Mouse)
        {
            Move = move;
            Look = look;
            Jump = jump;
            Crouch = crouch;
            Sprint = sprint;
            Aim = aim;
            Fire = fire;
            FirePressed = firePressed;
            Reload = reload;
            Interact = interact;
            Heal = heal;
            Swap = swap;
            WeaponSlot = weaponSlot;
            ShoulderSwap = shoulderSwap;
            Drop = drop;
            InteractHeld = interactHeld;
            FireDragDelta = fireDragDelta;
            InputDevice = inputDevice;
        }

        public Vector2 Move { get; }
        public Vector2 Look { get; }
        public bool Jump { get; }
        public bool Crouch { get; }
        public bool Sprint { get; }
        public bool Aim { get; }
        public bool Fire { get; }
        public bool FirePressed { get; }
        public bool Reload { get; }
        public bool Interact { get; }
        public bool Heal { get; }
        public bool Swap { get; }
        public int WeaponSlot { get; }
        public bool ShoulderSwap { get; }
        public bool Drop { get; }
        public bool InteractHeld { get; }
        public Vector2 FireDragDelta { get; }
        public AimInputDevice InputDevice { get; }
    }

    public interface IBRInputSource
    {
        BRInputFrame ReadInput();
    }

    public readonly struct DesktopCombatInput
    {
        public DesktopCombatInput(Vector2 look, bool aim, bool fire, bool firePressed,
            Vector2 fireDragDelta = default)
        {
            Look = look;
            Aim = aim;
            Fire = fire;
            FirePressed = firePressed;
            FireDragDelta = fireDragDelta;
        }

        public Vector2 Look { get; }
        public bool Aim { get; }
        public bool Fire { get; }
        public bool FirePressed { get; }
        public Vector2 FireDragDelta { get; }
    }

    public static class DesktopInputMapper
    {
        public const float MouseLookScale = 2.1f;

        public static DesktopCombatInput Map(Vector2 rawMouseDelta, bool primaryHeld,
            bool primaryPressed, bool secondaryHeld, bool mobileRuntime)
        {
            if (mobileRuntime) return new DesktopCombatInput(Vector2.zero, false, false, false);
            return new DesktopCombatInput(rawMouseDelta * MouseLookScale, secondaryHeld,
                primaryHeld, primaryPressed, primaryHeld ? rawMouseDelta * 0.01f : Vector2.zero);
        }
    }

    public sealed class LocalInputSource : MonoBehaviour, IBRInputSource
    {
        private readonly MobilePointerState pointerState = new();
        private readonly List<MobileTouchSample> touchSamples = new();

        public BRInputFrame ReadInput()
        {
            var mobileRuntime = Application.isMobilePlatform && !Application.isEditor;
            var move = Vector2.zero;
            var look = Vector2.zero;
            if (Input.GetKey(KeyCode.A)) move.x -= 1f;
            if (Input.GetKey(KeyCode.D)) move.x += 1f;
            if (Input.GetKey(KeyCode.S)) move.y -= 1f;
            if (Input.GetKey(KeyCode.W)) move.y += 1f;
            var jump = Input.GetKeyDown(KeyCode.Space);
            var drop = jump;
            var crouch = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
            var sprint = Input.GetKey(KeyCode.LeftShift);
            var reload = Input.GetKeyDown(KeyCode.R);
            var interact = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F);
            var interactHeld = Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.F);
            var heal = Input.GetKeyDown(KeyCode.Alpha4);
            var swap = false;
            var weaponSlot = Input.GetKeyDown(KeyCode.Alpha1) ? 0 : Input.GetKeyDown(KeyCode.Alpha2) ? 1 : -1;
            var shoulderSwap = Input.GetKeyDown(KeyCode.Q);

            var desktop = DesktopInputMapper.Map(
                new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")),
                Input.GetMouseButton(0), Input.GetMouseButtonDown(0), Input.GetMouseButton(1), mobileRuntime);
            look += desktop.Look;
            var aim = desktop.Aim;
            var fire = desktop.Fire;
            var firePressed = desktop.FirePressed;
            var fireDragDelta = desktop.FireDragDelta;

            if (mobileRuntime)
                ReadTouch(ref move, ref look, ref jump, ref crouch, ref sprint, ref aim, ref fire,
                    ref firePressed, ref reload, ref interact, ref interactHeld, ref heal, ref swap, ref drop,
                    ref fireDragDelta);
            return new BRInputFrame(Vector2.ClampMagnitude(move, 1f), look, jump, crouch, sprint, aim,
                fire, firePressed, reload, interact, heal, swap, weaponSlot, shoulderSwap, drop, interactHeld,
                fireDragDelta, mobileRuntime ? AimInputDevice.Touch : AimInputDevice.Mouse);
        }

        private void ReadTouch(ref Vector2 move, ref Vector2 look, ref bool jump, ref bool crouch,
            ref bool sprint, ref bool aim, ref bool fire, ref bool firePressed, ref bool reload, ref bool interact,
            ref bool interactHeld, ref bool heal, ref bool swap, ref bool drop, ref Vector2 fireDragDelta)
        {
            touchSamples.Clear();
            for (var index = 0; index < Input.touchCount; index++)
            {
                var control = Input.GetTouch(index);
                touchSamples.Add(new MobileTouchSample(control.fingerId, control.position,
                    control.deltaPosition, control.phase));
            }

            var controls = MobileControlLayout.Controls(Screen.safeArea.center.x);
            var touch = pointerState.RouteTouches(touchSamples, controls, GamePreferences.FireDragSensitivity);
            move += touch.Move;
            look += touch.Look;
            sprint |= touch.Sprint;
            aim |= touch.Aim;
            fire |= touch.Fire;
            firePressed |= touch.FirePressed;
            fireDragDelta += touch.FireDragDelta;
            jump |= touch.Jump;
            reload |= touch.Reload;
            interact |= touch.Interact;
            interactHeld |= touch.InteractHeld;
            heal |= touch.Heal;
            swap |= touch.Swap;
            drop |= touch.Drop;
            MobileControlLayout.SetVisualState(move, fire, aim);
        }

        public void ResetTransientState()
        {
            pointerState.Reset();
            MobileControlLayout.SetVisualState(Vector2.zero, false, false);
        }
    }
}
