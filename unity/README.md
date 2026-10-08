# Amazon Expedition 3D — Unity port (C# physics overhaul)

The C# version of the physics and character overhaul. This is the code that replaces
the HTML/Three.js prototype: same expedition, same three teammates, same quest —
but built on a real character controller, a real rig, real surfaces and real IK.

**Verified:** all 16 scripts compile cleanly (checked against a Unity API stub with
Mono C#). They need a Unity project + a rigged humanoid character to run — see below.

---

## 1. What this delivers against the spec

| Spec requirement | Where it lives | Status |
| --- | --- | --- |
| Rig → skinned mesh + full articulation | `Player/FootPlacementIK.cs`, `Player/ProceduralJointBending.cs` | Code done; needs a rigged model in the scene |
| Foot IK on uneven terrain | `FootPlacementIK.cs` | Done — per-foot raycast + pelvis correction |
| Motion: accel / decel / weight shift / turn dynamics | `Player/PlayerMotor.cs`, `Player/LocomotionDriver.cs` | Done |
| Procedural joint bending under stress | `ProceduralJointBending.cs` | Done — landing crouch spring, slope, water |
| Footstep physics + displaced grass + VFX per terrain | `Player/FootstepSystem.cs`, `Environment/SurfaceProfile.cs` | Done — footprints pooled, particles + audio per surface |
| NPC: no clipping, natural idle | `AI/TeammateAI.cs` | Done — state machine + obstacle avoidance |
| Soft shadows / CSM / GI | — | Engine config, see section 5 |
| Grass tessellation & micro-displacement | — | Engine config + shader, see section 5 |
| DoF, motion blur, TAA/DLSS | — | Post-processing Volume, see section 5 |
| Emergent: mud sliding, stumbling over rocks | `Environment/SlipSurface.cs`, `Environment/ImpulseZone.cs`, surface stumble fields | Done |
| Environmental storytelling: bending grass | `Environment/GrassInteractionManager.cs` + `Shaders/GrassBend.shader` | Done |

Not done yet (honest scope): a Motion Matching motion database (needs 30–60 min of
motion capture and the Motion Matching package), and the URP/URP renderer feature
that draws cascaded shadows. Both are configuration/asset work, not code.

---

## 2. JS → C# migration map

| Prototype (`amazon_3d.html`) | Unity port |
| --- | --- |
| `player` object + EYE / SPEED / SPRINT | `PlayerMotor` fields (walk/jog/sprint, eye height) |
| `collide()` push-out on `colliders[]` | `PlayerMotor` capsule sweep + `Physics.ComputePenetration` path |
| `bobT` head bob | `PlayerCameraRig` (footstep-synced) + `LocomotionDriver` weight shift |
| `sprint && energy` | `PlayerMotor` stamina + `DrainStamina`/`RestoreStamina` |
| `terrainH(x,z)` heightfield | Unity Terrain (or keep the analytic function in a component) |
| `inRiver()` wading drag | `Environment/WaterVolume.cs` (depth, drag, buoyancy, current) |
| `placeTree/placeRock` + colliders | Scene geometry + `ImpulseZone` on rocks |
| wildlife (`birds`, `butterflies`, `monkeys`) | Same closed-form approach in a small component, or the `TeammateAI` pattern |
| `TEAM_NAMES` / `TEAM_ANCHORS` / state machine | `AI/TeammateAI.cs` — one component per teammate |
| `sfx()` procedural audio | unchanged (Web Audio has no C# twin; keep the audio in the web build or use AudioClips) |
| `drawMinimap()` | UI Canvas + `OnGUI`; or keep minimap in the web build |
| `showcase.html` dossier | Unity scene UI / WebGL build of this scene |

---

## 3. Scene setup (15 minutes)

1. **Project:** Unity 2021.3 LTS or newer, 3D (URP or Built-in; the grass shader
   works in Built-in, and URP needs the shader converted or replaced by a Visual
   Element graph).
2. **Terrain:** import the jungle heightmap, paint layers, and assign one
   `SurfaceProfile` per layer (grass, dirt, mud, rock, water edge).
3. **Player rig:** one GameObject with `Rigidbody` + `CapsuleCollider` +
   `PlayerMotor`. Under it, a `Model` child with the humanoid `Animator`
   (feet at y=0, model facing +Z). Attach `LocomotionDriver`, `FootPlacementIK`,
   `ProceduralJointBending`, `FootstepSystem`, `GrassContact` to the model.
4. **Camera:** `PlayerCameraRig` as a child; `Camera` under its `PitchRoot`.
   Assign `Head`, `YawRoot`, `PitchRoot`.
5. **Input:** `PlayerInputSource` on the player (legacy; swap in the new Input
   System inside that one file).
6. **Teammates:** one GameObject per teammate with `CharacterController` +
   `TeammateAI`; set `displayName`, `homeOffset`, `homeRadius`. Tag the player
   `Player` so they find you.
7. **Surfaces:** create SurfaceProfiles via `Assets ▸ Create ▸ Amazon Expedition ▸
   Surface Profile`, add `TerrainSurfaceResolver` to the terrain.
8. **Water:** a `WaterVolume` trigger on the river, with a surface plane at the
   water level.

---

## 4. How the physics works (the short version)

- **Kinematic rigidbody + capsule sweep.** The motor never relies on
  `Rigidbody` collision response — it moves a capsule with `Physics.CapsuleCast`
  and resolves penetration itself. This is what removes the "blocky sliding"
  feel: slopes project the velocity onto the ground plane, steps under 0.42 m are
  stepped over, and the capsule snaps to the ground so you never leave the hill.
- **Time-based acceleration**, not velocity assignment: `MoveTowards` with
  separate accelerate / decelerate rates, scaled by the surface's `traction`.
  Walking into mud decelerates you; stepping onto rock accelerates you.
- **Turning is a rotation, not a slide**: the model faces the movement direction
  with `Quaternion.Slerp` at a configurable rate, so strafing looks like
  turning, not ice-skating. The camera is independent of the body.
- **Forces** (mud slide, stumble, current) are a decaying impulse list, so they
  can add up and fade instead of fighting each other.

---

## 5. What is engine configuration, not C#

These are the spec items that belong in Unity settings — put them here so the
dossier stays truthful:

- **Soft / cascaded shadows:** URP renderer asset → Shadows → Cascades: `4`,
  soft shadows on, depth bias tuned to remove acne on the canopy.
- **Global illumination:** Lighting window → Lightmap (Mixed or Baked) +
  Reflection Probes; adaptive probing for the jungle canopy.
- **Grass detail:** Terrain detail layer with `Detail Density`, plus the
  `Shaders/GrassBend.shader` billboard/cluster shader if you want per-blade bend.
- **Post-processing:** a Volume with `Depth of Field` (focus theDistance 4–25 m),
  `Motion Blur` (camera-based, shutter 0.03), `Panini Projection` optional, and
  **TAA** in the renderer asset (or DLSS if the project has the NVIDIA package).
- **Tessellation:** terrain shader with `tessellation:Phong` on the ground,
  displacement from a macro-normal texture; or keep the mesh and use detail
  meshes. Already-optimised route: detail meshes + the micro-displacement in the
  spec is a shader-graph job.

---

## 6. Performance notes

- The `PlayerMotor` does a fixed number of sphere/capsule casts per step: 1 ground
  probe + 1 body sweep (+1 for the step check). That is the conscious cost of
  correctness — no per-frame ray fan.
- `FootstepSystem` pools footprints (64 quads), so footprint memory is constant.
- `GrassInteractionManager` uses one `Vector4[]` upload for up to 8 bend points,
  read by a single global uniform in the grass shader. Per-instance bending needs
  a GPU instancing buffer — documented, not faked.
- Teammates are `CharacterController`-based with 3 avoidance rays each, which is
  far cheaper than a NavMesh for a 3-agent scene.

---

## 7. Known gaps

1. Motion matching is *simulated* (blend tree driven by measured accel/turn),
   not a real motion database. Swapping in the Motion Matching package is a
   follow-up task and needs capture data.
2. Subsurface scattering on the boots needs a `Material` with a subsurface
   profile (URP) — assign it in the scene, not in code.
3. The grass shader is Built-in RP. Port it to URP with a Shader Graph equivalent
   (same `_GrassBendArray` uniform).
