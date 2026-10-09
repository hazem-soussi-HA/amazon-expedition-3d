// Headless expedition harness for the Unity/C# port.
//
// Builds the jungle from amazon_3d.html (same analytic terrain, same river,
// same temple, same boat, same three teammates), wires up the real
// AmazonExpedition components, and drives a scripted expedition so the port's
// physics can be observed running rather than merely compiled.
//
//   mono ExpeditionHarness.exe [seconds] [seed]

using System;
using System.Collections.Generic;
using System.Reflection;
using AmazonExpedition.AI;
using AmazonExpedition.Environment;
using AmazonExpedition.Player;
using UnityEngine;

namespace AmazonExpedition.Harness
{
    public static class Program
    {
        private static GameObject playerGo;
        private static PlayerMotor motor;
        private static LocomotionDriver locomotion;
        private static FootPlacementIK footIk;
        private static ProceduralJointBending joints;
        private static FootstepSystem footsteps;
        private static FootstepSystem footstepSystem;
        private static string footstepHits = "none";
        private static int surfaceZoneHits;
        private static PlayerCameraRig cameraRig;
        private static PlayerCharacter characterRef;
        private static readonly List<TeammateAI> Team = new List<TeammateAI>();

        private static SurfaceProfile grassProfile;
        private static SurfaceProfile mudProfile;
        private static SurfaceProfile rockProfile;

