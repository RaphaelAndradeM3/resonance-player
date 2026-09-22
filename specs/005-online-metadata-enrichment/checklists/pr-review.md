# PR Review Checklist: 005 — Online Metadata Enrichment

**Purpose**: Reviewer-owned requirements-quality gate ("Unit Tests for English") for Pull Request review, validating requirement completeness, clarity, consistency, scenario coverage, and non-functional requirements across all vertical slices of Feature 005.  
**Created**: 2026-09-22  
**Feature**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md) | **Tasks**: [tasks.md](../tasks.md)  

**Review Ownership**: This checklist is a reviewer-owned requirements-quality review artifact. Mark an item `[x]` only when the reviewer determines the requirements-quality criterion is satisfied.  
**Marker Semantics**: `[x]` means the criterion has been reviewed and satisfied for requirements quality. It does not mean implementation work is complete.  

---

## 1. Requirement Completeness

- [ ] CHK001 - Are the MusicBrainz Web Service v2 lookup parameters (`inc=releases+artists+media+isrcs+tags&fmt=json`) and query structure fully specified? [Completeness, Spec §FR-001, Contracts §MusicBrainz]
- [ ] CHK002 - Are textual fallback search requirements (Lucene syntax `artist:"..." AND recording:"..."` and result cap) documented for tracks without `MusicBrainzTrackId`? [Completeness, Spec §FR-001, Contracts §MusicBrainz]
- [ ] CHK003 - Are Cover Art Archive resolution paths, image sizing hierarchy (`front-500` falling back to `front-250`), and direct image URI contracts explicitly defined? [Completeness, Spec §FR-003, Spec §FR-010]
- [ ] CHK004 - Are local disk cache storage path (`IPathConfiguration.MetadataCachePath` / `%LocalAppData%/Resonance/MetadataCache/`), JSON envelope format, and automatic 7-day TTL expiration window fully documented? [Completeness, Spec §FR-004, Spec §FR-011]
- [ ] CHK005 - Are all proposed metadata fields (Title, Artist, Album, AlbumArtist, Year, TrackNumber, TotalTracks, DiscNumber, TotalDiscs, Label, ISRC, Genre, CoverArt) comprehensively enumerated in the domain contracts? [Completeness, Spec §Key Entities, Contracts §MetadataEnrichment]
- [ ] CHK006 - Does the specification explicitly define the context menu integration points across the library views (`LibraryPage`, `AlbumViewPage`, `PlaylistSongViewPage`)? [Completeness, Spec §FR-007, Plan §Slice 3]

---

## 2. Requirement Clarity & Measurability

- [ ] CHK007 - Is the MusicBrainz rate limit threshold quantified with an exact request count and time window (strictly max 1 request per second per host)? [Clarity, Spec §FR-002, Spec §SC-001]
- [ ] CHK008 - Is the official `User-Agent` header value explicitly specified as `Resonance/1.0 (+https://github.com/RaphaelAndradeM3/resonance-player)`? [Clarity, Spec §FR-002]
- [ ] CHK009 - Is the cache response time threshold objectively measurable (< 15 milliseconds for cached responses)? [Measurability, Spec §SC-002]
- [ ] CHK010 - Is the canonical release selection heuristic deterministically defined (prioritizing status "Official", type "Album", oldest release date, or exact local album title match)? [Clarity, Spec §FR-009]
- [ ] CHK011 - Is the semantic genre merge rule quantified with an exact delimiter (semicolon `;`) and deduplication logic? [Clarity, Spec §FR-013]
- [ ] CHK012 - Are the default states for field selection checkboxes (`IsSelected`) clearly defined for each status (`NewValue` and `Updated` checked, `Unchanged` and `Conflict` unchecked)? [Clarity, Spec §FR-012, Plan §Slice 2]

---

## 3. Requirement Consistency & Architectural Boundaries

- [ ] CHK013 - Does the specification consistently enforce Principle VII (No Silent Tag Writing) by restricting proposals to memory and disk cache while forbidding physical file tag writes (strictly 0 bytes altered in media files)? [Consistency, Spec §FR-008, Spec §SC-004, Constitution §VII]
- [ ] CHK014 - Does the specification consistently mandate that zero raw audio bytes or personal telemetry are transmitted over the network (Principle IV)? [Consistency, Spec §SC-004, Constitution §IV]
- [ ] CHK015 - Do field proposals consistently require explicit provenance tracking (`LocalTag`, `MusicBrainz`, `CoverArtArchive`) across 100% of suggested fields? [Consistency, Spec §FR-006, Spec §SC-003]
- [ ] CHK016 - Are the domain status classifications (`Unchanged`, `Updated`, `NewValue`, `Conflict`) aligned consistently between `MetadataEnrichmentService` and Track Inspector visual status badges? [Consistency, Spec §Key Entities, Plan §Slice 3]
- [ ] CHK017 - Is the architectural boundary between in-memory proposal generation (Feature 005) and downstream tag writing (Feature 006) preserved without leakage of write dependencies? [Consistency, Spec §1, Spec §FR-008]

---

## 4. Scenario & Edge Case Coverage

- [ ] CHK018 - Does the specification define the exact behavior and user feedback when the device is completely offline or the MusicBrainz service is unreachable? [Scenario Coverage, Spec §US-3, Spec §FR-001]
- [ ] CHK019 - Are recovery flows, circuit breaker states, and exponential backoff delays specified for HTTP 429 (Too Many Requests) and HTTP 503 responses? [Exception Flow, Spec §US-3, Plan §Slice 1]
- [ ] CHK020 - Does the specification define the fallback behavior when a release has no artwork available on the Cover Art Archive (preserving local art without error)? [Edge Case, Spec §Edge Cases, Spec §FR-003]
- [ ] CHK021 - Are requirements specified for multi-disc box sets (mapping disc index, total discs, track index and track count)? [Edge Case, Spec §Edge Cases]
- [ ] CHK022 - Are requirements documented for multi-artist credits (concatenating artist names and joinphrases like "feat." or "with")? [Edge Case, Spec §Edge Cases]
- [ ] CHK023 - Are Unicode and character encoding requirements explicitly defined to prevent corruption of accented, Cyrillic, and CJK text? [Edge Case, Spec §Edge Cases]
- [ ] CHK024 - Are requirements defined for preserving local metadata fields not supported by the remote provider (such as user comments or BPM)? [Coverage, Spec §US-2]

---

## 5. Non-Functional Requirements & PR Validation Gates

- [ ] CHK025 - Are asynchronous execution requirements defined to guarantee the UI thread remains completely unblocked (> 16ms frame budget) and audio playback is uninterrupted during enrichment? [Performance, Spec §US-3, Constitution §VII]
- [ ] CHK026 - Is the local disk cache specified to avoid SQLite database migration complexity and avoid database lock contention? [Architecture, Spec §FR-011]
- [ ] CHK027 - Are whole-solution compilation and test gates in Release mode (`dotnet build Resonance.slnx --configuration Release -p:Platform=x64` with 0 warnings/0 errors and `dotnet test --no-build`) documented as mandatory PR acceptance criteria? [Validation, Spec §4, Constitution §III]
- [ ] CHK028 - Does the specification mandate automated unit test coverage for each vertical slice prior to PR merge? [Validation, Spec §3, Constitution §II]

---

## Notes

- Mark items `[x]` only after review confirms the requirement-quality criterion is satisfied.
- Leave items unchecked when they still require clarification, correction, or reviewer evaluation.
- `/speckit-implement` reads checklist checkbox state as a quality gate and must not modify markers.
- Items are numbered sequentially (`CHK001` – `CHK028`) for easy citation during Pull Request review.
