# PCG Phase2 Task Anchor Plan

## 1. Updated assumptions

Based on the latest clarification, the plan should use these facts as hard constraints:

- All task-room prefabs already have 4 sockets.
- Therefore, task-room outward expansion in phase 2 does not require extra task-room prefab work first.
- The more urgent prefab/role issue is not `SideTask`, but `Birth/Start`.

This changes the previous priority order:

- Phase 2 task-anchor expansion is still the correct topology direction.
- But the first resource-role mismatch to solve in the current system is `Start/Birth` being replaced by `Connector`.

---

## 2. Why `Birth` is being replaced by `Connector`

### 2.1 The `Birth` prefabs are already configured as `Start`

Your runtime profile already has a dedicated `Start` pool:

- [PcgGenerationProfile.asset](/E:/Unity%20Projects/Matrix/Assets/Resources/Configs/PCGCongfig/PcgGenerationProfile.asset:38)

And the two `Birth` prefabs themselves are authored with `defaultRole: 0`, which is `Start`:

- [Birth - Long.prefab](/E:/Unity%20Projects/Matrix/Assets/Resources/Prefab/Rooms/Birth/Birth%20-%20Long.prefab:48)
- [Birth - Square.prefab](/E:/Unity%20Projects/Matrix/Assets/Resources/Prefab/Rooms/Birth/Birth%20-%20Square.prefab:48)

So the problem is not:

- missing `Birth` prefab
- wrong pool assignment
- prefab self-role being lost

### 2.2 Runtime selection is based on graph degree, not prefab self-role

The runtime selection logic is here:

- [PcgMapGenerator.cs](/E:/Unity%20Projects/Matrix/Assets/Scripts/PCG/Generation/PcgMapGenerator.cs:600)

Current behavior:

1. Pick candidates from the requested role pool.
2. Filter by `requiredConnectorCount`.
3. If no candidate survives and the role is not `Connector`, fallback to the `Connector` pool.

This means the real selector is:

- `AssignedRole`
- graph node degree
- connector count compatibility

It does **not** prioritize prefab `defaultRole` once pool filtering begins.

### 2.3 The current `Start` node is usually not a one-exit node

The current start selection logic is here:

- [RoomRoleAllocator.cs](/E:/Unity%20Projects/Matrix/Assets/Scripts/PCG/Generation/RoomRoleAllocator.cs:72)

It selects `Start` from the primary ring, not from a leaf node.

That implies:

- ring nodes are naturally degree 2
- ring nodes with branches can be degree 3 or more

So the current topology is effectively saying:

- `Start` is a ring-entry region

But your current `Birth` prefab authoring is saying:

- `Birth` is a single-exit room

Those two semantics conflict directly.

### 2.4 Why the fallback happens

Because `Birth` is single-exit, when the selected `Start` node requires 2 or 3 connectors:

- no `Start` prefab passes `ConnectorCount >= requiredConnectorCount`
- `SelectRoomPrefab(...)` falls back to the `Connector` pool
- the generated room is visually/functionally a `Connector` room

So the observed runtime behavior is expected under the current implementation.

This is not random.

---

## 3. Immediate conclusion

At the moment, there are two separate issues:

### 3.1 The phase-2 topology problem

- task rooms should become expansion anchors
- generation should become two-stage

### 3.2 The current `Birth/Start` semantic mismatch

- topology thinks `Start` is a multi-connection ring node
- prefab authoring thinks `Birth` is a one-exit room

The second issue exists even before phase 2 lands.

---

## 4. What should change in planning

The previous plan should be updated as follows.

### 4.1 Remove task-room prefab expansion as a blocker

Because all task rooms already have 4 sockets:

- phase-2 task-anchor outward growth can proceed
- no extra task-room connector-capacity work is required first

### 4.2 Add `Start/Birth` as a dedicated planning item

This item should now be treated as an explicit architecture decision:

#### Option A: Keep current topology, redefine `Birth` as a multi-exit start room

- `Start` remains on the ring/main skeleton
- provide 2-3 socket `Start/Birth` rooms
- this is the lowest-risk solution

#### Option B: Keep single-exit `Birth`, change topology semantics

- `Start` no longer sits directly on the ring
- generate a dedicated one-exit `Birth` leaf
- connect it into the main map as a pre-entry room

This is a bigger topology change because it changes:

- start-node selection
- main path semantics
- stitching assumptions for the first room

For the current architecture, Option A is much cheaper and more stable.

---

## 5. Recommended architecture after this clarification

The updated recommended roadmap is:

1. Keep the two-stage generation direction.
2. Treat all task rooms as phase-2 anchors.
3. Exclude task-room prefab capacity from the blocker list.
4. Add a separate `Start/Birth` decision before implementation.

That gives two parallel workstreams:

### Workstream A: Phase-2 task-anchor expansion

- Build phase-1 skeleton
- Plan task anchors
- Expand outward from task rooms
- Rebuild triggers after expansion

### Workstream B: `Start/Birth` semantic fix

Choose one:

- align prefab to current topology
- or align topology to single-exit `Birth`

---

## 6. Recommended implementation order

### Step 1

Keep the phase-2 plan and start the code refactor for:

- `BuildPhase1(...)`
- `PlanTaskAnchors(...)`
- `ExpandFromTaskAnchors(...)`
- `FinalizeRoles(...)`

### Step 2

Before touching final start-room behavior, explicitly decide:

- Is `Birth` meant to be a one-exit narrative entry room?
- Or is it meant to be the gameplay start region on the ring?

### Step 3

If the answer is "gameplay start region", do this:

- stop treating single-exit `Birth` as valid for current `Start`
- replace or expand `Start` prefab pool with multi-exit start rooms

### Step 4

If the answer is "one-exit narrative birth room", do this:

- move `Birth` outside the current ring-based `Start` logic
- attach it as a generated pre-room before the main skeleton

---

## 7. Final planning judgment

After your clarification, the planning conclusion becomes:

- The two-stage task-room-as-anchor direction is still valid.
- Task-room prefab capacity is not a blocker.
- The current prefab-role issue is concentrated on `Birth/Start`, not on task rooms.

In other words:

- phase 2 should continue
- but `Start` semantics must be made explicit before implementation

If you want, the next document can be narrower and fully focused on one thing:

- `Start/Birth` semantic redesign options under the current PCG graph

or I can skip more documents and directly start the first code refactor pass for the two-stage generation skeleton.