        public static int Main(string[] args)
        {
            var seconds = args.Length > 0 ? float.Parse(args[0]) : 90f;
            var seed = args.Length > 1 ? int.Parse(args[1]) : 20261009;

            UnityEngine.Random.Seed(seed);
            Time.deltaTime = 1f / 60f;
            Time.fixedDeltaTime = 0.02f;
            PhysicsEngine.InverseFixedDelta = 1f / Time.fixedDeltaTime;

            Console.WriteLine("Amazon Expedition 3D - Unity/C# port, headless run");
            Console.WriteLine("seed {0}, {1:0.#}s, fixed {2:0.###}s\n", seed, seconds, Time.fixedDeltaTime);

            if (args.Length > 2 && args[2] == "probe") { Probe.Run(); return 0; }
            BuildSurfaces();
            BuildWorld();
            BuildPlayer();
            BuildTeam();

            FrameLoop.Rebuild();
            FrameLoop.WakeAll();

            Hud();
            Console.WriteLine();

            var elapsed = 0f;
            var report = 0f;
            var lastPos = motor.Position;
            var stallTimer = 0f;
            var stallCount = 0;
            var maxSpeed = 0f;
            var airFrames = 0;
            var footsteps = 0;
            var waterFrames = 0;
            var stumbleFrames = 0;
            var prevSurface = (string)null;

            locomotion.Footstep += strength =>
            {
                footsteps++;
                var surf = motor.Ground.surface;
                footstepHits = surf != null ? surf.surfaceName : "NULL SURFACE at stride raise";
            };

            while (elapsed < seconds)
            {
                ScriptPilot(elapsed, seconds);
                Input.EndFrame();

                PhysicsEngine.RaycastCount = 0;
                PhysicsEngine.SphereCastCount = 0;
                PhysicsEngine.CapsuleCastCount = 0;

                FrameLoop.TickFixed();
                Time.Advance(Time.fixedDeltaTime);
                FrameLoop.TickFrame();
                Time.Advance(Time.deltaTime);
                PlaceRigFeet(locomotion.Pose.strideTimer);
                FrameLoop.TickLate();

                elapsed += Time.deltaTime;
                report += Time.deltaTime;

                var here = motor.Position;
                if ((here - lastPos).sqrMagnitude < 1E-05f)
                {
                    stallTimer += Time.deltaTime;
                    if (stallTimer > 2f)
                    {
                        if (stallCount < 8)
                    {
                        var blockers = new List<string>();
                        var r = motor.BodyRadius;
                        var probeR = r - 0.015f;
                        var feet = motor.Position;
                        var p1 = feet + Vector3.up * (probeR + 0.01f);
                        var p2 = feet + Vector3.up * (motor.BodyHeight - probeR - 0.01f);
                        var vdir = motor.PlanarVelocity.sqrMagnitude > 1E-06f
                            ? motor.PlanarVelocity.normalized : Vector3.forward;
                        RaycastHit dbg;
                        var probeLen = Mathf.Max(0.001f, motor.PlanarVelocity.magnitude * Time.fixedDeltaTime);
                        var hitIt = Physics.CapsuleCast(p1, p2, probeR, vdir, out dbg, probeLen + 0.015f, ~0, QueryTriggerInteraction.Ignore);
                        blockers.Add(hitIt
                            ? "sweep hit d=" + dbg.distance.ToString("0.000") + " of " + probeLen.ToString("0.000")
                              + " point=" + dbg.point + " n=" + dbg.normal + " col=" + (dbg.collider != null ? dbg.collider.name : "null")
                            : "sweep clear len=" + probeLen.ToString("0.000"));
                        Console.WriteLine("  STALLED at {0} after {1:0.0}s g={2} spd={3:0.00} flags={4} near: {5}",
                            here, stallTimer, motor.Grounded, motor.Speed, motor.CollisionState,
                            string.Join(" | ", blockers.ToArray()));
                    }
                    stallCount++;
                    stallTimer = 0f;
                    }
                }
                else
                {
                    stallTimer = 0f;
                    lastPos = here;
                }

                if (motor.Speed > maxSpeed) maxSpeed = motor.Speed;
                if (!motor.Grounded) airFrames++;
                if (motor.InWater) waterFrames++;
                if (motor.StumbleShake > 0.01f) stumbleFrames++;

                var surface = motor.Ground.surface != null ? motor.Ground.surface.surfaceName : "-";
                if (surface != prevSurface)
                {
                    Console.WriteLine("  t={0,5:0.00}s  surface -> {1,-10} traction {2:0.00} drag {3:0.00}",
                        elapsed, surface,
                        motor.Ground.surface != null ? motor.Ground.surface.traction : 1f,
                        motor.Ground.surface != null ? motor.Ground.surface.drag : 0f);
                    prevSurface = surface;
                }

                if (report >= 5f)
                {
                    report = 0f;
                    Status(elapsed);
                }

            }

            Console.WriteLine();
            Console.WriteLine("--- run summary -------------------------------------");
            Console.WriteLine("peak speed        {0:0.00} m/s   (sprint cap {1:0.0})", maxSpeed, motor.SprintSpeed);
            Console.WriteLine("airborne frames   {0}", airFrames);
            Console.WriteLine("wading frames     {0}", waterFrames);
            Console.WriteLine("footsteps fired   {0}", footsteps);
            Console.WriteLine("footprints live   {0}", CountFootprints());
            Console.WriteLine("stumble frames    {0}", stumbleFrames);
            Console.WriteLine("surface named     {0}", motor.Ground.surface != null ? motor.Ground.surface.surfaceName : "none");
            Console.WriteLine("stamina           {0:0.0} / 100", motor.Stamina);
            Console.WriteLine("final position    {0}", motor.Position);
            Console.WriteLine("foot IK weights   L {0:0.00}  R {1:0.00}  pelvis {2:0.000}",
                footIk.LeftWeight, footIk.RightWeight, footIk.PelvisOffset);
            Console.WriteLine("camera yaw/pitch  {0:0.0} / {1:0.0} deg", cameraRig.Yaw, cameraRig.Pitch);
            Console.WriteLine("physics queries   ray {0}  sphere {1}  capsule {2}",
                PhysicsEngine.RaycastCount, PhysicsEngine.SphereCastCount, PhysicsEngine.CapsuleCastCount);

            Console.WriteLine("  resolver instance  {0}", TerrainSurfaceResolver.Instance != null);
            var nearTag = Physics.OverlapSphere(motor.Position + Vector3.up * 0.3f, 0.6f, ~0, QueryTriggerInteraction.Ignore);
            Console.WriteLine("  footstep receivers {0}", footstepSystem != null ? footstepSystem.GetComponentsInChildren<IFootstepReceiver>().Length : -1);
            Console.WriteLine("  locomotion pose    stride {0:0.00} grounded {1}", locomotion.Pose.strideTimer, motor.Grounded);
            Console.WriteLine("diagnostics:");
            Console.WriteLine("  motor grounded     {0}  surface {1}", motor.Grounded,
                motor.Ground.surface != null ? motor.Ground.surface.surfaceName : "none");
            Console.WriteLine("  stride timer       {0:0.00}  landImpact {1:0.00}", locomotion.Pose.strideTimer, locomotion.Pose.landImpact);
            var lf = boneTransforms[0].position;
            var rf = boneTransforms[1].position;
            RaycastHit lh, rh;
            var lStart = lf + Vector3.up * 0.6f;
            var rStart = rf + Vector3.up * 0.6f;
            var lOk = Physics.Raycast(lStart, Vector3.down, out lh, 0.6f + 1.6f, ~0, QueryTriggerInteraction.Ignore);
            var rOk = Physics.Raycast(rStart, Vector3.down, out rh, 0.6f + 1.6f, ~0, QueryTriggerInteraction.Ignore);
            Console.WriteLine("  ground under feet h={0:0.000}  L cast start {1}  R cast start {2}",
                JungleTerrain.MeshHeight(lf.x, lf.z), lStart, rStart);
            Console.WriteLine("  foot bones         L {0}  R {1}", lf, rf);
            Console.WriteLine("  foot ground cast   L {0}  R {1}", lOk, rOk);
            Console.WriteLine("  IK weights         L {0:0.00}  R {1:0.00}", footIk.LeftWeight, footIk.RightWeight);
            Console.WriteLine("  footstep surface   {0}", footstepHits);
            Status(0f);
            return 0;
        }

