<!--
Sync Impact Report
Version: template -> 1.0.0

Added principles:
- Existing Code Is the Source of Truth
- Vertical Slices
- Whole Solution Validation
- Local First
- Licensing
- Large Library Resilience
- Explicit Boundaries
-->

# Resonance Constitution

## Core Principles

### I. Existing Code Is the Source of Truth

Every change MUST begin by inspecting the current repository.

The agent MUST inspect:

- affected projects;
- existing services;
- interfaces;
- dependency injection;
- persistence;
- models;
- tests;
- existing implementation of the requested capability.

Agents MUST NOT create parallel services, DTOs, repositories, models, databases or audio
pipelines when equivalent concepts already exist.

Repository reality has precedence over prompt assumptions.

### II. Vertical Slices, Not Horizontal Microtasks

A feature MUST use at most three implementation slices by default.

Each slice MUST deliver observable behavior end-to-end.

The following MUST NOT normally become isolated tasks:

- create DTO;
- create interface;
- create repository;
- register DI;
- create service;
- create ViewModel;
- create View.

When those elements belong to one behavior, they belong to the same slice.

Every slice MUST repeat:

```markdown
## META IMUTÁVEL

Problem:
[Global problem]

Definition of Success:
[Global success]
```

This prevents agents from executing a task without knowledge of the feature objective.

### III. Whole-Solution Validation

No slice is complete until the whole affected solution is coherent.

Minimum gates:

```powershell
dotnet restore
dotnet build Nagi.sln --configuration Release --no-restore
dotnet test Nagi.sln --configuration Release --no-build
```

Existing analyzers, packaging validations and CI rules MUST also pass.

Passing one local unit test does NOT mean a task is complete if:

- another project fails compilation;
- DI is broken;
- database migration fails;
- application startup fails;
- packaging fails;
- an integration flow regresses.

### IV. Local-First and Privacy-First

Local playback MUST work offline.

Library browsing MUST work offline.

Equalizer MUST work offline.

Playlists MUST work offline.

Local metadata MUST work offline.

Local lyrics MUST work offline.

External services MUST be optional.

Providers MUST be independently enableable/disableable.

The application MUST NOT upload complete music files.

Only the minimum documented provider payload may leave the machine.

Secrets and API keys MUST NOT be committed.

### V. Licensing and Provider Compliance

The project derives from Nagi and MUST preserve applicable GPLv3 requirements.

New dependencies MUST have their license reviewed before merge.

External providers MUST have documented:

- Terms;
- Authentication;
- Rate limit;
- Cache policy;
- Attribution;
- Commercial restrictions.

The application MUST NOT bypass API restrictions.

A publicly reachable API MUST NOT be interpreted as permission to redistribute its content.

Lyrics and artwork MUST be treated as copyrighted content unless documented otherwise.

The project MUST NOT copy proprietary Winamp:

- source;
- logos;
- trademarks;
- skins;
- icons;
- sounds;
- preset data.

The product may use original implementations inspired by general player interaction concepts.

### VI. Large Library Resilience

Recursive scanning MUST:

- support multiple roots;
- support subdirectories;
- avoid loops;
- avoid duplicates;
- survive unreadable directories;
- survive malformed audio files;
- expose progress;
- expose cancellation;
- avoid UI blocking;
- use bounded concurrency;
- support incremental scanning where practical;
- preserve database consistency.

Performance claims MUST be measured.

### VII. Explicit Audio and Metadata Boundaries

Responsibilities MUST remain distinguishable:

- Playback;
- DSP / Equalizer;
- Audio Analysis / FFT;
- Library Discovery;
- Local Metadata;
- Remote Metadata;
- Lyrics;
- Persistence;
- Presentation.

UI code MUST NOT implement filesystem traversal.

UI code MUST NOT directly call provider APIs.

Remote metadata MUST retain provenance.

Remote metadata MUST NOT silently overwrite local files.

Tag writes require:

```text
Review
  -> Diff
  -> Explicit confirmation
  -> Apply
```

## Architecture & Change Discipline

The project MUST evolve the architecture found in the selected Nagi revision.

The agent MUST NOT replace the architecture with a theoretical clean-room design.

Before a plan is approved, identify:

- affected `.csproj`;
- existing types reused;
- DI registration;
- persistence impact;
- migration impact;
- WinUI thread boundaries;
- existing tests;
- licensing impact.

New library, layer, factory, repository, mediator or abstraction requires concrete
justification.

"Best practice" alone is NOT justification.

Refactors unrelated to the active feature MUST NOT be included.

## Spec Kit Execution Contract

Every feature spec MUST contain:

```markdown
## META IMUTÁVEL

> Problema:
> [2-3 linhas]

> Definição de Sucesso:
> [resultado observável]

> Regra de Ouro:
> Evoluir o código existente.
> Não duplicar abstrações.
> Não reescrever áreas não relacionadas.
```

Every `tasks.md` MUST:

1. contain at most 3 implementation slices by default;
2. use vertical slices;
3. repeat META IMUTÁVEL in each slice;
4. reference projects/files only after repository inspection;
5. finish every slice with build/tests;
6. make the final slice validate the entire flow;
7. never mark a slice complete while build/tests fail;
8. modify spec/plan if repository reality disproves an assumption.

## Agent Stop Conditions

The agent MUST stop for human review when:

- provider licensing is unclear;
- project license would change;
- destructive tag rewrite is proposed;
- destructive database migration is proposed;
- playback engine replacement is proposed;
- more than 3 vertical slices are required;
- implementation contradicts the specification.

## Required Spec Kit Flow

For production-impacting features:

```text
/speckit.specify
/speckit.clarify
/speckit.plan
/speckit.checklist
/speckit.tasks
/speckit.analyze
/speckit.implement
/speckit.converge
```

The constitution is established before feature work and amended only when governance itself
changes.

## Governance

This constitution supersedes ad-hoc implementation decisions.

Any artifact conflicting with a MUST rule is blocked.

Amendments require:

- rationale;
- human approval;
- version bump.

Versioning:

- MAJOR: backward incompatible governance change;
- MINOR: new principle or material expansion;
- PATCH: clarification with no behavioral change.

Every plan MUST contain a Constitution Check.

`speckit.analyze` MUST treat violation of a MUST rule as critical.

Version: 1.0.0  
Ratified: 2026-09-19  
Last Amended: 2026-09-19
