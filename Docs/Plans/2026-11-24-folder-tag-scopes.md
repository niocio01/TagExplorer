# Folder tag scopes (virtual tags) — plan

Date: 2026-11-24. Status: **draft, waiting for Nico's approval.**

## Goal
A tag on a folder can reach other items. The items it reaches show it as a *virtual* tag. There are three
assignment kinds (the UI calls them "kind"; in code they are `ApplyScope` values):

| UI name | Scope | The tag applies to |
|---|---|---|
| Only this item | `Self` (0) | the item itself (today's behaviour) |
| This folder and everything in it | `SelfAndAllDescendants` (2) | the folder and all files and folders below it (1a) |
| Each direct subfolder | `DirectChildren` (3, new) | only the folders directly inside it, not the folder itself (1b) |
| Each subfolder gets its own tag (1c) | Kind `AutoDirectChildrenAsChildTag` | see below |

`SelfAndDirectDescendants` (1) stays in the enum but isn't offered in the UI.

### Decisions (Nico, 2026-11-24)
- Both 1a and 1b, as separate kinds.
- A plain drop creates "Only this item". **Shift+drop** onto a folder opens a small menu to pick the kind.
  Right-clicking an applied tag in the details pane lets you change its kind.
- Virtual tags look different: an outlined badge (tag colour as border and text, no fill), and in the details pane
  a dimmed chip with a "from <folder>" tooltip. This is easy to restyle later.
- A virtual tag can't be removed from a single item. Its chip offers "Go to <folder>" and "Remove from <folder>"
  (which asks for confirmation and shows how many items are affected).
- Virtual tags count in tag filters (include and exclude).
- Inherited tags are resolved when they're needed and cached, the same way the other caches work.

### 1b reaches the subfolders only, not their contents (approved)
In the example, "Project" on `Projects\` marks `project1\`, `project2\` and so on as *projects*. A file inside
`project1\` is not itself a project, so it doesn't get the tag. That way the "Project" filter lists exactly the
project folders. Saying "this file belongs to project1" needs a separate tag per project. That is what the
removed `AutoDirectChildrenAsChildTag` / `AutoRuleParentTagId` design was for: an automatic child tag named after
each subfolder, inherited by everything in it. That is 1c below.
Only folders are tagged, not files sitting directly in `Projects\` (a readme there isn't a project).

### 1c: each subfolder gets its own child tag (naive version, needs OK)
The rule sits on a folder (Kind `AutoDirectChildrenAsChildTag`, `TagId` null, `AutoRuleParentTagId` = the dropped
tag, e.g. "Project"). It combines 1b and 1a: for each direct subfolder `S` of the folder,
- `S` gets the parent tag "Project" virtually (like 1b), and
- `S` and everything in it get the child tag "S" virtually (like 1a). "S" is a real tag with Parent = "Project",
  so it works in filters, badges and the Tags tab like any other tag.

Naive rules (overrides come later):
- Child tags are found by (ParentId, name, case-insensitive) or created: when the rule is created, and at every
  cache build for new subfolders. They get the parent's colour and icon, no short code, and description
  "Auto: <folder path>".
- A renamed subfolder gets a new tag at the next build; the old tag stays (no rename tracking yet).
- Removing the rule asks whether to delete its auto-created child tags too (only those that aren't used by any
  other assignment); otherwise they stay as normal tags.
- The filter panel lists child tags like any other tag. With many projects it gets long; grouping or collapsing
  under the parent tag comes later.
- Later (not now): virtual overrides such as excluding a subfolder, renaming the tag without the folder, or
  marking a subfolder as "not a project".

## Acceptance (all checked in the GUI and with SQL; screenshots in D:\AI\logs\cua\)
1. A plain drop on a folder creates a Self assignment (no regression).
2. Shift+drop on a folder shows the kind menu. Picking a kind saves one assignment with that Scope; cancelling
   (Esc or a click elsewhere) saves nothing. Shift+drop on a file creates a plain assignment.
3. 1a: every file and folder below shows the outlined badge; the folder itself shows a filled badge.
4. 1b: direct subfolders show the outlined badge; the folder itself, files directly in it, and anything deeper
   show nothing.
5. The tag filter (include and exclude, plus combined with file types and recursive search) treats virtual tags
   like direct ones. Counts are checked against an SQL/`find` calculation.
6. Details pane: a virtual chip is dimmed, its tooltip says "from <folder>", and its right-click menu offers
   Go to / Remove from. Remove asks first and then clears every virtual badge.
7. Right-clicking a direct chip on a folder lets you change the kind; the badges update without a restart.
8. Deleting a tag removes its virtual badges too (fix #18 still holds).
8b. 1c: Shift+drop "Project" on a folder and pick "Each subfolder gets its own tag". Every direct subfolder shows
   "Project" + its own child tag; everything inside shows only the child tag. The child tags exist in the DB with
   ParentId = Project. Filtering by a child tag lists exactly that subfolder's tree. A new subfolder gets its tag
   after a rebuild/restart. Removing the rule asks about deleting the child tags, and both answers work.
9. The cache rebuild/startup time on 05_Programming doesn't get noticeably worse (time before and after).
10. Build: 0 errors, no new warnings.

## Out of scope
Virtual overrides for 1c (see above), excluding a single item from a virtual tag, tag groups (#20), and any
change to how tags look apart from the virtual style.

## Tasks
1. **Model and DB.** `Models/TagAssignment.cs`: add `ApplyScope.DirectChildren = 3`, bring back
   `int? AutoRuleParentTagId` (it was in `e153646`, removed in `bee63f8`), and add `TagApplication.SourcePath`
   (= `Assignment.TargetPath`). The existing check constraints in `AppDbContext` already describe 1c correctly,
   so they stay. New migration `AddedAutoRuleParentTagId`: re-adds the column and its index, and creates both
   check constraints (PostgreSQL dropped them together with the column). Review it by hand, back up, then
   `dotnet ef database update`.
2. **Resolving virtual tags.** `Services/DataCachingService.cs`:
   - an index `_scopedAssignmentsByPath` (folder path → assignments with Scope ≠ Self);
   - `GetTagApplicationsForPath(path)` = direct applications + ones inherited by walking up the parent paths,
     with each result cached in `_inheritedApplicationsByPath`;
   - the cache is cleared on any assignment change, tag removal or rebuild.
   - `GetCandidates`: the items for a tag = direct items ∪ items below 1a folders (`GetItemsBelow`) ∪ the
     direct subfolders of 1b folders (`ItemsByParentPath`). This applies to both required and disallowed tags.
   - `RemoveTagApplication` on a virtual application is refused (it returns false). Removing goes through the
     source assignment.
   - Pattern file: the existing `_tagApplicationsByPath` / `GetItemsBelow` code in the same file.
3. **Assignment service.** `Services/TagAssignmentService.cs`: `AssignManualTag(item, tag, scope)` checks the
   scope (folders only for ≠ Self). Add `ChangeScope(assignment, scope)` and `RemoveAssignment(assignment)`, which
   removes the direct application and all virtual ones. This uses the unused `IsDescendantOrSelf` helper; drop
   `GetRelativeDepth` and `AddTagId` if they stay unused.
3b. **1c rule.** New `Services/AutoChildTagService.cs` (or a section in `TagAssignmentService`):
   `CreateChildTagRule(folder, parentTag)`, `EnsureChildTags(rule)` (create or find child tags, save them, add
   them to the tag cache with `UpsertTag`), and `RemoveRule(rule, deleteChildTags)`. `DataCachingService.ApplyAssignment`
   handles the `AutoDirectChildrenAsChildTag` case (the existing `TODO`): it resolves the virtual applications for
   the parent tag (subfolders) and the child tags (subfolder trees) through the same inherited-applications
   cache as task 2. `CompleteBuild` calls `EnsureChildTags` for every rule. Pattern: `TagsOverview_VM` create
   path for new tags.
4. **Drop with Shift.** `Views/FileList_V.xaml(.cs)`, `ViewModels/FileList_VM.cs`, and the details-pane drop in
   `Views/TagListDroppable_V` + `ItemDetails_VM`: read `DragEventArgs.KeyStates` for Shift. For a folder target,
   show a `ContextMenu` with the four kinds at the drop point. Pattern: the existing right-click filter menu
   in the file list.
5. **Details pane.** `Views/ItemDetails_V.xaml`, `ViewModels/ItemDetails_VM.cs`, `Models/Tag.cs`
   (`AppliedTag` gets `IsVirtual` and `SourcePath`):
   - direct chip menu: Remove (as now) + a "Kind" submenu with the current kind checked (folders only);
   - virtual chip menu: Go to <folder> / Remove from <folder> (with confirmation).
6. **Badge style.** `Views/CompactTagToken_V.xaml`: a DataTrigger on `IsVirtual` (transparent fill, coloured
   border and text). `ViewModels/FileList_VM.cs`: fix `.OrderBy(!IsVirtual).OrderBy(Name)` → `.ThenBy`, so
   direct tags come first.
7. **Verification.** Run the acceptance list above in the GUI (cua-driver), with SQL checks. Clean the DB
   afterwards. Update `D:\AI\logs\cua\TagExplorer_GUI_Test_Findings.md`.

There's no test project in the repo. The logic in task 2 (the resolver and GetCandidates) is checked with the
existing headless harness `%TEMP%\te_harness` against a temp folder tree, plus the GUI run.

## Global constraints
- Repo rules: `.editorconfig`, file-scoped namespaces, CommunityToolkit MVVM attributes as in the existing VMs,
  UTF-8 BOM and CRLF are kept.
- DB: back up before the first run (`pg_dump`). No destructive SQL apart from deleting the test rows the test
  created itself. The migration is reviewed by hand before `database update`.
- No commits or branch switches by agents; Nico commits. Branch: `fix/search-filter-cache` or a new
  `feature/folder-tag-scopes` (Nico decides).
- Build: `dotnet build TagExplorer.csproj -c Debug`, one build per terminal call.

## Risks
- The Shift key state during a drop is only available in `DragEventArgs.KeyStates`, so the menu has to open
  after the drop (fine). Simulated drags can't hold Shift reliably, so Shift+drop needs a hand test.
- `GetItemsBelow` scans all parent paths once per 1a assignment for each filter run. That's fine for a few
  dozen folder tags; if it gets slow, cache the item set per assignment.