        private static void Hud()
        {
            Console.WriteLine("position          {0}", motor.Position);
            Console.WriteLine("ground            grounded={0} slope={1:0.0} deg surface={2}",
                motor.Grounded, motor.Ground.angle,
                motor.Ground.surface != null ? motor.Ground.surface.surfaceName : "none");
            Console.WriteLine("locomotion pose   weightShift={0} stride={1:0.00} land={2:0.00} turn={3:0.00}",
                locomotion.Pose.weightShift, locomotion.Pose.strideTimer,
                locomotion.Pose.landImpact, locomotion.Pose.turnRate);
            Console.WriteLine("team              {0}", TeamSummary());
        }

        private static void Status(float t)
        {
            var label = t > 0f ? "t=" + t.ToString("0.0") + "s" : "final";
            Console.WriteLine("  {0,-9} pos {1,26}  spd {2,5:0.00}  stam {3,5:0}  water {4:0.00}  IK L{5:0.00}/R{6:0.00}",
                label,
                motor.Position.ToString(),
                motor.Speed,
                motor.Stamina,
                motor.WaterDepth,
                footIk.LeftWeight, footIk.RightWeight);
            foreach (var ai in Team)
                Console.WriteLine("            {0,-6} {1,-8} at {2,24} spd {3:0.00} grounded {4}",
                    ai.Name, ai.State, ai.transform.position.ToString(),
                    ai.WalkSpeed > 0f ? "ok" : "-", ai.GetComponent<CharacterController>().isGrounded);
        }

        private static string TeamSummary()
        {
            var s = string.Empty;
            foreach (var ai in Team) s += ai.Name + "=" + ai.State + "  ";
            return s.TrimEnd();
        }

