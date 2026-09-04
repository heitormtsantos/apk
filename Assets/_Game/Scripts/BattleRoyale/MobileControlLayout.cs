using UnityEngine;

namespace BattleRoyale
{
    public static class MobileControlLayout
    {
        public static float MoveRadius { get; private set; } = 62f;
        private static Vector2 moveCenter;
        private static Rect fire;
        private static Rect aim;
        private static Rect jump;
        private static Rect interact;
        private static Rect reload;
        private static Rect heal;
        private static Rect swap;
        private static bool moveEnabled = true;
        private static bool fireEnabled = true;
        private static bool aimEnabled = true;
        private static bool jumpEnabled = true;
        private static bool interactEnabled = true;
        private static bool reloadEnabled = true;
        private static bool healEnabled = true;
        private static bool swapEnabled = true;
        private static bool configured;
        public static Vector2 MoveVisual { get; private set; }
        public static bool FireHeld { get; private set; }
        public static bool AimHeld { get; private set; }

        public static Vector2 MoveCenter => configured ? moveCenter : new(Screen.safeArea.xMin + 92f, Screen.safeArea.yMin + 100f);
        public static Rect Fire => configured ? fire : Centered(Screen.safeArea.xMax - 70f, Screen.safeArea.yMin + 104f, 78f);
        public static Rect Aim => configured ? aim : Centered(Screen.safeArea.xMax - 158f, Screen.safeArea.yMin + 158f, 62f);
        public static Rect Jump => configured ? jump : Centered(Screen.safeArea.xMax - 72f, Screen.safeArea.yMin + 205f, 62f);
        public static Rect Interact => configured ? interact : Centered(Screen.safeArea.xMax - 246f, Screen.safeArea.yMin + 102f, 58f);
        public static Rect Reload => configured ? reload : Centered(Screen.safeArea.xMax - 158f, Screen.safeArea.yMin + 82f, 52f);
        public static Rect Heal => configured ? heal : Centered(Screen.safeArea.xMin + 186f, Screen.safeArea.yMin + 55f, 52f);
        public static Rect Swap => configured ? swap : Centered(Screen.safeArea.xMin + 244f, Screen.safeArea.yMin + 55f, 52f);
        public static bool MoveEnabled => moveEnabled;
        public static bool FireEnabled => fireEnabled;
        public static bool AimEnabled => aimEnabled;
        public static bool JumpEnabled => jumpEnabled;
        public static bool InteractEnabled => interactEnabled;
        public static bool ReloadEnabled => reloadEnabled;
        public static bool HealEnabled => healEnabled;
        public static bool SwapEnabled => swapEnabled;

        public static void Configure(Rect moveGui, Rect fireGui, Rect aimGui, Rect jumpGui, Rect interactGui,
            Rect reloadGui, Rect healGui, Rect swapGui, bool showMove = true, bool showFire = true,
            bool showAim = true, bool showJump = true, bool showInteract = true, bool showReload = true,
            bool showHeal = true, bool showSwap = true)
        {
            var moveScreen = ToScreen(moveGui);
            moveCenter = moveScreen.center;
            MoveRadius = Mathf.Max(24f, Mathf.Min(moveScreen.width, moveScreen.height) * 0.5f);
            fire = ToScreen(fireGui);
            aim = ToScreen(aimGui);
            jump = ToScreen(jumpGui);
            interact = ToScreen(interactGui);
            reload = ToScreen(reloadGui);
            heal = ToScreen(healGui);
            swap = ToScreen(swapGui);
            moveEnabled = showMove;
            fireEnabled = showFire;
            aimEnabled = showAim;
            jumpEnabled = showJump;
            interactEnabled = showInteract;
            reloadEnabled = showReload;
            healEnabled = showHeal;
            swapEnabled = showSwap;
            configured = true;
        }

        public static MobileControlRects Controls(float lookStartX) => new(MoveCenter, MoveRadius,
            Fire, Aim, Jump, Interact, Reload, Heal, Swap, lookStartX, MoveEnabled, FireEnabled,
            AimEnabled, JumpEnabled, InteractEnabled, ReloadEnabled, HealEnabled, SwapEnabled);

        public static bool IsButton(Vector2 point) => Controls(float.PositiveInfinity).IsButton(point);

        public static Rect ToGui(Rect screenRect) => ToGui(screenRect, Screen.height);
        public static Rect ToGui(Rect screenRect, float screenHeight) =>
            new(screenRect.x, screenHeight - screenRect.yMax, screenRect.width, screenRect.height);
        public static Rect ToScreen(Rect guiRect, float screenHeight) =>
            new(guiRect.x, screenHeight - guiRect.yMax, guiRect.width, guiRect.height);

        public static void SetVisualState(Vector2 move, bool fire, bool aim)
        {
            MoveVisual = Vector2.ClampMagnitude(move, 1f);
            FireHeld = fire;
            AimHeld = aim;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            configured = false;
            MoveRadius = 62f;
            moveEnabled = fireEnabled = aimEnabled = jumpEnabled = true;
            interactEnabled = reloadEnabled = healEnabled = swapEnabled = true;
            SetVisualState(Vector2.zero, false, false);
        }

        private static Rect Centered(float x, float y, float size) => new(x - size * 0.5f, y - size * 0.5f, size, size);
        private static Rect ToScreen(Rect guiRect) => ToScreen(guiRect, Screen.height);
    }
}
