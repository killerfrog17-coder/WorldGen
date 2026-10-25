# Selected-body eye review

Eye appearance needs a separate review from body selection and rig checks. A
front camera alone can hide a misplaced, buried or incorrectly angled eye.

The eye pass captures the selected sculpt without added facial pieces, plus the
previous painted result, from both sides and the front. Each socket receives an
independently inspected centre and opening size. `EYE_SOCKET_GUIDES.json` stores
those curated picks, their exact camera matrices and the selected geometry SHA.
Do not reuse the coordinates on a different candidate or regenerated body.

`fit-fauna-reviewed-eyes.py` fits those rays to the actual surface and rim. It does
not mirror the other eye or force equal world height. A declared open socket can
derive its centre depth from the surrounding rim; ordinary missed centre rays
fail instead of silently attaching an eye to an arbitrary nearby surface.

`finish-selected-fauna-face.py` builds a modest corneal cap, a shaped upper/lower
bare rim and inner corner, with the surrounding transition baked onto the body
atlas. Sparse upper lashes and brow hairs are limited to suitable mammals. Fish,
reptiles, birds and tiny arthropods keep their separate feature policies. Iris
pigment and pupils must follow the animal family rather than a shared goat pupil.
The existing body geometry, skin, rest skeleton and source actions are preserved.

Run `revise-fauna-eyes.py --workers 2` for the finite, resumable batch. One exclusive
lock owns the batch; each worker writes a different animal. Its receipt binds the
review parameters, implementation signature and exact native file. Cached folder
existence is insufficient. The progress writer retries brief Windows reader locks
without cancelling asset work.

`check-fauna-eye-batch.py --watch` consumes finished matching revisions. It samples
the added pieces through the retained animation clips and refreshes body/alternate
coat views without changing native files. These are attachment and preservation
checks, not contact, collision or natural-gait approval.

Build the result sheets with `build-fauna-eye-result-sheets.py`; actually inspect
all three views before running `record-fauna-eye-visual-checks.py --pages ...`.
That recorder rejects stale native files, changed images and pending new picks.
The owner still supplies appearance approval. Do not confuse an agent inspection
or technical pass with owner acceptance or full game readiness.

After the current results pass, refresh the existing private phone review with
`build-fauna-phone-overviews.py` and `build-fauna-phone-eyes.py`. The latter includes
front/right/left views and before/revised switching while retaining the same animal
numbers. Use the existing Sites project and its source/publishing workflow; keep
its owner-only audience. `check-fauna-phone-eyes.mjs` exercises the mobile controls,
search, navigation wrap, image switching and layout.

The accepted deer and the five simple ambient-fauna groups are kept separately.
Do not replace the accepted deer or force elaborate mammal-style eyes onto tiny
particle-like insects. Generated models, packed maps, renders and native proofs
remain local artifacts; Git stores source and the curated socket parameters.