        private static int CountFootprints()
        {
            var n = 0;
            foreach (var r in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
                if (r.gameObject != null && r.gameObject.activeSelf && r.transform.parent == footsteps.transform)
                    n++;
            return n;
        }

        // ------------------------------------------------------------ surfaces

        private static void BuildSurfaces()
        {
            grassProfile = ScriptableObject.CreateInstance<SurfaceProfile>();
            grassProfile.surfaceName = "grass";
            grassProfile.traction = 1f;
            grassProfile.drag = 0f;
            grassProfile.stumbleThreshold = 8f;
            grassProfile.stumbleChancePerSecond = 0.05f;
            grassProfile.footstepVolume = 0.5f;
            grassProfile.footprintStrength = 0.15f;
            grassProfile.grassClippingAmount = 1f;
            grassProfile.footstepClips = new[] { new AudioClip { name = "grass_step" } };

            mudProfile = ScriptableObject.CreateInstance<SurfaceProfile>();
            mudProfile.surfaceName = "mud";
            mudProfile.traction = 0.55f;
            mudProfile.drag = 6.5f;
            mudProfile.slipperiness = 0.7f;
            mudProfile.stumbleThreshold = 4.5f;
            mudProfile.stumbleChancePerSecond = 0.8f;
            mudProfile.sinkDepth = 0.12f;
            mudProfile.footstepVolume = 0.9f;
            mudProfile.footprintStrength = 1f;
            mudProfile.footstepClips = new[] { new AudioClip { name = "mud_step" } };

            rockProfile = ScriptableObject.CreateInstance<SurfaceProfile>();
            rockProfile.surfaceName = "rock";
            rockProfile.traction = 1.15f;
            rockProfile.drag = 0f;
            rockProfile.stumbleThreshold = 6.5f;
            rockProfile.stumbleChancePerSecond = 0.35f;
            rockProfile.footprintStrength = 0f;
            rockProfile.footstepVolume = 0.7f;
            rockProfile.footstepClips = new[] { new AudioClip { name = "rock_step" } };
        }

        /// Tag ground zones so SurfaceResolver.FindTag can pick a profile.
        ///
        /// The collider is solid, not a trigger: FindTag searches with
        /// QueryTriggerInteraction.Ignore, so a trigger would be invisible to
        /// it. To keep the zone out of the way of movement it sits on its own
        /// layer, which the motor's collisionMask excludes. This is the
        /// arrangement a real scene needs for surface tagging to work at all.
        private const int SurfaceLayer = 8;
        private const int CollisionMaskAll = ~(1 << SurfaceLayer);

        private static LayerMask Mask { get { return new LayerMask(CollisionMaskAll); } }

        private static GameObject SurfaceZone(string name, float x, float z, float radius, SurfaceProfile profile)
        {
            surfaceZoneHits++;
            var go = new GameObject(name);
            go.layer = SurfaceLayer;
            var ground = JungleTerrain.MeshHeight(x, z);
            go.transform.position = new Vector3(x, ground, z);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(radius * 2f, 2.4f, radius * 2f);
            box.center = new Vector3(0f, -1.2f, 0f);
            go.AddComponent<SurfaceTag>().profile = profile;
            PhysicsEngine.AddShape(new PhysShape
            {
                Kind = ShapeKind.Box,
                Owner = box,
                Layer = SurfaceLayer,
                Center = new Vector3(x, ground - 1.2f, z),
                Size = new Vector3(radius * 2f, 2.4f, radius * 2f)
            });
            return go;
        }

        // ------------------------------------------------------------ world

        private static void BuildWorld()
        {
            // Riverbank mud bands, then the temple approach in rock.
            for (var i = -4; i <= 4; i++)
                SurfaceZone("mud-" + i, i * 18f, JungleTerrain.RIVER_Z1 - 3.5f, 12f, mudProfile);
            for (var i = -4; i <= 4; i++)
                SurfaceZone("mudb-" + i, i * 18f, JungleTerrain.RIVER_Z2 + 3.5f, 12f, mudProfile);
            SurfaceZone("temple-rock", 0f, -120f, 22f, rockProfile);
            SurfaceZone("grass-0", -60f, 120f, 30f, grassProfile);

            // Obstacles: trees and rocks as box/sphere colliders.
            var trees = 0;
            for (var guard = 0; trees < 240 && guard < 4000; guard++)
            {
                var x = UnityEngine.Random.Range(-(JungleTerrain.WORLD - 8f), JungleTerrain.WORLD - 8f);
                var z = UnityEngine.Random.Range(-(JungleTerrain.WORLD - 8f), JungleTerrain.WORLD - 8f);
                if (JungleTerrain.InRiver(z) || JungleTerrain.NearRuins(x, z) || JungleTerrain.NearStart(x, z)) continue;
                var s = 0.8f + UnityEngine.Random.value * 1.6f;
                AddTree(x, z, 0.8f * s);
                trees++;
            }

            for (var i = 0; i < 70; i++)
            {
                var x = UnityEngine.Random.Range(-(JungleTerrain.WORLD - 6f), JungleTerrain.WORLD - 6f);
                var z = UnityEngine.Random.Range(-(JungleTerrain.WORLD - 6f), JungleTerrain.WORLD - 6f);
                if (JungleTerrain.InRiver(z)) continue;
                var s = 0.6f + UnityEngine.Random.value * 1.8f;
                AddRock(x, z, s);
            }

            // Ruins: eight pillars ringing the temple, same layout as the JS.
            for (var i = 0; i < 8; i++)
            {
                var a = i / 8f * Mathf.PI * 2f;
                var x = Mathf.Cos(a) * 22f;
                var z = Mathf.Sin(a) * 22f - 120f;
                var go = new GameObject("pillar-" + i);
                var h = JungleTerrain.MeshHeight(x, z);
                go.transform.position = new Vector3(x, h, z);
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(2.4f, 9f, 2.4f);
                box.center = new Vector3(0f, 4.5f, 0f);
                PhysicsEngine.AddShape(new PhysShape
                {
                    Kind = ShapeKind.Box,
                    Owner = box,
                    Center = new Vector3(x, h + 4.5f, z),
                    Size = new Vector3(2.4f, 9f, 2.4f)
                });
            }

            // Water: a WaterVolume trigger on the river.
            var water = new GameObject("River");
            water.transform.position = new Vector3(0f, JungleTerrain.WATER_LEVEL, (JungleTerrain.RIVER_Z1 + JungleTerrain.RIVER_Z2) * 0.5f);
            var waterCol = water.AddComponent<BoxCollider>();
            waterCol.size = new Vector3(500f, 6f, JungleTerrain.RIVER_Z2 - JungleTerrain.RIVER_Z1);
            var volume = water.AddComponent<WaterVolume>();
            Set(volume, "autoHeight", false);
            Set(volume, "surfaceHeightWorld", JungleTerrain.WATER_LEVEL);
            volume.currentStrength = 0.3f;
            volume.currentDirection = new Vector3(0f, 0f, -1f);

            // A mud slip patch to exercise SlipSurface.
            var slip = new GameObject("mudslide");
            var sz = 42f;
            var sx = 4f;
            var groundY = JungleTerrain.MeshHeight(sx, sz);
            slip.transform.position = new Vector3(sx, groundY, sz);
            var slipCol = slip.AddComponent<BoxCollider>();
            slipCol.size = new Vector3(14f, 3f, 14f);
            slipCol.center = new Vector3(0f, -1.4f, 0f);
            slipCol.isTrigger = true;
            var slipSurface = slip.AddComponent<SlipSurface>();
            slipSurface.slideForce = 4.5f;
            slipSurface.minSlipSpeed = 3.5f;
            slip.AddComponent<SurfaceTag>().profile = mudProfile;
            PhysicsEngine.AddShape(new PhysShape
            {
                Kind = ShapeKind.Box,
                Owner = slipCol,
                IsTrigger = true,
                Center = new Vector3(sx, groundY - 1.4f, sz),
                Size = new Vector3(14f, 3f, 14f)
            });
        }

        private static void AddTree(float x, float z, float radius)
        {
            var go = new GameObject("tree");
            go.transform.position = new Vector3(x, JungleTerrain.MeshHeight(x, z), z);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(radius * 2f, 9f, radius * 2f);
            box.center = new Vector3(0f, 4.5f, 0f);
            PhysicsEngine.AddShape(new PhysShape
            {
                Kind = ShapeKind.Box,
                Owner = box,
                Center = new Vector3(x, JungleTerrain.MeshHeight(x, z) + 4.5f, z),
                Size = new Vector3(radius * 2f, 9f, radius * 2f)
            });
        }

        private static void AddRock(float x, float z, float s)
        {
            if (s <= 1.4f) return; // only big rocks collide, matching the JS
            var go = new GameObject("rock");
            var h = JungleTerrain.MeshHeight(x, z) + 0.2f * s;
            go.transform.position = new Vector3(x, h, z);
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.9f * s;
            PhysicsEngine.AddShape(new PhysShape
            {
                Kind = ShapeKind.Sphere,
                Owner = col,
                Center = new Vector3(x, h, z),
                Radius = 0.9f * s
            });
        }

        // ------------------------------------------------------------ player

        private static void BuildPlayer()
        {
            playerGo = new GameObject("Player");
            playerGo.tag = "Player";
            playerGo.transform.position = new Vector3(0f, JungleTerrain.MeshHeight(0f, 180f), 180f);

            playerGo.AddComponent<Rigidbody>();
            var capsule = playerGo.AddComponent<CapsuleCollider>();
            motor = playerGo.AddComponent<PlayerMotor>();

            // Model child carrying the rig, so GetComponentInParent/InChildren
            // resolve the way the README's scene setup describes.
            var model = new GameObject("Model");
            model.transform.SetParent(playerGo.transform, true);
            model.transform.position = playerGo.transform.position;

            var hips = Bone(model, "Hips", HumanBodyBones.Hips, 0f, 0.95f, 0f);
            Bone(model, "Spine", HumanBodyBones.Spine, 0f, 1.25f, 0f);
            var leftFoot = Bone(model, "LeftFoot", HumanBodyBones.LeftFoot, 0.11f, 0.06f, 0f);
            var rightFoot = Bone(model, "RightFoot", HumanBodyBones.RightFoot, -0.11f, 0.06f, 0f);
            boneTransforms[0] = leftFoot.transform;
            boneTransforms[1] = rightFoot.transform;
            var leftKnee = Bone(model, "LeftLowerLeg", HumanBodyBones.LeftLowerLeg, 0.12f, 0.5f, 0f);
            var rightKnee = Bone(model, "RightLowerLeg", HumanBodyBones.RightLowerLeg, -0.12f, 0.5f, 0f);
            var leftElbow = Bone(model, "LeftLowerArm", HumanBodyBones.LeftLowerArm, 0.28f, 1.15f, 0f);
            var rightElbow = Bone(model, "RightLowerArm", HumanBodyBones.RightLowerArm, -0.28f, 1.15f, 0f);

            var animator = model.AddComponent<Animator>();
            animator.Bones[HumanBodyBones.Hips] = hips.transform;
            animator.Bones[HumanBodyBones.Spine] = model.transform.Find("Spine");
            animator.Bones[HumanBodyBones.LeftFoot] = leftFoot.transform;
            animator.Bones[HumanBodyBones.RightFoot] = rightFoot.transform;
            animator.Bones[HumanBodyBones.LeftLowerLeg] = leftKnee.transform;
            animator.Bones[HumanBodyBones.RightLowerLeg] = rightKnee.transform;
            animator.Bones[HumanBodyBones.LeftLowerArm] = leftElbow.transform;
            animator.Bones[HumanBodyBones.RightLowerArm] = rightElbow.transform;

            locomotion = model.AddComponent<LocomotionDriver>();
            Set(locomotion, "motor", motor);
            Set(locomotion, "animator", animator);
            Set(locomotion, "modelRoot", model.transform);

            footIk = model.AddComponent<FootPlacementIK>();
            Set(footIk, "animator", animator);
            Set(footIk, "leftFoot", leftFoot.transform);
            Set(footIk, "rightFoot", rightFoot.transform);
            Set(footIk, "hips", hips.transform);
            Set(footIk, "groundMask", Mask);

            joints = model.AddComponent<ProceduralJointBending>();
            Set(joints, "animator", animator);
            Set(joints, "spine", model.transform.Find("Spine"));
            Set(joints, "leftKnee", leftKnee.transform);
            Set(joints, "rightKnee", rightKnee.transform);
            Set(joints, "leftElbow", leftElbow.transform);
            Set(joints, "rightElbow", rightElbow.transform);
            Set(joints, "collisionMask", Mask);

            footsteps = model.AddComponent<FootstepSystem>();
            footstepSystem = footsteps;
            Set(footsteps, "locomotion", locomotion);
            Set(footsteps, "motor", motor);
            Set(footsteps, "leftFootBone", leftFoot.transform);
            Set(footsteps, "rightFootBone", rightFoot.transform);

            model.AddComponent<AudioSource>();
            model.AddComponent<ParticleSystem>().name = "FootfallDust";
            Set(footsteps, "footfallDust", model.GetComponent<ParticleSystem>());
            Set(footsteps, "footfallGrass", model.AddComponent<ParticleSystem>());

            model.AddComponent<GrassContact>();
            Set(motor, "defaultSurface", grassProfile);
            Set(motor, "collisionMask", Mask);

            // PlayerCharacter lives on the player root: its Awake resolves PlayerMotor
            // with GetComponent, which does not search children.
            var character = playerGo.AddComponent<PlayerCharacter>();
            characterRef = character;
            Set(character, "motor", motor);
            Set(character, "locomotion", locomotion);
            Set(character, "footIK", footIk);
            Set(character, "jointBending", joints);
            Set(character, "footsteps", footsteps);
            Set(joints, "collisionMask", Mask);

            // Camera rig as a child of the player, per the scene-setup notes.
            var camRoot = new GameObject("CameraRig");
            camRoot.transform.SetParent(playerGo.transform, true);
            var yaw = new GameObject("YawRoot");
            yaw.transform.SetParent(camRoot.transform, true);
            var pitch = new GameObject("PitchRoot");
            pitch.transform.SetParent(yaw.transform, true);
            var cam = pitch.AddComponent<Camera>();
            Camera.main = cam;

            cameraRig = camRoot.AddComponent<PlayerCameraRig>();
            Set(cameraRig, "motor", motor);
            Set(cameraRig, "head", pitch.transform);
            Set(cameraRig, "yawRoot", yaw.transform);
            Set(cameraRig, "pitchRoot", pitch.transform);
            Set(cameraRig, "cameraTarget", cam);
            Set(cameraRig, "collisionMask", Mask);
            Set(character, "cameraRig", cameraRig);

            // Legacy input axes are driven by the pilot script through the
            // Input stub, so the real PlayerInputSource stays in the loop.
            var inputSource = playerGo.AddComponent<PlayerInputSource>();
            Set(character, "inputSource", inputSource);

            // A TerrainSurfaceResolver makes SurfaceResolver look for SurfaceTag
            // components in the world, which is how the mud banks and temple
            // rock actually get chosen. Without one every step reads as the
            // motor's fallback profile.
            var resolverGo = new GameObject("TerrainSurfaceResolver");
            var resolver = resolverGo.AddComponent<TerrainSurfaceResolver>();
            Set(resolver, "fallback", grassProfile);

            new GameObject("GrassInteractionManager").AddComponent<GrassInteractionManager>();
        }

        private static GameObject Bone(GameObject parent, string name, HumanBodyBones bone, float x, float y, float z)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, true);
            go.transform.localPosition = new Vector3(x, y, z);
            return go;
        }

