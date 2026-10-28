# Claude Code + Unity + Blender setup

This guide connects **Claude Code** to both the **Unity Editor** and **Blender**, so a single Claude Code
chat can model an asset in Blender, export it, import it into Unity, and wire it into the game.

```
                 ┌── MCP (stdio) ──> Unity relay ──> Unity Editor  (AI Assistant package)
Claude Code ─────┤
                 └── MCP (stdio) ──> blender-mcp ──TCP 127.0.0.1:9876──> Blender (MCP add-on)
```

It was written for the [UnityPlayGround](https://github.com/akrimahuzaifa/UnityPlayGround) project
(the Neon Flux game), but the Claude/Unity/Blender parts work for any Unity project. Commands are for
Windows (PowerShell); notes for macOS/Linux are inline.

## What you need

| Tool | Version used here | Notes |
| --- | --- | --- |
| Unity | 6000.6.2f1 (Unity 6.6) | Open the project with this version or newer. |
| Unity AI Assistant package | `com.unity.ai.assistant` 2.19.0-pre.2 | Already in this project's `Packages/manifest.json`. Provides the Unity MCP server. |
| Blender | 5.1 or newer | The MCP add-on needs 5.1+. |
| Claude Code | latest | CLI, VS Code extension or desktop app. |
| uv | any recent | Python tool installer used for the Blender MCP server. |
| Git + Git LFS | any recent | Models, textures and `.blend` files are stored in LFS. |

Install the tools you are missing:

```powershell
# Claude Code (native installer). macOS/Linux: curl -fsSL https://claude.ai/install.sh | bash
irm https://claude.ai/install.ps1 | iex

# uv. macOS/Linux: curl -LsSf https://astral.sh/uv/install.sh | sh
powershell -ExecutionPolicy ByPass -c "irm https://astral.sh/uv/install.ps1 | iex"

# Git LFS (once per machine)
git lfs install
```

## 1. Clone the project

```powershell
git clone https://github.com/akrimahuzaifa/UnityPlayGround.git
cd UnityPlayGround
git lfs pull
```

Open the folder in Unity Hub with Unity 6000.6.2f1. The first import takes a few minutes. Open
`Assets/Scenes/NeonFlux.unity` (or use the menu **Neon Flux > Open Playable Scene**).

## 2. Connect Claude Code to Unity

The Unity AI Assistant package ships an MCP server. Unity copies a small relay program to
`%USERPROFILE%\.unity\relay\` (macOS/Linux: `~/.unity/relay/`), and Claude Code talks to Unity through it.

1. In Unity, open **Edit > Project Settings > AI > Unity MCP Server**.
2. Find **Claude Code** in the list of clients and click **Configure**. This writes the server entry into
   your Claude Code config for you.
3. If you prefer to do it by hand, create `.mcp.json` in the project root (it is git-ignored because it
   contains your local paths):

   ```json
   {
     "mcpServers": {
       "unity": {
         "command": "C:\\Users\\<you>\\.unity\\relay\\relay_win.exe",
         "args": ["--mcp", "--project-path", "C:\\path\\to\\UnityPlayGround"]
       }
     }
   }
   ```

   On macOS the relay is `relay_mac_arm64` or `relay_mac_x64`; on Linux it is `relay_linux`.
4. Start Claude Code in the project folder. The first time, Claude Code asks you to approve the project's
   `unity` server; say yes. Unity may also ask you to allow the connection; allow it.

Keep the Unity Editor open whenever Claude should work in Unity.

## 3. Install the Blender add-on

The add-on is the official one from [Blender Lab](https://www.blender.org/lab/mcp-server/). It runs a small
TCP server inside Blender on `127.0.0.1:9876`.

1. Download the add-on from [blender.org/lab/mcp-server](https://www.blender.org/lab/mcp-server/#addon).
2. In Blender: **Edit > Preferences > Get Extensions**, open the **⌄** menu at the top right, choose
   **Install from Disk...** and pick the downloaded file.
3. In **Edit > Preferences > System > Network**, turn on **Allow Online Access**. The add-on refuses to
   start its server without it, even though it only listens on localhost.
4. In **Edit > Preferences > Add-ons**, find **MCP**, make sure it is enabled, and note its version
   (for example 1.0.3). Auto-start is on by default, so the server starts with Blender.

## 4. Install the Blender MCP server for Claude Code

The MCP server is a separate program that Claude Code launches. It comes from the same repository as the
add-on. **Use the tag that matches your add-on version.**

```powershell
mkdir $HOME\tools -Force
cd $HOME\tools
git clone https://projects.blender.org/lab/blender_mcp.git
cd blender_mcp
git checkout v1.0.3          # same version as the add-on from step 3
cd mcp
uv tool install --force .
```

This installs `blender-mcp.exe` into `%USERPROFILE%\.local\bin\` with its own Python environment.
Now register it with Claude Code at **user** scope, so it is available in every project:

```powershell
claude mcp add blender --scope user -- "$HOME\.local\bin\blender-mcp.exe"
```

macOS/Linux: `claude mcp add blender --scope user -- ~/.local/bin/blender-mcp`

> Already using the Blender extension in **Claude Desktop**? Its server lives inside Claude Desktop's
> data folder and can move when Desktop updates. Pointing Claude Code at it works, but the standalone
> install above is more robust.

## 5. Check that everything is connected

1. Open Blender and Unity.
2. In a terminal, run:

   ```powershell
   claude mcp list
   ```

   You want `blender ... ✔ Connected` and `unity ... ✔ Connected`.
3. Start (or restart) Claude Code. MCP tools only load when a session starts. In VS Code use
   **Developer: Reload Window** or open a new Claude Code chat.
4. Ask Claude something that needs both sides, for example:
   *"List the objects in my Blender scene, then tell me which scene is open in Unity."*

## 6. How this project uses Blender assets

These conventions keep the Blender to Unity round trip predictable. Ask Claude to follow them.

- **Blender sources** go in `Art/Blender/*.blend`, outside `Assets/`, so Unity does not try to import them.
  Example: `Art/Blender/NeonStars.blend`.
- **Exports** go into `Assets/` as FBX. Models the game builds from code at runtime go in
  `Assets/Resources/NeonFlux/`, so they can be loaded with `Resources.LoadAll<Mesh>(...)`.
  Example: `Assets/Resources/NeonFlux/NeonStars.fbx`.
- **FBX export settings** (Blender uses Z-up, Unity uses Y-up): Apply Scalings = *FBX All*,
  Forward = *Z*, Up = *Y*, *Apply Transform* on, *Selected Objects* only, mesh objects only.
  With Forward = *-Z* Unity mirrors the X axis, which only goes unnoticed on symmetric models
  (the stars and coin were exported that way; the ship would point backwards).
- **Model scale and orientation:** model at real size in meters, centered on the origin. Side-view
  pieces are modelled in Blender's front view (the XZ plane); Blender +X becomes Unity +X (the game's
  forward direction) and Blender +Z becomes Unity +Y.
- **Unity import settings:** materials *None*, no animation, cameras or lights. The game draws everything
  with its own unlit "neon" materials (see `Assets/Scripts/Flappy/Neon.cs`), so only geometry is needed.
- **Keep meshes light.** The star models are 16 to 32 triangles each.

## Troubleshooting

| Symptom | Check / fix |
| --- | --- |
| `Cannot connect to Blender at localhost:9876` | Blender is not running, or the MCP add-on is disabled. Open Blender; in **Preferences > Add-ons > MCP** check the server is running. Confirm with `netstat -ano \| findstr 9876`. |
| Add-on says online access must be enabled | Turn on **Preferences > System > Network > Allow Online Access**. |
| `claude mcp list` shows `blender ✗ Failed` | The path in the registration is wrong or the server was removed. Run `claude mcp remove blender --scope user`, then repeat step 4. |
| Blender tools do not appear in Claude Code | Restart the Claude Code session (tools load at startup). |
| Odd Blender errors after updating the add-on | The server and add-on versions differ. In `~/tools/blender_mcp` run `git fetch --tags`, `git checkout v<add-on version>`, then `cd mcp; uv tool install --force .`. |
| `unity ... Pending approval` | Start `claude` in the project folder and approve the `unity` server. |
| `Unity not detected` | The Unity Editor is closed, or it is in the middle of a script reload. Wait for compiling to finish and try again. |
| Unity does not pick up a new FBX or script | Click into the Unity window. Unity only refreshes assets and recompiles when it has focus (or when asked via **Assets > Refresh**). |
| Play mode does not advance while Claude tests it | Unity pauses the game loop when its window is not focused. Click into Unity, or enable **Run In Background** in Player settings. |

### Testing the Blender add-on without Claude

This sends one request straight to the add-on, bypassing the MCP server. A reply with `"status": "ok"`
means the Blender side works.

```powershell
python -c "import socket,json;s=socket.create_connection(('127.0.0.1',9876));s.sendall(json.dumps({'type':'execute','code':'import bpy\nresult={\"v\":bpy.app.version_string}','strict_json':True}).encode()+b'\0');print(s.recv(65536).decode())"
```
