# Amazon Expedition 3D

**A first-person jungle adventure that runs entirely in your browser.** Explore a living Amazon rainforest, collect ten glowing gems, find the Temple of the Jaguar, and carry the Golden Idol back to your expedition boat.

No install. No build step. No assets — every tree, river, bird, idol and sound is generated at runtime from code.

[![license](https://img.shields.io/badge/license-MIT-2ecc71)](LICENSE) ![genre](https://img.shields.io/badge/genre-adventure-2ecc71) ![engine](https://img.shields.io/badge/engine-Three.js%20r128-35f0ff) ![deps](https://img.shields.io/badge/deps-1-ffd54a) ![build](https://img.shields.io/badge/build-none-ffd54a) ![lines](https://img.shields.io/badge/code-747%20lines-b6d3bf)

**[▶ Play it in your browser](https://hazem-soussi-ha.github.io/amazon-expedition-3d/)** · **[Project dossier & technical breakdown](https://hazem-soussi-ha.github.io/amazon-expedition-3d/showcase.html)**

---

## Quick start

**Option A — play now:** open the link above. That's it.

**Option B — one file, zero risk:** download [`dist/amazon-expedition-3d.html`](dist/amazon-expedition-3d.html) (616 KB, the whole runtime inlined) and double-click it. It works from a zip, an email attachment, a USB stick — nothing to break.

**Option C — run from source:**

```bash
git clone https://github.com/hazem-soussi-HA/amazon-expedition-3d.git
cd amazon-expedition-3d
# keep amazon_3d.html and three.min.js in the same folder, then open amazon_3d.html
open amazon_3d.html        # macOS
xdg-open amazon_3d.html    # Linux
start amazon_3d.html       # Windows
```

**Rebuild the single-file version** after editing the game:

```bash
./build.sh   # -> dist/amazon-expedition-3d.html
```

Requires: any modern browser with WebGL, plus `three.min.js` (vendored, r128) if you run from source.

---

## Controls

| Input | Action |
| --- | --- |
| `W` `A` `S` `D` | Move |
| Mouse | Look around (pointer lock) |
| `Shift` | Sprint — drains energy |
| `Space` | Jump |
| `E` | Pick up gems / the Golden Idol |
| `Esc` | Pause |

---

## What's in the box

- **Procedural 3D jungle** — an analytic heightfield for hills, a great river that must be crossed, and ancient ruins at the south end
- **Wildlife** — circling birds, drifting butterflies, hopping monkeys, all on closed-form paths
- **Survival economy** — sprinting and wading drain energy; resting restores it
- **A full quest loop** — temple → Golden Idol → return it to the boat, plus 10 hidden gems, score and a win state
- **Canvas-2D minimap, HUD design, procedural Web Audio SFX**, SVG logo and identity system
- **One file.** 747 lines of shippable code, plus one vendored runtime.

---

## Code map

Everything lives in [`amazon_3d.html`](amazon_3d.html), in reading order:

| Lines | Section | What lives there |
| --- | --- | --- |
| 95 | setup | renderer, PCF soft shadows, fog, camera, lights |
| 127 | terrain | `terrainH(x, z)` — the analytic heightfield, river, water plane |
| 167 | vegetation & rocks | tree / rock / grass placement, `inRiver`, `nearRuins`, `nearStart` |
| 247 | ruins & idol | temple geometry and the Golden Idol pedestal |
| 296 | expedition boat | the finish point and win state |
| 323 | gems | the ten collectibles and their placement |
| 346 | wildlife | birds, butterflies, monkeys — parametric, no animation clips |
| 397 | player & controls | pointer lock, mouse look, key state, jump |
| 461 | pickups | `nearestGem`, `tryPickup`, interaction hints |
| 495 | audio | `sfx()` — procedural Web Audio, no sample files |
| 510 | HUD & minimap | score, energy bar, `drawMinimap()` on Canvas 2D |
| 571 | physics & update | `collide()` push-out, delta-time movement, energy economy |
| 725 | main loop | fixed-loop `requestAnimationFrame` with a frame-time clamp |

[`showcase.html`](showcase.html) is the project dossier: the full design, identity and technical write-up, self-contained like the game.

---

## How this was built

AI-assisted authoring, human-directed design. The honest split:

| The AI did | I did |
| --- | --- |
| Drafted implementations of systems I specified | Architecture, the one-file / zero-dependency constraint, naming |
| Generated first-pass geometry and audio parameters | Art direction, colour grading, lighting mood, the SVG identity system |
| Reviewed, refactored and hardened | Debugging pointer lock and WebGL, playtesting, tuning the energy economy |
| Wrote first-pass documentation | Final editorial voice and technical accuracy |

What made it work:

1. **Constraints before code.** No framework, no bundler, no package manager, no asset files. The dependency graph is one sentence long, which is why it stayed readable enough to ship in a single sitting.
2. **Maths over noise.** Wildlife and terrain are closed-form, so the world is deterministic and nothing jitters.
3. **A draft is a draft.** The AI's first pass is a starting point, never a product — every line gets reviewed and tuned.
4. **Ship loops, not big bangs.** v1.0 plays end to end. Everything after that deepens what already exists (see the roadmap).

---

## Learn by extending it

Good first changes, each in one section of the code map:

1. **Denser canopy** — raise the tree count in *vegetation & rocks* and watch the frame budget.
2. **A sixth animal** — add a "firefly" alongside the wildlife using the same closed-form pattern.
3. **A harder river** — make wading drain energy faster, so crossing becomes a real decision.
4. **A second gem type** — gold gems worth 5, in the ruins only.
5. **Night mode** — drive the directional light and sky colour from a time variable in *setup*.

---

## Roadmap

| Version | Status | What |
| --- | --- | --- |
| v1.0 — First light | Shipped | The full game, playable end to end in one file |
| v1.1 — Weather & time of day | Next | Sun/moon cycle driving light and sky, plus rain that increases wading drain |
| v1.2 — Camp & inventory | Next | Placeable campfires, tradeable gems, reusing the collider and pickup systems |
| v1.3 — Wildlife depth | Planned | Steering behaviours, riverbank fish, night insects |
| v2.0 — WebGPU & co-op | Speculative | Modern pipeline, instanced foliage, local two-player |

---

## Contributing

Fork, make a branch, open a PR. Two house rules:

- **No build step and no new dependencies.** One file in, one file out.
- **Keep it readable.** If a change needs a paragraph to explain, it needs a refactor.

---

## License

Code: [MIT](LICENSE) — © 2026 Hazem Soussi. Fork it, learn from it, ship it.

The brand artwork — the Amazon Expedition 3D logo, signature, seals and dossier identity — is © Hazem Soussi and is *not* covered by the MIT license. Please don't reuse it as your own.

[`three.min.js`](three.min.js) is Three.js r128, © Three.js Authors, MIT.

---

## Learn to build like this

I teach people how to direct AI to ship real, working projects — 3D, games, web apps — without pretending the AI does it alone.

If that's you: **hazem.soussi@gmail.com** — or open an issue and say hi.

---

## Credits

Design, engineering and art direction by **Hazem Soussi** — 2026. AI-assisted authoring and code review. Zero asset files: 100% generated at runtime.