        // ------------------------------------------------------------ team

        private static void BuildTeam()
        {
            var names = new[] { "MARCO", "ELENA", "KAI" };
            var anchors = new[]
            {
                new Vector3(-10f, 0f, 168f),
                new Vector3(26f, 0f, -98f),
                new Vector3(-34f, 0f, -62f)
            };
            var radii = new[] { 24f, 32f, 38f };

            for (var i = 0; i < names.Length; i++)
            {
                var go = new GameObject(names[i]);
                var x = anchors[i].x;
                var z = anchors[i].z;
                go.transform.position = new Vector3(x, JungleTerrain.MeshHeight(x, z), z);

                var cc = go.AddComponent<CharacterController>();
                cc.radius = 0.3f;
                cc.height = 1.8f;
                cc.center = 0.9f;

                var ai = go.AddComponent<TeammateAI>();
                ai.displayName = names[i];
                Set(ai, "homeOffset", new Vector3(x, 0f, z));
                Set(ai, "homeRadius", radii[i]);
                Set(ai, "playerRoot", playerGo.transform);
            Set(ai, "groundMask", Mask);
            Set(ai, "obstacleMask", Mask);
                Team.Add(ai);
            }
        }

        // ------------------------------------------------------------ pilot

        /// Scripted expedition: camp -> river crossing -> temple -> back to the
        /// boat. Input goes through the same PlayerInputSource surface a human
        /// would use, so PlayerCharacter and PlayerMotor see normal input.

