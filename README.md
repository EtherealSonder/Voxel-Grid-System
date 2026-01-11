# Voxel Grid System

![Voxel Grid System Demo](docs/voxel-grid-demo.gif)

**Unity Version:** Unity 6.3 LTS (6000.3.2f1)

---

## Development Process (8-hour scope)

> The timelines below are approximate. The work was done iteratively, but everything described here was completed within an overall 8-hour window.

### Grid system and world setup
I started by building a clear grid-based world. The grid is rendered at runtime, shows world bounds, and acts as the foundation that all other systems rely on.

### Camera movement and bounds
I implemented a free-fly camera with WASD movement, vertical control, and speed boost. The camera is clamped to the grid bounds so the player always stays inside the playable area.

### Polycube shape representation and placement
I defined polycube shapes as data using scriptable objects. These shapes are instantiated at runtime as groups of unit cubes and placed cleanly into the grid.

### Interaction system
I built an interaction loop where the player can pick up a shape, move it across the grid, rotate it along the X, Y, and Z axes, validate placement, and either place or discard the shape.

### Save and load system
I added a save/load system that captures the full world state. Loading clears the scene and rebuilds it from saved data to restore the exact previous state.

### UI, feedback, and polish
I added a pause menu, audio feedback for interactions, visual feedback for valid and invalid placement, and an on-screen controls panel for clarity.

### Procedural polycube generation
With remaining time, I extended the system to support procedurally generated polycube shapes and integrated them into the existing placement, interaction, and save/load workflow.

---

## Build

**Playable build:**  
https://drive.google.com/file/d/1RMGdEPpDu7EeAd6J-HWVWLkfBnkc38pM/view?usp=drive_link

---

## Controls

WASD Move · RMB Pick Up · LMB Discard · Q/E/R Rotate X/Y/Z · Ctrl Down · Space Up · Shift Boost · Esc Pause
