# Uckers 3D (Stepped Board)

A minimal Unity 2022.3 LTS project that generates the entire Uckers board, tokens, and UI at runtime. Copy these files into a fresh Unity project and hit Play.

## Prerequisites
- Unity 2022.3 LTS (Built-in Render Pipeline)
- Windows or macOS editor play mode

## Getting Started
1. Create a new **3D (Built-in RP)** project in Unity 2022.3.
2. Copy this repository's contents into the Unity project directory (overwrite the empty Assets/Packages/ProjectSettings folders).
3. Open `Assets/Scenes/Bootstrap.unity`. The scene is intentionally empty apart from a Bootstrapper component—everything else is spawned at runtime.
4. Press **Play**. If the scene is ever empty, the Bootstrapper retries after 10 seconds and rebuilds the hierarchy automatically.

## Controls
- **Space** or **Roll** button: roll the die.
- **Left Mouse**: cycle/select eligible tokens; double-click a token to confirm a move.
- **Enter**: confirm currently highlighted token.
- **R**: restart the scene.
- **Right Mouse Drag**: orbit the isometric camera slightly.
- **Mouse Wheel**: zoom in/out.

## Building
- **Windows**: `File > Build Settings > PC, Mac & Linux Standalone > Target Platform Windows > Build`.
- **macOS**: `File > Build Settings > PC, Mac & Linux Standalone > Target Platform macOS > Build`.

No manual scene setup is required. All meshes, materials, lighting, and UI are generated when play mode starts.

## Version Control
1. Initialize git in the Unity project root (if not already): `git init`.
2. Commit changes (this repo already includes a Unity-ready `.gitignore`).
3. Push to your GitHub remote: `git remote add origin <url>` then `git push -u origin main`.

## Running Tests
- Open the Unity Test Runner (Window > General > Test Runner).
- Select the **Edit Mode** tab and run the `Uckers.Tests.Domain.Smoke.SmokeTests` suite.

## Player Count
Choose between 2, 3, or 4 players when the game starts. If no selection is made within a few seconds the game automatically defaults to a 2-player match.

## Troubleshooting

### No cameras rendering
The Bootstrapper now auto-attaches itself at runtime and guarantees a main camera and directional light even if the scene starts empty. This behaviour is covered by the edit-mode `BootstrapRenderingTests` suite.