        /// The rig's foot bones. LocomotionDriver and FootPlacementIK resolve feet via
        /// Animator.GetBoneTransform, which is driven by locomotion speed, so the
        /// bones must sit in world space at hip height for the ground casts to
        /// find anything.
        private static void PlaceRigFeet(float strideTimer)
        {
            var baseY = motor.Position.y;

            var phase = strideTimer * 6.28318f;
            for (var side = 0; side < 2; side++)
            {
                var sign = side == 0 ? 1f : -1f;
                var swing = Mathf.Sin(phase + (side == 0 ? 0f : Mathf.PI)) * 0.16f;
                var x = sign * 0.11f;
                var z = Mathf.Cos(phase + (side == 0 ? 0f : Mathf.PI)) * 0.18f;
                var bx = motor.Position.x + x;
                var bz = motor.Position.z + z;
                var t = boneTransforms[side];
                t.position = new Vector3(bx, baseY + 0.06f + swing, bz);
            }
        }

        private static readonly Transform[] boneTransforms = new Transform[2];

        /// Straight-line steering with two whisker probes, the same avoidance idea as
        /// TeammateAI.AvoidObstacles. Keeps the pilot walking around the temple
        /// pillars and tree trunks instead of grinding into them.
        private static Vector3 Steer(Vector3 desired)
        {
            var origin = motor.Position + Vector3.up * 0.6f;
            RaycastHit ahead;
            if (!Physics.Raycast(origin, desired, 2.6f, CollisionMaskAll, QueryTriggerInteraction.Ignore, out ahead))
            {
                sideStep = Vector3.zero;
                return desired;
            }

            // Slide along the obstacle: project the goal direction onto the
            // surface plane, which walks the pilot around a pillar or trunk
            // instead of grinding into it.
            var tangent = Vector3.ProjectOnPlane(desired, ahead.normal);
            if (tangent.sqrMagnitude > 1E-04f) return tangent.normalized;

            // Head-on: commit to one side and keep going.
            var left = Quaternion.Euler(0f, 55f, 0f) * desired;
            var right = Quaternion.Euler(0f, -55f, 0f) * desired;
            if (sideStep == Vector3.zero)
            {
                sideStep = Physics.Raycast(origin, left, 2.6f, CollisionMaskAll, QueryTriggerInteraction.Ignore) ? right : left;
            }
            return sideStep;
        }

