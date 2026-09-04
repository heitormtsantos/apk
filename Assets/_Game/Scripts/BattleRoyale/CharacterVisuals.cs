using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace BattleRoyale
{
    public sealed class CharacterVisualAnimator : MonoBehaviour
    {
        private const int PortCount = 12;
        private BRCharacterMotor motor;
        private Animator animator;
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimationClipPlayable walkPlayable;
        private AnimationClipPlayable runPlayable;
        private AnimationClipPlayable armedWalkPlayable;
        private AnimationClipPlayable runGunPlayable;
        private AnimationClipPlayable reloadPlayable;
        private AnimationClipPlayable hitPlayable;
        private InventorySystem inventory;
        private WeaponSystem weapon;
        private BRParticipant participant;
        private CharacterVisualProfile profile;
        private Transform visualRoot;
        private SkinnedMeshRenderer[] bodyRenderers;
        private float hitReactionUntil;
        private int activePort = -1;
        private bool ready;
        private bool forceArmedPresentation;

        public bool Ready => ready;
        public string ActiveClips { get; private set; } = "none";
        public string ProfileId => profile?.Id ?? "none";
        public CharacterAnimationState CurrentState { get; private set; } = CharacterAnimationState.Idle;
        public Transform VisualRoot => visualRoot;

        public void Configure(GameObject model) => Configure(model, CharacterVisualCatalog.Lightweight, false);

        public void Configure(GameObject model, CharacterVisualProfile visualProfile) =>
            Configure(model, visualProfile, false);

        public void Configure(GameObject model, CharacterVisualProfile visualProfile, bool forceArmed)
        {
            profile = visualProfile ?? CharacterVisualCatalog.Lightweight;
            forceArmedPresentation = forceArmed;
            visualRoot = model != null ? model.transform : null;
            bodyRenderers = model != null ? model.GetComponentsInChildren<SkinnedMeshRenderer>(true) : null;
            animator = model != null ? model.GetComponentInChildren<Animator>() : null;
            if (animator != null)
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.Normal;
            }
            var owner = GetComponent<BRParticipant>();
            if (model != null && (owner == null || owner.IsPlayer))
            {
                var wardrobe = GetComponent<CharacterClothingManager>()
                    ?? gameObject.AddComponent<CharacterClothingManager>();
                wardrobe.ConfigureRuntime(model.transform, null,
                    Resources.Load<ClothingDatabase>("Wardrobe/ClothingDatabase"));
            }
        }

        private void Start()
        {
            motor = GetComponent<BRCharacterMotor>();
            inventory = GetComponent<InventorySystem>();
            weapon = GetComponent<WeaponSystem>();
            participant = GetComponent<BRParticipant>();
            if (animator == null) return;
            animator.applyRootMotion = false;
            profile = CharacterVisualCatalog.ResolveAvailable(profile);
            var clips = profile.LoadClips();
            var fallbackClips = profile.Fallback != null ? profile.Fallback.LoadClips() : clips;
            var idle = FindClip(clips, "Idle", "Idle_Shoot");
            var walk = FindClip(clips, "Standard Walk", "Walk") ?? idle;
            var run = FindClip(clips, "Running", "Run", "Run_Gun");
            var jump = FindClip(clips, "Jumping", "Jump", "Jump_Idle");
            var idleGun = FindClip(clips, "Rifle Idle", "Idle_Shoot") ?? idle;
            var walkGun = FindClip(clips, "Rifle Walk", "Walk Forward") ?? walk;
            var runGun = FindClip(clips, "Rifle Run", "Run_Gun") ?? run;
            var aim = FindClip(clips, "Rifle Aiming Idle") ?? idleGun;
            var crouchIdle = FindClip(clips, "Crouch Idle", "Duck") ?? idleGun;
            var crouchMove = FindClip(clips, "Crouched Run", "Walk Crouching Forward") ?? crouchIdle;
            var reload = FindClip(clips, "Reloading", "Reload") ?? idleGun;
            var hitReact = FindClip(clips, "HitReact") ?? FindClip(fallbackClips, "HitReact") ?? idleGun;
            if (idle == null || run == null || jump == null) return;
            ActiveClips = $"{idle.name},{walk.name},{run.name},{jump.name},{idleGun.name},{walkGun.name}," +
                $"{runGun.name},{aim.name},{crouchIdle.name},{crouchMove.name},{reload.name},{hitReact.name}";

            graph = PlayableGraph.Create($"{name} Locomotion");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(graph, PortCount);
            ConnectClip(idle, 0, 1f);
            walkPlayable = ConnectClip(walk, 1, 1f);
            runPlayable = ConnectClip(run, 2, 1.05f);
            ConnectClip(jump, 3, 1f);
            ConnectClip(idleGun, 4, 1f);
            armedWalkPlayable = ConnectClip(walkGun, 5, 1f);
            runGunPlayable = ConnectClip(runGun, 6, 1.05f);
            ConnectClip(aim, 7, 1f);
            ConnectClip(crouchIdle, 8, 1f);
            ConnectClip(crouchMove, 9, 1f);
            reloadPlayable = ConnectClip(reload, 10, 1f);
            hitPlayable = ConnectClip(hitReact, 11, 1f);
            var output = AnimationPlayableOutput.Create(graph, "Character", animator);
            output.SetSourcePlayable(mixer);
            mixer.SetInputWeight(0, 1f);
            graph.Play();
            graph.Evaluate(0f);
            if (visualRoot != null)
            {
                BRAssetVisuals.NormalizeHeight(visualRoot.gameObject,
                    profile.TargetHeight);
                GroundVisualToMotorRoot();
            }
            ready = true;
        }

        private void LateUpdate()
        {
            if (!ready) return;
            var speed = motor != null ? new Vector2(motor.Velocity.x, motor.Velocity.z).magnitude : 0f;
            var grounded = motor == null || motor.IsGrounded;
            var armed = forceArmedPresentation || inventory != null && inventory.ActiveWeapon != null;
            var aiming = participant != null && participant.IsPlayer && Camera.main != null
                && Camera.main.GetComponent<ThirdPersonCamera>() is { Aiming: true };
            CurrentState = EvaluatePresentationState(speed, grounded, motor != null && motor.Crouching,
                armed, aiming, weapon != null && weapon.Reloading);
            var desiredPort = CurrentState switch
            {
                CharacterAnimationState.Walk when armed => 5,
                CharacterAnimationState.Run when armed => 6,
                CharacterAnimationState.Idle when armed => 4,
                CharacterAnimationState.Walk => 1,
                CharacterAnimationState.Run => 2,
                CharacterAnimationState.Jump => 3,
                CharacterAnimationState.Crouch when speed >= 0.15f => 9,
                CharacterAnimationState.Crouch => 8,
                CharacterAnimationState.Aim => 7,
                CharacterAnimationState.Reload => 10,
                _ => 0
            };
            if (Time.time < hitReactionUntil)
            {
                desiredPort = 11;
                CurrentState = CharacterAnimationState.HitReaction;
            }
            if (desiredPort != activePort && desiredPort == 10 && reloadPlayable.IsValid()) reloadPlayable.SetTime(0d);
            activePort = desiredPort;
            var blendStep = 10f * Time.deltaTime;
            for (var port = 0; port < PortCount; port++)
                mixer.SetInputWeight(port, Mathf.MoveTowards(mixer.GetInputWeight(port), port == desiredPort ? 1f : 0f, blendStep));
            if (walkPlayable.IsValid()) walkPlayable.SetSpeed(Mathf.Lerp(0.82f, 1.2f, Mathf.InverseLerp(0.5f, 3.2f, speed)));
            if (runPlayable.IsValid()) runPlayable.SetSpeed(Mathf.Lerp(0.78f, 1.28f, Mathf.InverseLerp(1f, 7f, speed)));
            if (armedWalkPlayable.IsValid()) armedWalkPlayable.SetSpeed(Mathf.Lerp(0.82f, 1.2f, Mathf.InverseLerp(0.5f, 3.2f, speed)));
            if (runGunPlayable.IsValid()) runGunPlayable.SetSpeed(Mathf.Lerp(0.78f, 1.28f, Mathf.InverseLerp(1f, 7f, speed)));
            Repeat(walkPlayable);
            Repeat(runPlayable);
            Repeat(armedWalkPlayable);
            Repeat(runGunPlayable);
            GroundVisualToMotorRoot();
        }

        private void GroundVisualToMotorRoot()
        {
            if (visualRoot == null || bodyRenderers == null || bodyRenderers.Length == 0) return;
            var minimumY = float.PositiveInfinity;
            foreach (var renderer in bodyRenderers)
                if (renderer != null && renderer.enabled) minimumY = Mathf.Min(minimumY, renderer.bounds.min.y);
            if (float.IsPositiveInfinity(minimumY)) return;
            var correction = GroundCorrection(transform.position.y, minimumY);
            if (Mathf.Abs(correction) > 0.001f) visualRoot.position += Vector3.up * correction;
        }

        public static float GroundCorrection(float motorRootY, float renderedMinimumY) =>
            motorRootY + 0.015f - renderedMinimumY;

        public void PlayHitReaction()
        {
            if (!ready || !hitPlayable.IsValid()) return;
            hitPlayable.SetTime(0d);
            hitReactionUntil = Time.time + 0.28f;
        }

        public static CharacterAnimationState EvaluateState(float planarSpeed, bool grounded, bool crouching = false)
        {
            if (!grounded) return CharacterAnimationState.Jump;
            if (crouching) return CharacterAnimationState.Crouch;
            return planarSpeed >= 0.15f ? CharacterAnimationState.Run : CharacterAnimationState.Idle;
        }

        public static CharacterAnimationState EvaluatePresentationState(float planarSpeed, bool grounded,
            bool crouching, bool armed, bool aiming, bool reloading)
        {
            if (reloading && grounded) return CharacterAnimationState.Reload;
            if (!grounded) return CharacterAnimationState.Jump;
            if (crouching) return CharacterAnimationState.Crouch;
            if (aiming && armed && planarSpeed < 0.15f) return CharacterAnimationState.Aim;
            if (planarSpeed >= 3.2f) return CharacterAnimationState.Run;
            if (planarSpeed >= 0.15f) return CharacterAnimationState.Walk;
            return CharacterAnimationState.Idle;
        }

        private AnimationClipPlayable ConnectClip(AnimationClip clip, int port, float speed)
        {
            var playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(true);
            playable.SetSpeed(speed);
            graph.Connect(playable, 0, mixer, port);
            return playable;
        }

        private static void Repeat(AnimationClipPlayable playable)
        {
            if (!playable.IsValid()) return;
            var duration = playable.GetDuration();
            if (duration <= 0.001d) return;
            var time = playable.GetTime();
            if (time >= duration) playable.SetTime(time % duration);
        }

        private static AnimationClip FindClip(AnimationClip[] clips, params string[] names)
        {
            foreach (var requested in names)
            foreach (var clip in clips)
                if (clip != null && clip.name.Equals(requested, System.StringComparison.OrdinalIgnoreCase)) return clip;
            foreach (var requested in names)
            foreach (var clip in clips)
                if (clip != null && clip.name.Contains(requested)) return clip;
            return null;
        }

        private void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }

    public enum CharacterAnimationState
    {
        Idle,
        Walk,
        Run,
        Jump,
        Crouch,
        Aim,
        Reload,
        HitReaction
    }

    public sealed class ParticipantWeaponVisual : MonoBehaviour
    {
        private InventorySystem inventory;
        private Transform armAnchor;
        private GameObject characterModel;
        private Renderer embeddedWeapon;
        private WeaponDefinition shownWeapon;
        private GameObject model;
        private bool importedWeaponModel;
        private Vector3 modelHipPosition;
        private Quaternion modelHipRotation;

        public float AimPoseProgress { get; private set; }

        public Vector3 MuzzlePosition
        {
            get
            {
                var fallback = CharacterPresentationProfile.FallbackMuzzle(transform);
                if (armAnchor == null) return fallback;
                if (embeddedWeapon == null && model == null) return fallback;

                var candidate = embeddedWeapon != null ? embeddedWeapon.bounds.center : model.transform.position;
                candidate.y = transform.position.y + CharacterPresentationProfile.MuzzleHeight;
                if (Vector3.ProjectOnPlane(candidate - transform.position, Vector3.up).sqrMagnitude > 1.8f)
                    return fallback;
                return candidate + transform.forward * CharacterPresentationProfile.MuzzleForward;
            }
        }
        public string MountName => embeddedWeapon != null ? $"embedded:{embeddedWeapon.name}" : armAnchor != null ? armAnchor.name : "root-fallback";

        public bool IsWeaponRenderer(Renderer renderer)
        {
            if (renderer == null) return false;
            if (renderer == embeddedWeapon) return true;
            return model != null && renderer.transform.IsChildOf(model.transform);
        }

        public void Configure(GameObject characterModel)
        {
            this.characterModel = characterModel;
            armAnchor = FindBone(characterModel != null ? characterModel.transform : null, "Hand.R", "RightHand", "Index1.R", "LowerArm.R");
        }

        private void Awake()
        {
            inventory = GetComponent<InventorySystem>();
        }

        private void LateUpdate()
        {
            var active = inventory != null ? inventory.ActiveWeapon : null;
            if (active != shownWeapon)
            {
                shownWeapon = active;
                if (model != null) Destroy(model);
                if (embeddedWeapon != null) embeddedWeapon.enabled = false;
                embeddedWeapon = null;
                model = null;
                importedWeaponModel = false;
                if (active != null && !string.IsNullOrWhiteSpace(active.modelResource))
                {
                    embeddedWeapon = FindEmbeddedWeapon(active.modelResource);
                    if (embeddedWeapon != null) embeddedWeapon.enabled = true;
                    else CreateHeldModel(active);
                }
            }
            ApplyAimPose(active);
        }

        private void CreateHeldModel(WeaponDefinition active)
        {
            var prefab = Resources.Load<GameObject>(active.modelResource);
            if (prefab == null) return;
            importedWeaponModel = active.modelResource.StartsWith(ImportedGameplayVisuals.WeaponPrefabRoot,
                System.StringComparison.Ordinal);
            var parent = importedWeaponModel ? transform : armAnchor != null ? armAnchor : transform;
            model = Instantiate(prefab, parent, false);
            model.name = $"Equipped {active.displayName}";
            BRAssetVisuals.PrepareForUrp(model);
            model.transform.localPosition = importedWeaponModel
                ? Vector3.zero
                : armAnchor != null
                ? new Vector3(0f, 0.18f, 0.02f)
                : new Vector3(0.26f, CharacterPresentationProfile.MuzzleHeight, CharacterPresentationProfile.MuzzleForward);
            model.transform.localRotation = importedWeaponModel
                ? Quaternion.identity
                : armAnchor != null ? Quaternion.Euler(82f, 0f, 90f) : Quaternion.Euler(0f, 90f, -12f);
            model.transform.localScale = Vector3.one;
            foreach (var collider in model.GetComponentsInChildren<Collider>()) Destroy(collider);
            NormalizeToLength(model.transform, CharacterPresentationProfile.WeaponLength(active.weaponClass));
            modelHipPosition = model.transform.localPosition;
            modelHipRotation = model.transform.localRotation;
        }

        private void ApplyAimPose(WeaponDefinition active)
        {
            var participant = GetComponent<BRParticipant>();
            var cameraController = participant != null && participant.IsPlayer && Camera.main != null
                ? Camera.main.GetComponent<ThirdPersonCamera>() : null;
            AimPoseProgress = cameraController != null ? cameraController.AdsProgress : 0f;
            if (model == null || active == null) return;
            if (importedWeaponModel)
            {
                var aimForward = cameraController != null && AimPoseProgress > 0.05f
                    ? Camera.main.transform.forward
                    : transform.forward;
                var mountPosition = armAnchor != null
                    ? armAnchor.position
                    : transform.position + Vector3.up * CharacterPresentationProfile.MuzzleHeight;
                model.transform.position = mountPosition + transform.forward * 0.10f + transform.up * 0.025f;
                model.transform.rotation = Quaternion.LookRotation(aimForward.normalized, transform.up);
                return;
            }
            var scope = active.scope;
            var positionOffset = scope != null ? scope.weaponPositionOffset : Vector3.zero;
            var rotationOffset = scope != null ? scope.weaponRotationOffset : Vector3.zero;
            var adsPosition = modelHipPosition + positionOffset + new Vector3(0f, 0.015f, 0.035f);
            var adsRotation = modelHipRotation * Quaternion.Euler(rotationOffset);
            model.transform.localPosition = Vector3.Lerp(modelHipPosition, adsPosition, AimPoseProgress);
            model.transform.localRotation = Quaternion.Slerp(modelHipRotation, adsRotation, AimPoseProgress);
        }

        private static Transform FindBone(Transform root, params string[] names)
        {
            if (root == null) return null;
            foreach (var candidate in names)
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name.Equals(candidate, System.StringComparison.OrdinalIgnoreCase) || child.name.Contains(candidate)) return child;
            return null;
        }

        private Renderer FindEmbeddedWeapon(string resourcePath)
        {
            if (characterModel == null) return null;
            var separator = resourcePath.LastIndexOf('/');
            var modelName = separator >= 0 ? resourcePath[(separator + 1)..] : resourcePath;
            foreach (var renderer in characterModel.GetComponentsInChildren<Renderer>(true))
                if (renderer.name.Equals(modelName, System.StringComparison.OrdinalIgnoreCase)) return renderer;
            return null;
        }

        private static void NormalizeToLength(Transform visual, float targetLength)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            var largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (largest > 0.001f) visual.localScale *= targetLength / largest;
        }
    }
}
