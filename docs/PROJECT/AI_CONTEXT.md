# AI_CONTEXT

## Purpose

Provide compact project context for AI assistants.

## Usage

Read this file before making non-trivial changes to the project.

## Maintenance

Update after meaningful development sessions or when project direction changes.

## Rules

- Keep this concise.
- Include current conventions, constraints, and high-impact context.
- Do not duplicate full architecture or API guideline documentation.
- Reference other files instead of copying large sections.
- Treat Trello as the source of truth for task state once docs/PROJECT/TRELLO_WORKFLOW.md says the project board is ready.

## Project Summary

Workes.InventorySystem is a broad reusable .NET inventory package built around catalog-registered item definitions, inventory-owned runtime state, and replaceable layouts, policies, and rules.

## Current State

The package implementation and focused public documentation are complete. README.md is a concise package landing page;
the detailed manual lives in docs/QUICK_START.md, docs/CONCEPTS.md, docs/CATALOGS_AND_DEFINITIONS.md,
docs/INVENTORY_OPERATIONS.md, docs/LAYOUTS.md, docs/POLICIES_AND_RULES.md, docs/TRANSACTIONS.md,
docs/EVENTS_AND_UI.md, docs/FAILURES.md, docs/PERSISTENCE.md, and docs/EXTENDING.md.

## Current Conventions

- Use docs/CONCEPTS.md for the package mental model and shared terminology.
- Use docs/CATALOGS_AND_DEFINITIONS.md for item-universe setup, registration, tags, attributes, schemas, and definition ID migrations.
- Use docs/INVENTORY_OPERATIONS.md for inventory creation, local queries and mutations, item instances, metadata, and basic sort/repack operations.
- Use docs/LAYOUTS.md for built-in placement models, contexts, restrictions, footprints, sorting, repacking, and layout persistence concepts.
- Use docs/POLICIES_AND_RULES.md for stack resolvers, capacity policies, rules, inventory-owned configuration mutation, rebuild actions, and parameter-change events.
- Use docs/TRANSACTIONS.md for transaction staging and commit, cross-inventory movement ownership, bulk and maximum moves, mapped placement compatibility, swaps, atomicity, and failure behavior.
- Use docs/EVENTS_AND_UI.md for committed-change payloads, affected contexts, refresh decisions, movement and metadata behavior, configuration events, and UI synchronization.
- Use docs/FAILURES.md for structured expected-failure results, project-owned inventory exceptions, stable categories and codes, and extension failure guidance.
- Use docs/PERSISTENCE.md for snapshot DTOs, definition migrations, external serialization boundaries, layout data, compatibility, restore behavior, and application save versioning.
- Use docs/EXTENDING.md for custom definitions, policies, rules, layouts, capabilities, cloning, sorting, footprints,
  validation invariants, and custom persistence codecs.
- Use docs/QUICK_START.md for the shortest zero-to-working application walkthrough and links into focused guides.
- Keep normal application usage separate from extension-authoring guidance.
- Keep focused public guides directly under docs/.
- Treat the catalog's registered definition object as canonical for each definition id.
- Route runtime mutations through inventory-owned APIs.
- Treat Inventory.Items as storage order and layouts as presentation/placement.
- Keep inventory-owned repack layout-agnostic. Custom layouts opt in with `IRepackableInventoryLayout<TKey>` and
  `IParameterizedRepackableInventoryLayout<TKey>` while inventory retains ordering, validation, atomic commit, and events.
- Layouts that can reposition surviving items after structural, amount, or metadata mutations opt in with
  `IInventoryLayoutReconciler<TKey>`. Inventory diffs their surviving item contexts and merges reconciliation output into
  the operation's single event.
- Classify every `ItemMoved<TKey>` with `ItemMovementCause`: direct targets are `ExplicitMove`; sort, repack, and
  collateral layout reconciliation use `Sort`, `Repack`, and `LayoutReflow`. One event may contain mixed causes.
- `RequiresFullRefresh` means an event delta is incomplete for context-level synchronization, not merely complex.
  Precise sort and repack events remain incremental; topology or unrepresented layout-owned presentation can request it.
- Use non-generic `InventorySnapshot` and `CaptureSnapshot()` for new persistence work. Custom key codecs are assigned
  by key type; every layout owns a mandatory stateless capture/decode/exact-restore codec. The generic
  `Serialize`/`Deserialize` model is legacy only.

## Important Constraints

- Trello owns documentation-migration task state; do not reproduce the migration plan here.
- Keep README.md as a concise package landing page and place detailed usage or extension guidance in the focused public
  guides.
- Do not retrospectively populate ARCHITECTURE.md, API_GUIDELINES.md, or DECISIONS.md unless the developer later decides to do so.

## External Task Board

Trello is used for task state after docs/PROJECT/TRELLO_WORKFLOW.md has been completed for this project.

Use Trello for current work, next work, backlog, bugs, blocked work, roadmap/planning cards if used, and completed task tracking.

Do not duplicate Trello task state here.

## AI Instructions

- Read docs/PROJECT/PROJECT_CONTEXT.md first.
- Read docs/PROJECT/TRELLO_WORKFLOW.md before inspecting or modifying task state.
- Read docs/PROJECT/ARCHITECTURE.md before making structural changes.
- Read docs/PROJECT/API_GUIDELINES.md before changing public-facing API design.
- Read docs/PROJECT/DECISIONS.md before revisiting architectural choices.
- Update this file after meaningful development sessions.
- Do not duplicate large sections from other documentation files here.
