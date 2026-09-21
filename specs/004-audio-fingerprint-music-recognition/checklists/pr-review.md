# PR Review Checklist: 004 — Audio Fingerprint & Music Recognition

**Purpose**: Reviewer-owned requirements-quality gate ("Unit Tests for English") for Pull Request review, validating requirement completeness, clarity, consistency, scenario coverage, and non-functional requirements across all vertical slices of Feature 004.  
**Created**: 2026-09-21  
**Feature**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md) | **Tasks**: [tasks.md](../tasks.md)  

**Review Ownership**: This checklist is a reviewer-owned requirements-quality review artifact. Mark an item `[x]` only when the reviewer determines the requirements-quality criterion is satisfied.  
**Marker Semantics**: `[x]` means the criterion has been reviewed and satisfied for requirements quality. It does not mean implementation work is complete.  

---

## 1. Requirement Completeness

- [ ] CHK001 - Are the local FFmpeg chromaprint muxer invocation arguments (`-t 120`, `-f chromaprint`, `-fp_format base64`, `pipe:1`) fully documented in the specification? [Completeness, Spec §FR-001]
- [ ] CHK002 - Does the specification define the exact payload schema and query parameters (`client`, `duration`, `fingerprint`, `meta`) sent to the AcoustID v2 API? [Completeness, Spec §FR-003, Contracts]
- [ ] CHK003 - Are the database schema changes and EF Core entity properties for `Song.AcousticFingerprint` and `Song.AcoustId` comprehensively documented? [Completeness, Spec §FR-001, Data Model]
- [ ] CHK004 - Does the specification explicitly define what happens to suggested tag fields (`Title`, `Artists`, `Album`, `Year`) when a candidate is selected? [Completeness, Spec §FR-009]
- [ ] CHK005 - Are the exact context menu locations in the music library (`SongsView`, `LibraryPage`, `AlbumViewPage`, `PlaylistSongViewPage`) where "Identificar Música via Áudio" must appear explicitly listed? [Completeness, Spec §FR-008]

---

## 2. Requirement Clarity & Measurability

- [ ] CHK006 - Is the rate limit threshold quantified with an exact request count and time window (strictly max 3 requests per second per client)? [Clarity, Spec §FR-004]
- [ ] CHK007 - Is the candidate relevance cutoff threshold quantified with an exact numeric boundary (confidence score $\ge 40\%$)? [Measurability, Spec §FR-005]
- [ ] CHK008 - Is the high-confidence visual highlight boundary quantified with an exact score threshold (confidence score $\ge 80\%$)? [Measurability, Spec §FR-005]
- [ ] CHK009 - Is the maximum number of candidates returned per lookup unambiguously capped (at most 5 candidates)? [Clarity, Spec §FR-005]
- [ ] CHK010 - Is the maximum latency for local fingerprint extraction objectively defined (< 1.5 seconds)? [Measurability, Spec §SC-001]

---

## 3. Requirement Consistency & Architectural Boundaries

- [ ] CHK011 - Does the specification consistently enforce Principle VII (No Silent Tag Writing) by restricting candidate linking to memory and SQLite while forbidding physical file tag writes to disk? [Consistency, Spec §FR-010, Constitution §VII]
- [ ] CHK012 - Does the specification consistently mandate that zero raw audio bytes or PCM samples are ever transmitted over the network (Principle IV)? [Consistency, Spec §SC-002, Constitution §IV]
- [ ] CHK013 - Are the candidate properties in `RecognitionCandidate` aligned consistently with `TrackExternalIds` and `Song` entity properties? [Consistency, Spec §FR-006, Contracts]
- [ ] CHK014 - Is the three-tiered API key resolution hierarchy (User Key in Settings $\to$ ApiKeyService secure storage $\to$ embedded default key) unambiguously ordered without conflicting precedence? [Consistency, Spec §FR-007, Clarifications]

---

## 4. Scenario & Edge Case Coverage

- [ ] CHK015 - Does the specification define the exact behavior and user feedback when an audio recording is shorter than 10 seconds? [Edge Case, Spec §Edge Cases, Spec §FR-002]
- [ ] CHK016 - Is the handling of degenerate audio inputs (pure silence, non-audio files, corrupted headers) specified with deterministic failure states? [Edge Case, Spec §Edge Cases]
- [ ] CHK017 - Are retry policies, exponential backoff delays, and circuit breaker states explicitly defined for HTTP 429 (Too Many Requests) and HTTP 5xx responses? [Exception Flow, Spec §Edge Cases, Spec §SC-003]
- [ ] CHK018 - Does the specification describe the exact system response and UI status when the device is offline or when the AcoustID service is disabled in settings? [Scenario Coverage, Spec §FR-011, US-2]
- [ ] CHK019 - Is the workflow for re-identifying tracks that already possess associated AcoustID or MusicBrainz IDs clearly specified with user confirmation controls? [Scenario Coverage, Spec §FR-006, US-1]
- [ ] CHK020 - Does the specification define the discard workflow when a user rejects all candidate suggestions? [Scenario Coverage, Spec §FR-009, US-1]

---

## 5. Non-Functional Requirements & PR Validation Gates

- [ ] CHK021 - Are asynchronous execution requirements defined to guarantee the UI thread remains completely unblocked during FFmpeg extraction and network lookups? [Performance, Spec §SC-004]
- [ ] CHK022 - Is the in-memory lookup cache bounded and specified to prevent redundant HTTP requests for identical fingerprints? [Efficiency, Spec §FR-012]
- [ ] CHK023 - Are whole-solution compilation and test gates (`dotnet build Resonance.slnx --configuration Release -p:Platform=x64` and `dotnet test --no-build`) documented as mandatory PR acceptance criteria? [Validation, Spec §4, Constitution §III]
- [ ] CHK024 - Does the specification mandate automated unit test coverage for each vertical slice prior to PR merge? [Validation, Spec §3, Constitution §II]

---

## Notes

- Mark items `[x]` only after review confirms the requirement-quality criterion is satisfied.
- Leave items unchecked when they still require clarification, correction, or reviewer evaluation.
- `/speckit-implement` reads checklist checkbox state as a quality gate and must not modify markers.
- Items are numbered sequentially (`CHK001` - `CHK024`) for easy citation during code and requirements review.
