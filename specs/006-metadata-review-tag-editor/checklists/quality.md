# Requirements Quality Checklist: Feature 006 — Metadata Review, Tag Editor & File Update

**Purpose**: Reviewer-owned requirements-quality gate ("Unit Tests for English") validating completeness, clarity, measurability, and scenario coverage for Feature 006  
**Created**: 2026-09-22  
**Feature**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md)  

**Review Ownership**: This checklist is a reviewer-owned requirements-quality review artifact. Mark an item `[x]` only when the reviewer determines the requirements-quality criterion is satisfied.  
**Marker Semantics**: `[x]` means the criterion has been reviewed and satisfied for requirements quality. It does not mean implementation work is complete.  

---

## 1. Requirement Completeness

- [ ] CHK001 - Are all target metadata fields (Title, Artist, Album, AlbumArtist, Year, TrackNumber, TrackTotal, DiscNumber, DiscTotal, Genre, Comment, CoverArt) explicitly enumerated in the data model and requirements? [Completeness, Spec §Requirements, Data Model §2.4]
- [ ] CHK002 - Are the initial selection states (`IsSelected`) for diff fields clearly specified based on proposal status (`NewValue` and `Updated` enabled by default, `Unchanged` disabled)? [Completeness, Spec §FR-002, Data Model §2.1]
- [ ] CHK003 - Are embedded cover art specifications (ID3 APIC / FLAC Picture block extraction, resolution handling, and MIME type) clearly documented? [Completeness, Spec §FR-008, Research §1]
- [ ] CHK004 - Is the formal conversion contract between an `EnrichmentProposal` (Feature 005) and an executable `TagWritePlan` specified without ambiguity? [Completeness, Contracts §ITagDiffService, Plan §Slice 1]
- [ ] CHK005 - Are the entry points for launching the `TagEditorDialog` (Track Inspector "Revisar e Gravar" button and track context menu "Editar Tags") explicitly specified? [Completeness, Spec §FR-010, Plan §Slice 3]

---

## 2. Requirement Clarity & Measurability

- [ ] CHK006 - Is the audio stream integrity requirement objectively measurable (0 bytes altered in audio/PCM payload)? [Measurability, Spec §SC-001, Plan §Technical Context]
- [ ] CHK007 - Is the local database persistence synchronization threshold objectively measurable (< 50ms from confirmation to SQLite commit)? [Measurability, Spec §SC-002]
- [ ] CHK008 - Is the Golden Rule ("Zero automatic tag writes without explicit user confirmation") formulated with unambiguous pass/fail criteria? [Measurability, Spec §SC-003, Constitution §VII]
- [ ] CHK009 - Is the single-track scope boundary explicitly documented, leaving batch tag editing formally declared as out-of-scope for a future feature? [Clarity, Spec §FR-007, Clarifications §Session 2026-09-22]
- [ ] CHK010 - Is the format and delimiter for multi-valued fields (such as semicolon `;` for genres and artists) quantified consistently? [Clarity, Spec §Slice 1, Research §1]

---

## 3. Requirement Consistency & Architectural Boundaries

- [ ] CHK011 - Are requirements consistent with Constitution Principle VII regarding the strict progression `Review -> Diff -> Explicit confirmation -> Apply`? [Consistency, Constitution §VII, Spec §2]
- [ ] CHK012 - Do the responsibilities between UI presentation (`TagEditorDialog`), diff calculation (`ITagDiffService`), and atomic file writing (`ITagWriterService`) maintain strict architectural boundaries without leaking file I/O into UI layers? [Consistency, Constitution §VII, Plan §Project Structure]
- [ ] CHK013 - Is field-level provenance tracking (`LocalTag`, `MusicBrainz`, `CoverArtArchive`, `UserOverride`) consistently specified across diff generation and plan building? [Consistency, Spec §Key Entities, Contracts §TagReviewDtos]
- [ ] CHK014 - Does the tag writing engine strictly reuse existing ATL.NET capabilities (`ATL.Track`) without introducing parallel audio tag libraries? [Consistency, Constitution §I, Research §1]

---

## 4. Atomic Write, Resilience & File Lock Recovery

- [ ] CHK015 - Are the four discrete stages of atomic writing (temporary file in same directory, tag writing, post-write ATL verification, safe replacement) documented with explicit failure boundaries? [Resilience, Spec §FR-004, Research §2]
- [ ] CHK016 - Are rollback and cleanup requirements explicitly specified when a write failure or exception occurs (immediate deletion of `.tmp` and preservation of original)? [Recovery, Spec §Edge Cases, Research §2]
- [ ] CHK017 - Are the coordination steps for releasing the file handle with `IMusicPlaybackService` defined when the target file is actively playing? [Resilience, Spec §FR-009, Clarifications §Session 2026-09-22]
- [ ] CHK018 - Is the playback position restoration behavior quantified when resuming playback after tag writing (resuming from identical millisecond timestamp)? [Clarity, Spec §FR-009, Spec §Edge Cases]
- [ ] CHK019 - Are the requirements for detecting and temporarily removing the Windows `FileAttributes.ReadOnly` flag during authorized writes clearly defined? [Resilience, Spec §FR-011, Research §4]
- [ ] CHK020 - Is user notification and graceful abort specified when the operating system denies write access due to NTFS/ACL permissions or external locks? [Exception Flow, Spec §FR-011, Spec §Edge Cases]
- [ ] CHK021 - Are requirements documented for verifying sufficient disk space before creating the temporary working copy? [Edge Case, Spec §Edge Cases, Plan §Technical Context]

---

## 5. UI Interaction, Diff Review & User Scenarios

- [ ] CHK022 - Are visual comparison requirements (side-by-side Before vs Proposed display) explicitly defined for the `TagEditorDialog`? [Completeness, Spec §FR-001, Spec §FR-010]
- [ ] CHK023 - Are interaction requirements specified for individual checkbox toggling and batch "Select All / Deselect All"? [Coverage, Spec §FR-002, Spec §US-1]
- [ ] CHK024 - Are requirements specified for allowing manual overrides on individual fields during an online enrichment review prior to saving? [Coverage, Spec §FR-010, Spec §US-2]
- [ ] CHK025 - Are cancel and dismiss requirements specified to ensure zero disk changes occur if the user closes or cancels the dialog? [Coverage, Spec §FR-006, Data Model §3]
- [ ] CHK026 - Are Unicode, emojis, and special character encoding requirements specified for metadata fields in the editor? [Coverage, Spec §Edge Cases]

---

## 6. Non-Functional Requirements & Governance

- [ ] CHK027 - Is 100% offline functionality mandated for manual tag editing without any dependency on external networks? [Governance, Constitution §IV, Plan §Technical Context]
- [ ] CHK028 - Are latency targets defined for diff generation (< 10ms), atomic save (< 100ms for < 50MB files), and catalog sync (< 50ms)? [Performance, Plan §Technical Context, Spec §SC-002]
- [ ] CHK029 - Are mandatory toolchain validation gates (`restore`, `build --warnaserror`, `test`) explicitly defined before any implementation task completion? [Governance, Constitution §III, Spec §4]

---

## Notes

- Mark items `[x]` only after reviewer confirms the requirement-quality criterion is satisfied
- Leave items unchecked when they still require clarification, correction, or reviewer evaluation
- `/speckit-implement` reads checklist checkbox state as a gate and must not modify markers
- Items are numbered sequentially (CHK001 - CHK029) for easy reference in pull request reviews