        private static Vector3 sideStep;

        private static void ScriptPilot(float t, float total)
        {
            var pos = motor.Position;
            Vector3 goal;
            bool sprint;
            bool wade;

            if (t < total * 0.22f) { goal = new Vector3(4f, 0f, 42f); sprint = true; wade = false; }
            else if (t < total * 0.45f) { goal = new Vector3(4f, 0f, -10f); sprint = true; wade = true; }
            else if (t < total * 0.62f) { goal = new Vector3(4f, 0f, -60f); sprint = true; wade = false; }
            else if (t < total * 0.80f) { goal = new Vector3(0f, 0f, -116f); sprint = false; wade = false; }
            else { goal = new Vector3(6f, 0f, 187f); sprint = true; wade = false; }

            var to = goal - pos;
            to.y = 0f;
            var dist = to.magnitude;

            if (dist > 0.5f)
            {
                var dir = Steer(to / dist);

                // Face the heading by writing the rig's yaw, the same value the
                // mouse would drive through SetLookDelta.
                var heading = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                Set(cameraRig, "yaw", (float)Mathf.DeltaAngle(Get<float>(cameraRig, "yaw"), heading));
                Set(cameraRig, "pitch", 0f);

                var forward = cameraRig.transform.forward;
                var right = cameraRig.transform.right;
                var f = Vector3.Dot(dir, forward);
                var r = Vector3.Dot(dir, right);
                Input.SetAxis("Horizontal", Mathf.Clamp(r, -1f, 1f));
                Input.SetAxis("Vertical", Mathf.Clamp(f, -1f, 1f));
            }
            else
            {
                Input.SetAxis("Horizontal", 0f);
                Input.SetAxis("Vertical", 0f);
            }

            Input.SetKey(KeyCode.LeftShift, sprint && dist > 6f);
            if (wade) Input.SetKey(KeyCode.Space, dist > 2f && UnityEngine.Random.value < 0.02f);
        }

        // ------------------------------------------------------------ reflection helpers

        private static void Set(object target, string field, object value)
        {
            var t = target.GetType();
            while (t != null)
            {
                var f = t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f != null) { f.SetValue(target, value); return; }
                t = t.BaseType;
            }
            throw new InvalidOperationException("no field " + field + " on " + target.GetType().Name);
        }

        private static T Get<T>(object target, string field)
        {
            var t = target.GetType();
            while (t != null)
            {
                var f = t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f != null) return (T)f.GetValue(target);
                t = t.BaseType;
            }
            throw new InvalidOperationException("no field " + field + " on " + target.GetType().Name);
        }
    }
}