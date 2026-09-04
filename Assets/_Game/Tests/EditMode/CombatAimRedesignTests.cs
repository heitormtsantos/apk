using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace BattleRoyale.Tests
{
    public sealed class CombatAimRedesignTests
    {
        [Test]
        public void LegacyFireModePreservesExistingAutomaticFlag()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.fireMode = WeaponFireMode.Legacy;
            weapon.automatic = false;
            Assert.That(weapon.ResolvedFireMode, Is.EqualTo(WeaponFireMode.SemiAutomatic));
            weapon.automatic = true;
            Assert.That(weapon.ResolvedFireMode, Is.EqualTo(WeaponFireMode.Automatic));
            Object.DestroyImmediate(weapon);
        }

        [Test]
        public void TriggerSeparatesSemiAutomaticAutomaticAndBurst()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.fireInterval = 0.1f;
            var trigger = new WeaponTriggerController();

            weapon.fireMode = WeaponFireMode.SemiAutomatic;
            Assert.That(trigger.ShouldFire(weapon, true, true, 0f), Is.True);
            Assert.That(trigger.ShouldFire(weapon, true, false, 0.2f), Is.False);

            weapon.fireMode = WeaponFireMode.Automatic;
            trigger.Reset(weapon);
            Assert.That(trigger.ShouldFire(weapon, true, true, 0f), Is.True);
            Assert.That(trigger.ShouldFire(weapon, true, false, 0.1f), Is.True);

            weapon.fireMode = WeaponFireMode.Burst;
            weapon.burstCount = 3;
            weapon.burstInterval = 0.05f;
            trigger.Reset(weapon);
            Assert.That(trigger.ShouldFire(weapon, true, true, 0f), Is.True);
            Assert.That(trigger.ShouldFire(weapon, false, false, 0.05f), Is.True);
            Assert.That(trigger.ShouldFire(weapon, false, false, 0.1f), Is.True);
            Assert.That(trigger.ShouldFire(weapon, false, false, 0.15f), Is.False);
            Object.DestroyImmediate(weapon);
        }

        [Test]
        public void FirstHipFireShotIsMoreAccurateThanContinuousFireAndRecovers()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.standingSpreadDegrees = 1f;
            weapon.firstShotSpreadMultiplier = 0.4f;
            weapon.continuousFireSpreadIncrease = 0.5f;
            weapon.maxHipFireSpread = 2f;
            weapon.spreadRecoverySpeed = 10f;
            var spread = new WeaponSpread();

            var firstShot = spread.Evaluate(weapon, false, false, false, false, false);
            spread.RegisterShot(weapon);
            var sustained = spread.Evaluate(weapon, false, false, false, false, false);
            Assert.That(firstShot, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(sustained, Is.GreaterThan(firstShot));
            spread.Tick(weapon, 0.5f);
            Assert.That(spread.ShotsInSequence, Is.EqualTo(0));
            Assert.That(spread.Evaluate(weapon, false, false, false, false, false),
                Is.EqualTo(firstShot).Within(0.001f));
            Object.DestroyImmediate(weapon);
        }

        [Test]
        public void FireDragIsResolutionNormalizedAndKeepsFireFingerCaptured()
        {
            var pointer = new MobilePointerState();
            var fire = new Rect(800f, 100f, 100f, 100f);
            var controls = new MobileControlRects(new Vector2(120f, 120f), 80f, fire,
                new Rect(), new Rect(), new Rect(), new Rect(), new Rect(), new Rect(), 500f);
            pointer.RouteTouches(new[]
            {
                new MobileTouchSample(7, fire.center, Vector2.zero, TouchPhase.Began)
            }, controls, 1f, 1000f);

            var frame = pointer.RouteTouches(new[]
            {
                new MobileTouchSample(7, new Vector2(980f, 220f), new Vector2(10f, 5f), TouchPhase.Moved)
            }, controls, 1f, 1000f);

            Assert.That(frame.Fire, Is.True);
            Assert.That(pointer.IsFiring(7), Is.True);
            Assert.That(frame.FireDragDelta.x, Is.EqualTo(0.01f).Within(0.0001f));
            Assert.That(frame.FireDragDelta.y, Is.EqualTo(0.005f).Within(0.0001f));
        }

        [Test]
        public void DesktopFireDragIsReportedOnlyWhilePrimaryIsHeld()
        {
            var idle = DesktopInputMapper.Map(new Vector2(2f, 3f), false, false, false, false);
            var firing = DesktopInputMapper.Map(new Vector2(2f, 3f), true, true, false, false);

            Assert.That(idle.FireDragDelta, Is.EqualTo(Vector2.zero));
            Assert.That(firing.FireDragDelta, Is.EqualTo(new Vector2(0.02f, 0.03f)));
        }

        [Test]
        public void UpwardHipFireDragDisablesOnlyVerticalMagnetism()
        {
            var ownerObject = new GameObject("Owner");
            var targetObject = new GameObject("Target");
            var cameraObject = new GameObject("Camera");
            var owner = ownerObject.AddComponent<BRParticipant>();
            var target = targetObject.AddComponent<BRParticipant>();
            var camera = cameraObject.AddComponent<Camera>();
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            owner.Configure("Owner", true, 0, 1);
            target.Configure("Target", false, 1, 2);
            owner.SetPhase(ParticipantPhase.Grounded);
            target.SetPhase(ParticipantPhase.Grounded);
            ownerObject.transform.position = new Vector3(-5f, 0f, -2f);
            targetObject.transform.position = new Vector3(0f, 0f, 10f);
            var chest = AimAssistTargeting.AimPoint(target, AimPointPreference.Chest);
            cameraObject.transform.position = new Vector3(0f, chest.y, -2f);
            cameraObject.transform.LookAt(chest);
            Physics.SyncTransforms();
            var controller = new AimAssistController();

            SetAimAssistTarget(controller, target);
            controller.UpdateHipFireState(camera, weapon, true, true,
                new Vector2(0f, weapon.dragBreakThresholdTouch * 1.2f), AimInputDevice.Touch, 1f);

            Assert.That(controller.VerticalState, Is.EqualTo(VerticalAimState.VerticalOverride));
            Assert.That(controller.CurrentHorizontalStrength, Is.GreaterThan(0f));
            Assert.That(controller.CurrentVerticalStrength, Is.EqualTo(0f));
            Assert.That(controller.SensitivityScaleByAxis(weapon, AimInputDevice.Touch).y,
                Is.EqualTo(1f).Within(0.001f));

            Object.DestroyImmediate(weapon);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(ownerObject);
        }

        [Test]
        public void HeadCorridorAppliesFrictionWithoutVerticalMagnetism()
        {
            var ownerObject = new GameObject("Owner");
            var targetObject = new GameObject("Target");
            var cameraObject = new GameObject("Camera");
            var owner = ownerObject.AddComponent<BRParticipant>();
            var target = targetObject.AddComponent<BRParticipant>();
            var camera = cameraObject.AddComponent<Camera>();
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            owner.Configure("Owner", true, 0, 11);
            target.Configure("Target", false, 1, 22);
            owner.SetPhase(ParticipantPhase.Grounded);
            target.SetPhase(ParticipantPhase.Grounded);
            ownerObject.transform.position = new Vector3(-5f, 0f, -2f);
            targetObject.transform.position = new Vector3(0f, 0f, 10f);
            var head = AimAssistTargeting.AimPoint(target, AimPointPreference.Head);
            cameraObject.transform.position = new Vector3(0f, head.y, -2f);
            cameraObject.transform.LookAt(head);
            Physics.SyncTransforms();
            var controller = new AimAssistController();
            SetAimAssistTarget(controller, target);
            controller.UpdateHipFireState(camera, weapon, true, true,
                new Vector2(0f, weapon.dragBreakThresholdTouch * 1.2f), AimInputDevice.Touch, 1f);
            controller.UpdateHipFireState(camera, weapon, true, true, Vector2.zero,
                AimInputDevice.Touch, 1f + weapon.verticalUnlockDuration + 0.01f);
            var scale = controller.SensitivityScaleByAxis(weapon, AimInputDevice.Touch);

            Assert.That(controller.VerticalState, Is.EqualTo(VerticalAimState.UpperBodyFreeAim));
            Assert.That(controller.CurrentVerticalStrength, Is.EqualTo(0f));
            Assert.That(scale.y, Is.LessThan(1f));

            Object.DestroyImmediate(weapon);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(ownerObject);
        }

        [Test]
        public void AimAssistNeverMagnetizesTowardHeadPreference()
        {
            var targetObject = new GameObject("Target");
            var cameraObject = new GameObject("Camera");
            var target = targetObject.AddComponent<BRParticipant>();
            var camera = cameraObject.AddComponent<Camera>();
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.aimPointPreference = AimPointPreference.Head;
            target.Configure("Target", false, 1, 2);
            target.SetPhase(ParticipantPhase.Grounded);
            targetObject.transform.position = new Vector3(0f, 0f, 10f);
            var chest = AimAssistTargeting.AimPoint(target, AimPointPreference.Chest);
            cameraObject.transform.position = new Vector3(0f, chest.y, 0f);
            cameraObject.transform.LookAt(chest);
            var controller = new AimAssistController();
            SetAimAssistTarget(controller, target);
            controller.UpdateHipFireState(camera, weapon, false, true, Vector2.zero,
                AimInputDevice.Touch, 1f);

            var corrected = controller.CorrectAngles(0f, 0f, camera.transform.position, weapon,
                AimInputDevice.Touch, true, 0f, 1f, -35f, 62f);

            Assert.That(corrected.y, Is.EqualTo(0f).Within(0.001f));

            Object.DestroyImmediate(weapon);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(targetObject);
        }

        [Test]
        public void AimPointDefaultsToChestInsteadOfHead()
        {
            var root = new GameObject("Target");
            var participant = root.AddComponent<BRParticipant>();
            var chest = AimAssistTargeting.AimPoint(participant);
            var head = AimAssistTargeting.AimPoint(participant, AimPointPreference.Head);
            Assert.That(chest.y, Is.LessThan(head.y));
            Assert.That(chest.y, Is.EqualTo(CharacterPresentationProfile.VisualHeight * 0.68f).Within(0.001f));
            Object.DestroyImmediate(root);
        }

        [Test]
        public void AimSolverConvergesCameraRayAndStopsAtMuzzleCover()
        {
            var owner = new GameObject("Owner");
            var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cover.transform.position = new Vector3(0f, 0f, 1.5f);
            cover.transform.localScale = new Vector3(1f, 1f, 0.2f);
            Physics.SyncTransforms();

            var viewRay = new Ray(new Vector3(0f, 2f, 0f), Vector3.forward);
            var solution = AimSolver.Solve(viewRay, Vector3.zero, 50f, Vector2.zero, owner, ~0);

            Assert.That(solution.IsFinite, Is.True);
            Assert.That(solution.MuzzleObstructed, Is.True);
            Assert.That(solution.EndPoint.z, Is.LessThan(2f));
            Assert.That(Vector3.Angle(solution.MuzzleRay.direction,
                solution.AimPoint - solution.MuzzleRay.origin), Is.LessThan(0.01f));

            Object.DestroyImmediate(cover);
            Object.DestroyImmediate(owner);
        }

        [Test]
        public void MouseAimAssistIsWeakerThanTouchByDefault()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            Assert.That(AimAssistController.DeviceMultiplier(weapon, AimInputDevice.Mouse),
                Is.LessThan(AimAssistController.DeviceMultiplier(weapon, AimInputDevice.Touch)));
            Object.DestroyImmediate(weapon);
        }

        [Test]
        public void AimStateTransitionsSmoothlyAndReverses()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            var scope = ScriptableObject.CreateInstance<ScopeDefinition>();
            scope.scopeType = ScopeType.Scope4X;
            scope.transitionSpeed = 2f;
            weapon.scope = scope;
            var state = new AimStateController();

            Assert.That(state.Tick(true, weapon, 0.25f), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(state.Mode, Is.EqualTo(ScopeType.Scope4X));
            Assert.That(state.Tick(false, weapon, 0.1f), Is.EqualTo(0.3f).Within(0.001f));

            Object.DestroyImmediate(scope);
            Object.DestroyImmediate(weapon);
        }

        [TestCase(WeaponClass.Pistol, AimPresentationMode.FirstPerson)]
        [TestCase(WeaponClass.Rifle, AimPresentationMode.SoftShoulder)]
        [TestCase(WeaponClass.Shotgun, AimPresentationMode.HipFire)]
        public void WeaponDataOverridesClassForAdsPresentation(WeaponClass weaponClass,
            AimPresentationMode presentation)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.weaponClass = weaponClass;
            weapon.ResolvedAimDefinition.presentation = presentation;
            Assert.That(AimStateController.PresentationFor(weapon), Is.EqualTo(presentation));
            Object.DestroyImmediate(weapon);
        }

        [Test]
        public void FirstPersonOffsetIsAtEyesWhileSoftAdsRemainsBehindPlayer()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.weaponClass = WeaponClass.Sniper;
            var firstPerson = ThirdPersonCamera.FirstPersonOffset(weapon);
            var soft = ThirdPersonCamera.SoftShoulderOffset(false, false);

            Assert.That(firstPerson.magnitude, Is.LessThan(0.15f));
            Assert.That(firstPerson.z, Is.GreaterThan(0f));
            Assert.That(soft.z, Is.LessThan(-1f));
            Object.DestroyImmediate(weapon);
        }

        [Test]
        public void LocalAdsVisibilityRestoresPreviousRendererState()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var renderer = root.GetComponent<Renderer>();
            var visibility = root.AddComponent<LocalAdsVisibility>();

            visibility.SetHidden(true);
            Assert.That(visibility.Hidden, Is.True);
            Assert.That(renderer.enabled, Is.False);
            visibility.SetHidden(false);
            Assert.That(visibility.Hidden, Is.False);
            Assert.That(renderer.enabled, Is.True);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void HeadZoneSlowsButNeverBlocksDeliberateUpwardInput()
        {
            var atHead = AimAssistController.HeadZoneSensitivityScale(Vector2.zero,
                0.14f, 0.055f, 0.28f);
            var aboveHead = AimAssistController.HeadZoneSensitivityScale(new Vector2(0.01f, -0.03f),
                0.14f, 0.055f, 0.28f);
            var deliberateOvershoot = AimAssistController.HeadZoneSensitivityScale(new Vector2(0.01f, -0.2f),
                0.14f, 0.055f, 0.28f);

            Assert.That(atHead, Is.EqualTo(0.28f).Within(0.001f));
            Assert.That(aboveHead, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(deliberateOvershoot, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void AimAssistCanBeDisabledPerWeaponWithoutChangingClass()
        {
            var ownerObject = new GameObject("Owner");
            var targetObject = new GameObject("Target");
            var owner = ownerObject.AddComponent<BRParticipant>();
            var target = targetObject.AddComponent<BRParticipant>();
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            owner.Configure("Owner", true, 0, 1);
            target.Configure("Target", false, 1, 2);
            owner.SetPhase(ParticipantPhase.Grounded);
            target.SetPhase(ParticipantPhase.Grounded);
            weapon.weaponClass = WeaponClass.Smg;
            weapon.ResolvedAimDefinition.aimAssistMode = AimAssistMode.None;
            var controller = new AimAssistController();
            SetAimAssistTarget(controller, target);

            Assert.That(controller.UpdateTarget(owner, Vector3.zero, Vector3.forward, weapon, 0f), Is.Null);
            Assert.That(controller.SensitivityScaleByAxis(weapon, AimInputDevice.Touch), Is.EqualTo(Vector2.one));

            Object.DestroyImmediate(weapon);
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(ownerObject);
        }

        [Test]
        public void MagnifiedScopeDefaultsToSingleCameraOverlay()
        {
            var cameraObject = new GameObject("Camera");
            cameraObject.AddComponent<Camera>();
            var controller = cameraObject.AddComponent<ScopeController>();
            var scope = ScriptableObject.CreateInstance<ScopeDefinition>();
            scope.scopeType = ScopeType.Scope2X;
            scope.renderMode = ScopeRenderMode.SingleCameraOverlay;
            scope.useRenderTexture = false;

            controller.Tick(scope, 1f);

            Assert.That(controller.ScopeActive, Is.True);
            Assert.That(controller.UsingRenderTexture, Is.False);
            Assert.That(controller.ScopeTexture, Is.Null);
            Assert.That(controller.CanvasOverlayReady, Is.True);
            Assert.That(controller.ActiveOverlay, Is.Not.Null);
            Assert.That(ScopeController.LensHeightFraction(ScopeType.RedDot), Is.InRange(0.25f, 0.35f));
            Assert.That(ScopeController.LensHeightFraction(ScopeType.Scope2X), Is.InRange(0.45f, 0.55f));
            Assert.That(ScopeController.LensHeightFraction(ScopeType.Scope4X), Is.InRange(0.60f, 0.72f));
            Assert.That(ScopeController.LensHeightFraction(ScopeType.Sniper), Is.InRange(0.76f, 0.86f));
            Assert.That(ScopeController.UsesOutsideDarkening(ScopeType.Scope2X), Is.False);
            Assert.That(ScopeController.UsesOutsideDarkening(ScopeType.Scope4X), Is.False);
            Assert.That(ScopeController.UsesOutsideDarkening(ScopeType.Sniper), Is.False);
            Assert.That(ScopeController.HasOptic(ScopeType.RedDot), Is.True);
            Assert.That(ScopeController.OverlayResourcePath(ScopeType.Scope2X), Is.EqualTo("UI/ScopeOverlays/scope-2x"));
            Assert.That(ScopeController.MainCameraAdsFieldOfView(ScopeType.RedDot), Is.EqualTo(52f));
            Assert.That(ScopeController.MainCameraAdsFieldOfView(ScopeType.Scope2X), Is.EqualTo(40f));
            Assert.That(ScopeController.MainCameraAdsFieldOfView(ScopeType.Scope4X), Is.EqualTo(28f));
            Assert.That(ScopeController.MainCameraAdsFieldOfView(ScopeType.Sniper), Is.EqualTo(17f));

            Object.DestroyImmediate(scope);
            Object.DestroyImmediate(cameraObject);
        }

        [TestCase(ScopeType.RedDot)]
        [TestCase(ScopeType.Scope2X)]
        [TestCase(ScopeType.Scope4X)]
        [TestCase(ScopeType.Sniper)]
        public void ScopeCanvasPngIsAvailable(ScopeType scopeType)
        {
            var sprite = Resources.Load<Sprite>(ScopeController.OverlayResourcePath(scopeType));

            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.texture, Is.Not.Null);
        }

        [Test]
        public void DamageFeedbackStorageIsBounded()
        {
            DamageFeedbackSystem.ClearEvents();
            for (var i = 0; i < DamageFeedbackSystem.MaxDamageEvents + 5; i++)
                DamageFeedbackSystem.Report(Vector3.zero, 10f + i, i % 2 == 0);

            Assert.That(DamageFeedbackSystem.Instance.Events.Count,
                Is.EqualTo(DamageFeedbackSystem.MaxDamageEvents));
            Assert.That(DamageFeedbackSystem.BodyColor, Is.Not.EqualTo(DamageFeedbackSystem.HeadshotColor));
            Object.DestroyImmediate(DamageFeedbackSystem.Instance.gameObject);
        }

        [Test]
        public void HipFireRetentionExpandsOnlyTheRetainedTargetCone()
        {
            Assert.That(AimAssistTargeting.RetainedConeDegrees(14f, 3f), Is.EqualTo(42f));
            Assert.That(AimAssistTargeting.RetainedConeDegrees(14f, 0.2f), Is.EqualTo(14f));
        }

        [Test]
        public void StrongManualLookDoesNotReacquireTargetInSameFrame()
        {
            var ownerObject = new GameObject("Owner");
            var owner = ownerObject.AddComponent<BRParticipant>();
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.aimAssistBreakThreshold = 2f;
            var controller = new AimAssistController();

            Assert.That(controller.UpdateTarget(owner, Vector3.zero, Vector3.forward, weapon, 3f), Is.Null);
            Assert.That(controller.Locked, Is.False);

            Object.DestroyImmediate(weapon);
            Object.DestroyImmediate(ownerObject);
        }

        private static void SetAimAssistTarget(AimAssistController controller, BRParticipant target)
        {
            typeof(AimAssistController).GetField("target", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(controller, target);
        }
    }
}
