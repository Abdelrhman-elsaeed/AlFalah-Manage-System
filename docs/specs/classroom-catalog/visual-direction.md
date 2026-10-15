# Classroom catalogue and teaching assignments — design direction

Original C32 direction: design/prototype only,2026-10-15. C35 now implements its assignment ledger direction in the existing Angular route; [runtime choices and critique](assignments.md), [visual verification](verification/assignments/README.md). The prototype limitations below describe the original C32 artifact.

## Visual plan before implementation

Reuse the platform's actual tokens: forest `#12563a`, action green `#0d6434`, warm page `#f5f4ef`, white `#ffffff`, muted green `#edf7f1`, and restrained gold `#946800`. Tajawal carries interface text; the official paper uses an Arabic print face. Keep alignment RTL, compact metadata, legible counts,44px targets and visible keyboard focus.

The memorable element is the assignment ledger: grade-grouped classroom rows, readable subject columns, teacher names and allocated periods, with a gold edge only on changed cells. Pending cells remain obvious without washing the page in color. A focused teacher picker shows remaining capacity beside the actual decision. The manager's school catalogue and teacher multi-selection share the same grade grouping and classroom identity.

```text
School header                         Prototype / synthetic-data notice
Compact navigation                    Page title / year / export
                                      Grade tabs / search / view switch
                                      Classroom × subject ledger
                                      Pending + changed state / save
```

The secondary teacher view mirrors the paper's organization. Export preview uses a quiet monochrome landscape sheet with merged column headings: teacher, specialization, three grade columns, specialization load, other-subject columns and total load. Screen styling does not replace the school's official document structure.

## Brief review

Rejected an oversized green hero, repeated metric cards and a permanent teacher-card column: they reduce matrix space and repeat the presentation problems reported elsewhere in this platform. Keep the work surface dominant and make capacity a local decision aid. A separate reference-paper preview makes source fidelity inspectable without forcing the working interface to look like a scanned form.

## Prototype limits

Standalone static asset with in-memory synthetic data, no application API/session access, no operational save or permission change. PDF uses browser print for this design phase; the production renderer and migration are specified separately. The prototype is a single proposed direction, not an installed replacement for the existing Angular assignment screen.
