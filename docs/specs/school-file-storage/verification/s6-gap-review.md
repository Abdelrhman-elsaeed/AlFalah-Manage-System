# S6 gap review — 2026-10-05

Starting HEAD: `29329fd`; S5 implementation: `c30e1cf`. Working tree was clean. Later Drive picker/setup commits are retained. Original Development configuration and keys are excluded from edits.

| Requirement | State at start | S6 change / evidence to collect |
|---|---|---|
| Preview → human matching → review → commit → exceptions | S1 import tables existed; no runtime importer | Extend those tables, bounded JSON/CSV service/repository, scoped controller and RTL page |
| School/year/template identity and source fingerprint | Batch unique key omitted template version | Add template/source version, row keys and retained review/commit provenance |
| Repeat, races, stale review, partial bytes recovery | No S6 workflow | SQL application locks, review digest covering source/mappings/current requirements/members, source immutability, S2 reserved upload recovery |
| Existing S4 mapping | 4/11/36 immutable template and 145 fingerprints | Reuse existing school/year scope; never duplicate or silently change its template/requirements |
| Original bytes | Not supplied in prototype | Separate verified upload, download hash verification, one asset/multiple Draft links; missing originals remain ReferenceOnly/NeedsReview |
| HistoricalImport policy | Protected type denied in evidence/readiness | Keep its denial; verified originals explicitly become SchoolUpload through S2, with import-row provenance; VisitArchive stays excluded |
| Sidebar | Context returned 404 when OFF; shell hid link | Actual live school manager receives Disabled context without storage schema/Google I/O, explaining activation and linking Drive settings; enabled access still requires DB storage permissions |
| Live credentials | Earlier reports recorded pre-consent failure | Read-only observation now succeeds for school 18; school 1 still cannot decrypt credentials. Preserve both outcomes |
| Download audit and revocation | Generic content lacked audit/recheck after provider content acquisition | Recheck current file authorization after I/O, dispose on denial, persist Storage.ContentRead |
| Actual old SQL data | One legacy Approved submission in baseline | COPY_ONLY clone, additive migrations on clone, real backfill and second apply, checksum restore rehearsal |
| Rollout | Four original flags OFF | Concrete school-18 runbook; synthetic live smoke in isolated clone only, no operational cutover/retirement |

Owner acceptance of final parity, original historical bytes, production migration/capacity window and operational cutover approval remain separate gates. Isolated algorithm tests and a synthetic live smoke do not close those gates.
