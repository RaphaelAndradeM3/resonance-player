# Requirements Quality Checklist: Feature 007 — Lyrics Engine

**Purpose**: Reviewer-owned requirements-quality gate ("Unit Tests for English") validating completeness, clarity, measurability, and scenario coverage for Feature 007  
**Created**: 2026-09-23  
**Feature**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md)  

**Review Ownership**: This checklist is a reviewer-owned requirements-quality review artifact. Mark an item `[x]` only when the reviewer determines the requirements-quality criterion is satisfied.  
**Marker Semantics**: `[x]` means the criterion has been reviewed and satisfied for requirements quality. It does not mean implementation work is complete.  

---

## 1. Requirement Completeness

- [ ] CHK001 - Are all 6 steps of the canonical lyrics resolution order (Embedded Synced $\to$ Embedded Plain $\to$ Sidecar `.lrc` $\to$ Sidecar `.txt` $\to$ Local Cache $\to$ Remote Providers) explicitly specified without gaps? [Completeness, Spec §2, Spec §FR-001]
- [ ] CHK002 - Are both matching rules for local sidecar files (`<NomeDoAudio>.lrc/.txt` prioritário e `<Artista> - <Título>.lrc/.txt` como fallback, case-insensitive) clearly documented? [Completeness, Spec §FR-002, Clarifications §Session 2026-09-23]
- [ ] CHK003 - Are the lifecycle states and persistence boundaries of the local cache (`%LocalAppData%\Resonance\Cache\Lrc\`) explicitly defined? [Completeness, Spec §FR-005, Plan §Slice 2]
- [ ] CHK004 - Is the explicit export contract (`ExportSidecarLrcAsync`) for saving sidecars on demand documented with target naming rules? [Completeness, Spec §FR-008, Contracts §ILrcService]
- [ ] CHK005 - Are the database persistence requirements for instrumental status (`IsInstrumental`) and calibration offset (`LyricsOffsetMs`) in the `Song` entity documented? [Completeness, Data Model §2, Spec §FR-009, Spec §FR-010]

---

## 2. Requirement Clarity & Measurability

- [ ] CHK006 - Is the local file reading and rendering latency quantified with an objective threshold (< 50ms)? [Measurability, Spec §SC-001]
- [ ] CHK007 - Is the active lyric line highlight synchronization precision quantified with a measurable threshold (minimum 100ms precision)? [Measurability, Spec §FR-003, Spec §SC-002]
- [ ] CHK008 - Is the Local-First criterion ("zero remote network requests if local lyrics exist") formulated with unambiguous pass/fail criteria? [Measurability, Spec §SC-003, Constitution §IV]
- [ ] CHK009 - Is the timing offset adjustment granularity explicitly quantified (steps of 100ms and 500ms)? [Clarity, Spec §FR-010, Clarifications §Session 2026-09-23]
- [ ] CHK010 - Is the seek-by-click interaction behavior quantified when jumping to future or past lyric lines? [Clarity, Spec §FR-004, Plan §Slice 3]

---

## 3. Requirement Consistency & Architectural Boundaries

- [ ] CHK011 - Are requirements consistent with Constitution Principle IV (Local-First and Privacy-First) regarding offline operation and optional remote providers? [Consistency, Constitution §IV, Spec §1]
- [ ] CHK012 - Does the spec enforce Constitution Principle VII (Explicit Boundaries) prohibiting silent writes to the user's music directory upon downloading remote lyrics? [Consistency, Constitution §VII, Spec §FR-005]
- [ ] CHK013 - Are architectural boundaries maintained between WinUI presentation (`LyricsPageViewModel`), resolution engine (`ILrcService`), and tag extraction (`ATL.Track`) without UI-level filesystem traversals? [Consistency, Constitution §VII, Plan §Project Structure]
- [ ] CHK014 - Does the specification strictly evolve the existing `ILrcService` and `LrcService` without introducing redundant parallel lyrics services or engines? [Consistency, Constitution §I, Plan §Slice 1]

---

## 4. Canonical Resolution & Local-First Resilience

- [ ] CHK015 - Are fallback requirements clearly defined when embedded lyrics contain empty tags or whitespace? [Edge Case, Spec §Edge Cases, Research §2]
- [ ] CHK016 - Are timestamp format variations (e.g., standard `[mm:ss.xx]`, extended `[hh:mm:ss.xx]`, comma decimal separators `[mm:ss,xx]`) explicitly addressed in parser requirements? [Coverage, Spec §Edge Cases]
- [ ] CHK017 - Are recovery and empty-state requirements defined when an audio file has no lyrics across all 6 resolution stages? [Coverage, Spec §US-2, Spec §FR-009]
- [ ] CHK018 - Is the avoidance of repeated online lookups for verified-empty or instrumental tracks documented with timestamp check rules (`LyricsLastCheckedUtc`)? [Resilience, Spec §FR-009, Spec §Edge Cases]
- [ ] CHK019 - Are cancellation requirements specified for in-flight online lyrics requests when the user skips or changes tracks? [Resilience, Plan §Slice 2, Quickstart §Scenario 4]

---

## 5. UI Interaction, Playback Synchronization & User Scenarios

- [ ] CHK020 - Are visual provenance indicator requirements (clear badges for "Embutida", "Arquivo .lrc", "LRCLIB", etc.) explicitly defined for the Lyrics View? [Completeness, Spec §FR-006]
- [ ] CHK021 - Are interaction requirements specified for plain/unsynchronized lyrics (manual scrolling enabled, click-to-seek disabled, `[Não Sincronizada]` indicator displayed)? [Completeness, Spec §FR-007, Clarifications §Session 2026-09-23]
- [ ] CHK022 - Are UI empty-state requirements distinct between verified instrumental tracks (`♫ Faixa Instrumental`) and tracks with missing lyrics? [Clarity, Spec §FR-009, Plan §Slice 3]
- [ ] CHK023 - Are live offset calibration controls (`+`, `-`, `Zerar`) and real-time active offset display (`Offset: +X ms`) specified in the UI requirements? [Completeness, Spec §FR-010, Data Model §3]
- [ ] CHK024 - Is the confirmation flow (Flyout/TeachingTip notification) specified when the user triggers the manual "Exportar como .lrc" action? [Coverage, Plan §Slice 3, Quickstart §Scenario 2]
- [ ] CHK025 - Are romanization display requirements (optional secondary text line beneath the main lyric) preserved without regression? [Coverage, Data Model §1.4, Spec §Architecture Limits]

---

## Notes

- Mark items `[x]` only after reviewer confirms the requirements-quality criterion is satisfied.
- Leave items unchecked (`[ ]`) when they still require clarification, correction, or reviewer evaluation.
- `/speckit-implement` reads checklist checkbox state as a gate and must not modify markers.
- `checklists/requirements.md` has a separate built-in lifecycle maintained by `/speckit-specify` and `/speckit-clarify`.
