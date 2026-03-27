# GPU Animations

Unity URP demo comparing different character animation and particle approaches for mobile crowd rendering.

[![Demo Video](https://img.youtube.com/vi/dyg9eFg5pWA/0.jpg)](https://www.youtube.com/watch?v=dyg9eFg5pWA)

## Implemented Solutions

- **CPU Skinning** — standard Unity `SkinnedMeshRenderer` + `Animator` pipeline.
- **GPU Skinning** — Unity's GPU skinning with `SkinnedMeshRenderer`.
- **Bone Texture (custom)** — animation clips baked into an `RGBAHalf` texture (3 rows per bone per frame, encoding a 4×3 matrix). A static mesh with bone indices/weights packed into UV2 is skinned entirely in the vertex shader (`GPUAnimationCore.hlsl`), with frame interpolation and crossfade blending. No compute shaders — all work happens in the vertex stage via `MaterialPropertyBlock` instancing.
- **Shared Particle Emitters** — a fixed set of `ParticleSystem` emitters reused across agents via custom particle data tagging, reducing per-agent overhead.
- **Pooled Particle Instances** — classic object-pooled VFX prefabs with prewarm support, one instance per agent.

## Goal

The primary goal was to implement the **bone-texture vertex skinning** approach and benchmark it against Unity's built-in **CPU skinning** and **GPU skinning**, while also exploring ways to reduce particle system overhead for large crowds on mobile.

Both animation paths (skinned vs bone texture) and both particle modes (shared vs pooled) produce **visually identical** results, allowing a fair apples-to-apples performance comparison.

## Animation Baker Tool

An editor window (**Tools → GPU Animation Baker**) that bakes `SkinnedMeshRenderer` animations into GPU-ready assets:

1. Select a GameObject with `SkinnedMeshRenderer` + `Animator`, pick clips, set sample FPS.
2. The tool evaluates animation curves frame-by-frame, computes `worldToLocal * bone * bindpose` matrices, and writes the top 3 rows of each 4×4 matrix as `RGBAHalf` pixels into a texture (width = `boneCount × 3`, height = total frames).
3. It also generates a static mesh copy with bone indices and normalized two-bone weights packed into UV2, so the original `SkinnedMeshRenderer` is no longer needed at runtime.
4. Outputs: animation texture, baked mesh, and a `GPUAnimationData` ScriptableObject with clip metadata (start frame, frame count, FPS, loop flag).

## How Shared Particles Work

The classic (pooled) approach instantiates a separate `ParticleSystem` per agent — easy to manage, but costly when hundreds of agents each own their own emitter.

**Shared emitters** flip this: a single `ParticleSystem` instance is created per VFX prefab type and stays alive for the entire session. When an agent needs an effect, particles are injected into this shared system via `ParticleSystem.Emit` at the agent's position.

This eliminates per-agent `ParticleSystem` overhead. No extra GameObjects, no per-system simulation cost, no dynamic batching processing.

Both particle modes use an **object pool with prewarm** — instances are pre-instantiated before gameplay begins, avoiding runtime allocation spikes.

## Benchmarks

Tested on **Samsung Galaxy A53**, target frame rate **30**, render target **1200×540**, **200** agents.

### Warmed-up device


| Configuration                   | CPU (ms) | GPU (ms) | Thermal | Bottleneck      |
| ------------------------------- | -------- | -------- | ------- | --------------- |
| CPU skinning + Pooled particles | 43       | 20       | 0.85    | CPU             |
| GPU skinning + Pooled particles | 39       | 27       | 0.86    | CPU             |
| Bone texture + Shared particles | 13       | 21       | 0.71    | TargetFramerate |
| Bone texture + Pooled particles | 16       | 21       | 0.77    | TargetFramerate |


### Cold device


| Configuration                   | CPU (ms) | GPU (ms) | Thermal | Bottleneck      |
| ------------------------------- | -------- | -------- | ------- | --------------- |
| CPU skinning + Pooled particles | 24       | 20       | 0.6     | TargetFramerate |
| GPU skinning + Pooled particles | 18       | 25       | 0.6     | TargetFramerate |
| Bone texture + Shared particles | 13       | 21       | 0.6     | TargetFramerate |
| Bone texture + Pooled particles | 16       | 21       | 0.6     | TargetFramerate |


### Key Takeaways

- **Bone texture** delivers the lowest CPU time across all scenarios (13–16 ms), making it **~3× faster on CPU** than standard CPU skinning on a warmed-up device.
- The difference is most pronounced under thermal throttling — bone texture stays bottlenecked only by the target framerate, while CPU/GPU skinning becomes CPU-bound.
- **Shared particle emitters** shave ~3 ms off CPU compared to pooled instances when paired with bone texture.

## Project Structure

```
Assets/
├── Scripts/
│   ├── GPUAnimation/       # Bone texture baking (editor) & runtime player
│   ├── AgentAnimation/     # Abstraction over skinned / GPU animation
│   ├── Crowd/              # Spawn, pooling, NavMesh agents
│   ├── Vfx/                # Shared & pooled particle dispatching
│   ├── Pool/               # Generic object pool
│   └── Bombing/            # Tap-to-bomb crowd interaction
├── Shaders/
│   ├── GPUAnimationCore.hlsl      # Bone texture sampling & vertex skinning
│   ├── GPUAnimationLit.shader     # URP Lit + GPU animation
│   ├── GPUAnimationLambert.shader # Simplified Lambert variant
│   └── GPUAnimationUnlit.shader   # Unlit variant
└── Scenes/
    └── SampleScene.unity
```

