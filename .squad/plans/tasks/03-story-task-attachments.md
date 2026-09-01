# Story 03 — Task Attachments

## Prerequisites

- Story 02 completed: [`02-story-task-assignment-and-comments.md`](02-story-task-assignment-and-comments.md), in this same folder. This story only **edits** files Story 02 creates (`Endpoints/TaskEndpoints.cs`, `Data/AppDbContext.cs`, `Dtos/Tasks/TaskDtos.cs`) and adds new ones alongside them — it does not stand alone. Every reference below to "Story 02's `X`" means the code that story's own task list specifies, since none of these files exist on disk until Story 02 is implemented.
- Reuses `TaskEndpoints.IsPrivileged`, `GetCallerEmployeeAsync`, and `CanViewTaskAsync` (all private static helpers Story 02 task 9 adds to `Endpoints/TaskEndpoints.cs`) — do not re-implement equivalents.

## Story Goal

Let anyone who can already view a task (per Story 02's visibility rule) upload a file to it and download any of its attachments, and let the uploader — or an Admin/Manager/Supervisor — delete one. Concretely:

1. `POST /api/tasks/{id}/attachments` accepts a single file (`multipart/form-data`) and stores it on local disk under a configurable root directory, recording its metadata (`TaskAttachment`) in the database.
2. `GET /api/tasks/{id}/attachments/{attachmentId}` streams the file back — added because "upload" with no way to retrieve the file would be a dead end; the acceptance criteria lists only upload + delete explicitly, but retrieval is implied the same way Story 01 added `POST /api/users` for a capability the spec implied without naming the endpoint.
3. `DELETE /api/tasks/{id}/attachments/{attachmentId}` removes both the database row and the file on disk.
4. `GET /api/tasks/{id}` (Story 02's `GetTaskByIdAsync`) additionally returns attachment metadata alongside comments, for the same reason comments are embedded there — no separate list endpoint was requested.
5. Storage approach: **local disk**, under a path configured in `appsettings.Development.json` — not blob storage. No cloud-storage package is referenced anywhere in `EmpoloyeeManagment.csproj`, no cloud credentials exist in any `appsettings*.json`, and this codebase's established v1 pattern is consistently "the simple, unglamorous option, accepted as fine at this scale" (see `docs/Project_Startup_Plan.md`'s reasoning for synchronous, unbatched API-call logging as the closest precedent). Introducing blob storage would mean adding a new dependency, new config, and new credentials for a v1 system that doesn't otherwise have any — out of proportion to what's asked.

**Not in scope for this story:**
- File-size or content-type restrictions beyond Kestrel's own default request-body-size ceiling — not requested in the acceptance criteria; see Edge Cases.
- Virus scanning or content inspection of uploaded files.
- Editing/replacing an existing attachment — only upload (create) and delete, per the acceptance criteria.

---

## Context — Read These Files First

1. [`EmpoloyeeManagment/EmpoloyeeManagment.csproj`](../../../EmpoloyeeManagment/EmpoloyeeManagment.csproj) — whole file (26 lines). No blob/cloud-storage package (`Azure.Storage.*`, `AWSSDK.*`) is referenced anywhere, and `<ImplicitUsings>enable</ImplicitUsings>` (line 6) plus `net10.0` (line 4) confirm `IFormFile` needs no extra package — it ships in the shared ASP.NET Core framework referenced via `Microsoft.NET.Sdk.Web` (line 1).
2. A repo-wide search for `IFormFile|IWebHostEnvironment|wwwroot|UseStaticFiles|BlobServiceClient` in `EmpoloyeeManagment/` returns no matches — this story is the first file-upload code in the codebase; there is no existing convention to match beyond the general endpoint/DI patterns Story 02 already established.
3. [`EmpoloyeeManagment/appsettings.Development.json`](../../../EmpoloyeeManagment/appsettings.Development.json) — whole file (20 lines). The `ConnectionStrings`/`Jwt`/`Cors` sections (lines 8–19) are the config-section convention the new `Storage` section (task 2) matches — dev-only config, nothing in base `appsettings.json`.
4. [`.gitignore`](../../../.gitignore) — confirmed no `App_Data/`-style entry exists yet (there's a commented-out `#wwwroot/` line only, for a different, unused convention). Task 3 adds a real entry so locally-uploaded files during development aren't committed.
5. `EmpoloyeeManagment/obj/Debug/net10.0/EmpoloyeeManagment.GlobalUsings.g.cs` — `System.IO` (`Path`, `Directory`, `File`) and `Microsoft.AspNetCore.Http`/`Microsoft.AspNetCore.Hosting` (`IWebHostEnvironment`) are all global usings already. `Microsoft.Extensions.Configuration` (`IConfiguration`) too. This story's new handlers and storage-root helper (task 6) need **no new `using` lines** in `Endpoints/TaskEndpoints.cs` for any of this.
6. Story 02's task 4 (`Data/AppDbContext.cs` changes) and task 9 (`Endpoints/TaskEndpoints.cs`, the full file content) — read both in full before starting. This story's task 4 (EF config) follows the exact same `entity.HasOne<T>().WithMany().HasForeignKey(...).OnDelete(...)` shape Story 02 task 4 uses for `TaskItem`/`TaskComment`. This story's task 6 adds routes to the same `MapTaskEndpoints` method body and reuses `IsPrivileged`/`GetCallerEmployeeAsync`/`CanViewTaskAsync` from that file rather than duplicating them.
7. Story 02's task 8 (`Dtos/Tasks/TaskDtos.cs`) — specifically the `TaskDetailResponse(TaskDetailDto Task, List<TaskCommentDto> Comments)` record, which task 5 below extends with a third `Attachments` field.
8. [`README.md`](../../../README.md) lines 58–76 and [`CLAUDE.md`](../../../CLAUDE.md) lines 63–65 — updated by Story 02 task 11 already; this story's task 8 makes a second, small update to the same spots for the three new routes.

---

## Backend Tasks

`No frontend changes required in this repository` — companion UI work is tracked in the separate `employee-management-web` repo.

### 1 — Add the `TaskAttachment` model

**Create file: `EmpoloyeeManagment/Models/TaskAttachment.cs`**

```csharp
namespace EmpoloyeeManagment.Models;

public class TaskAttachment
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public int UploadedByEmployeeId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
```

`FileName` is the original, client-supplied display name. `StoredFileName` is a server-generated, collision-proof, path-traversal-proof name (task 6) — the two are deliberately different columns; never use `FileName` to build a disk path.

### 2 — Add the storage path config

**File: `EmpoloyeeManagment/appsettings.Development.json`**

Add a `Storage` section after the `Cors` block (after line 19):

```json
"Storage": {
  "TaskAttachmentsPath": "App_Data/task-attachments"
}
```

Relative to `IWebHostEnvironment.ContentRootPath` at runtime (task 6) — not `wwwroot`, since these files must only ever be reachable through the authenticated download endpoint, never via static-file serving.

### 3 — Ignore the local upload folder

**File: `.gitignore`**

Add a new entry (anywhere in the file; grouping it near the commented-out `#wwwroot/` line is reasonable):

```
App_Data/
```

### 4 — Register the DbSet and EF configuration

**File: `EmpoloyeeManagment/Data/AppDbContext.cs`**

Add one more DbSet alongside the two Story 02 adds:

```csharp
public DbSet<TaskAttachment> TaskAttachments => Set<TaskAttachment>();
```

Add one more configuration block inside `OnModelCreating`, after Story 02's `TaskComment` block:

```csharp
builder.Entity<TaskAttachment>(entity =>
{
    entity.Property(a => a.FileName).HasMaxLength(260).IsRequired();
    entity.Property(a => a.StoredFileName).HasMaxLength(64).IsRequired();
    entity.Property(a => a.ContentType).HasMaxLength(255).IsRequired();

    entity.HasOne<TaskItem>()
        .WithMany()
        .HasForeignKey(a => a.TaskId)
        .OnDelete(DeleteBehavior.Cascade);

    entity.HasOne<Employee>()
        .WithMany()
        .HasForeignKey(a => a.UploadedByEmployeeId)
        .OnDelete(DeleteBehavior.Restrict);
});
```

`FileName` bounded to 260 (Windows `MAX_PATH`-sized bound for a display name); `StoredFileName` to 64 (a GUID is 36 characters, leaving headroom for an extension); `ContentType` to 255 (generous for a MIME type string). `Cascade` on `TaskId` (deleting a task removes its attachment rows — but not the files on disk; see Edge Cases), `Restrict` on `UploadedByEmployeeId` matching Story 02's convention for FKs to `Employee`.

### 5 — Extend the Tasks DTOs

**File: `EmpoloyeeManagment/Dtos/Tasks/TaskDtos.cs`**

Add one new record:

```csharp
public record TaskAttachmentDto(int Id, string FileName, string ContentType, long SizeBytes, int UploadedByEmployeeId, DateTime UploadedAt);
```

Change the `TaskDetailResponse` record Story 02 added from:

```csharp
public record TaskDetailResponse(TaskDetailDto Task, List<TaskCommentDto> Comments);
```

to:

```csharp
public record TaskDetailResponse(TaskDetailDto Task, List<TaskCommentDto> Comments, List<TaskAttachmentDto> Attachments);
```

### 6 — Add attachment routes to `TaskEndpoints`

**File: `EmpoloyeeManagment/Endpoints/TaskEndpoints.cs`**

Add three more route registrations inside `MapTaskEndpoints`, after the comment routes Story 02 added:

```csharp
group.MapPost("/{id:int}/attachments", UploadAttachmentAsync);
group.MapGet("/{id:int}/attachments/{attachmentId:int}", DownloadAttachmentAsync);
group.MapDelete("/{id:int}/attachments/{attachmentId:int}", DeleteAttachmentAsync);
```

Add the three handlers plus one private storage-path helper, anywhere among the other private methods:

```csharp
private static async Task<Results<Created<TaskAttachmentDto>, NotFound, BadRequest<string>>> UploadAttachmentAsync(
    int id, IFormFile file, ClaimsPrincipal principal, AppDbContext db, IConfiguration configuration, IWebHostEnvironment env)
{
    var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id);
    if (task is null) return TypedResults.NotFound();
    if (!await CanViewTaskAsync(task, principal, db)) return TypedResults.NotFound();

    if (file.Length == 0) return TypedResults.BadRequest("The uploaded file is empty.");

    var caller = await GetCallerEmployeeAsync(principal, db);
    if (caller is null) return TypedResults.NotFound();

    var storedFileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
    var root = GetAttachmentStorageRoot(configuration, env);
    var fullPath = Path.Combine(root, storedFileName);

    await using (var stream = File.Create(fullPath))
    {
        await file.CopyToAsync(stream);
    }

    var attachment = new TaskAttachment
    {
        TaskId = id,
        UploadedByEmployeeId = caller.Id,
        FileName = Path.GetFileName(file.FileName),
        StoredFileName = storedFileName,
        ContentType = file.ContentType,
        SizeBytes = file.Length,
        UploadedAt = DateTime.UtcNow
    };
    db.TaskAttachments.Add(attachment);
    await db.SaveChangesAsync();

    return TypedResults.Created($"/api/tasks/{id}/attachments/{attachment.Id}", ToAttachmentDto(attachment));
}

private static async Task<Results<PhysicalFileHttpResult, NotFound>> DownloadAttachmentAsync(
    int id, int attachmentId, ClaimsPrincipal principal, AppDbContext db, IConfiguration configuration, IWebHostEnvironment env)
{
    var task = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
    if (task is null) return TypedResults.NotFound();
    if (!await CanViewTaskAsync(task, principal, db)) return TypedResults.NotFound();

    var attachment = await db.TaskAttachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == attachmentId && a.TaskId == id);
    if (attachment is null) return TypedResults.NotFound();

    var root = GetAttachmentStorageRoot(configuration, env);
    var fullPath = Path.Combine(root, attachment.StoredFileName);
    if (!File.Exists(fullPath)) return TypedResults.NotFound();

    return TypedResults.PhysicalFile(fullPath, attachment.ContentType, attachment.FileName);
}

private static async Task<Results<NoContent, NotFound, ForbidHttpResult>> DeleteAttachmentAsync(
    int id, int attachmentId, ClaimsPrincipal principal, AppDbContext db, IConfiguration configuration, IWebHostEnvironment env)
{
    var attachment = await db.TaskAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.TaskId == id);
    if (attachment is null) return TypedResults.NotFound();

    var caller = await GetCallerEmployeeAsync(principal, db);
    var isUploader = caller is not null && attachment.UploadedByEmployeeId == caller.Id;
    if (!isUploader && !IsPrivileged(principal)) return TypedResults.Forbid();

    var root = GetAttachmentStorageRoot(configuration, env);
    var fullPath = Path.Combine(root, attachment.StoredFileName);
    if (File.Exists(fullPath)) File.Delete(fullPath);

    db.TaskAttachments.Remove(attachment);
    await db.SaveChangesAsync();

    return TypedResults.NoContent();
}

private static string GetAttachmentStorageRoot(IConfiguration configuration, IWebHostEnvironment env)
{
    var configuredPath = configuration["Storage:TaskAttachmentsPath"] ?? "App_Data/task-attachments";
    var root = Path.IsPathRooted(configuredPath) ? configuredPath : Path.Combine(env.ContentRootPath, configuredPath);
    Directory.CreateDirectory(root);
    return root;
}

private static TaskAttachmentDto ToAttachmentDto(TaskAttachment attachment) => new(
    attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeBytes, attachment.UploadedByEmployeeId, attachment.UploadedAt);
```

`IFormFile file` as a plain parameter is bound automatically from a `multipart/form-data` request body by minimal APIs (.NET 7+) — no `[FromForm]` attribute needed, and no `.DisableAntiforgery()` call needed either, since this project has no antiforgery middleware registered anywhere (confirmed by Context item 2's search). `Path.GetFileName(file.FileName)` (in `UploadAttachmentAsync`) strips any directory components the client-supplied filename might contain **before** it's stored as `FileName` — defense in depth against path-traversal-style filenames, even though `StoredFileName` (the only value ever used to build a real disk path) is always a fresh server-generated GUID and never derived from client input.

Delete permission mirrors comments' author-or-Admin rule but widens it to `IsPrivileged` (Admin/Manager/Supervisor, Story 02's helper) rather than Admin alone — an attachment is a task-owned artifact a Supervisor should be able to clean up, unlike a comment, which is closer to a personal statement.

### 7 — Create the migration

```bash
dotnet ef migrations add AddTaskAttachments --project EmpoloyeeManagment
dotnet ef database update --project EmpoloyeeManagment
```

Confirm the generated migration only adds the `TaskAttachments` table.

### 8 — Update docs

**File: `README.md`** — add the three new routes to the API overview table.

**File: `CLAUDE.md`** — extend the "Known v1 gaps" paragraph (or the architecture section, if a fuller note is warranted) to mention attachments are stored on local disk under a configurable path, not blob storage, and why (see Story Goal point 5).

---

## Edge Cases & Failure Modes

- **Path traversal via the client-supplied filename.** Never trust `file.FileName` as a disk path — the actual on-disk name (`StoredFileName`) is always a fresh `Guid.NewGuid()`, and the display name (`FileName`) is stripped to its filename component (`Path.GetFileName(...)`, task 6) even though it's never used to build a path. This is a deliberate security precaution, not incidental.
- **Storage root doesn't exist yet.** `GetAttachmentStorageRoot` calls `Directory.CreateDirectory(root)` unconditionally (task 6) — a no-op if the directory already exists, so first-upload-ever and every subsequent call behave identically with no separate provisioning step required.
- **Attachment row exists but the file is missing on disk** (e.g. manually deleted, or the storage path config changed after upload). `DownloadAttachmentAsync` checks `File.Exists(fullPath)` and returns `404` rather than letting `PhysicalFile` throw — a missing file is treated the same as a missing attachment from the caller's point of view.
- **Deleting a task cascades attachment *rows* but not attachment *files*.** `TaskAttachment.TaskId`'s `OnDelete(DeleteBehavior.Cascade)` (task 4) only removes the database row if a `TaskItem` is ever deleted — there is no `DELETE /api/tasks/{id}` endpoint anywhere in this codebase (Story 02 deliberately doesn't add one), so this is currently unreachable in practice, but if a future story adds task deletion, it must also delete the now-orphaned files on disk; this story does not add that cleanup since there's nothing to trigger it yet.
- **No explicit file-size or content-type limit.** This story adds no `RequestSizeLimit`/allow-list validation — only Kestrel's own default request-body-size ceiling applies. Not a gap introduced here; simply not requested by the acceptance criteria, and inventing an arbitrary limit wasn't asked for either.
- **Concurrent uploads to the same task.** Each upload gets its own `Guid`-named file — no collision is possible between two simultaneous uploads, even to the same task, even with identical original filenames.
- **`ContentType` is trusted from the client.** `file.ContentType` (the browser/client-declared MIME type) is stored and later served back verbatim as the `Content-Type` header on download (task 6) — this codebase does no server-side content sniffing/verification anywhere, and adding it wasn't requested; noted here so it isn't mistaken for an oversight if it comes up later.

---

## Test Plan

Manual Swagger walkthrough, following the same pattern as Story 02's Test Plan (this repo has no automated tests — `CLAUDE.md` line 22).

1. **Migration applies cleanly** — `dotnet ef database update --project EmpoloyeeManagment`; confirm `TaskAttachments` exists and no other table changed.
2. **Upload + visibility** — as the assignee Employee of an existing task, `POST /api/tasks/{id}/attachments` with a small test file; confirm `201` with a `TaskAttachmentDto` body. As a *different* Employee (not the assignee), retry; confirm `404`.
3. **Storage on disk** — confirm a new file actually appears under the configured `Storage:TaskAttachmentsPath` directory, named as a GUID (not the original filename).
4. **Download round-trip** — `GET /api/tasks/{id}/attachments/{attachmentId}`; confirm the response body byte-for-byte matches the uploaded file and `Content-Disposition`/filename reflects the *original* `FileName`, not the stored GUID name.
5. **Delete permissions** — as the uploader, `DELETE /api/tasks/{id}/attachments/{attachmentId}`; confirm `204` and that the file is gone from disk. Upload a second file as the assigning Supervisor, then delete it as the assignee Employee (not the uploader); confirm `403` unless the Employee is also the uploader. Confirm an Admin can delete any attachment regardless of uploader.
6. **Missing file on disk** — manually delete a stored file from the filesystem without going through the API, then `GET` its attachment id; confirm `404` rather than a `500`.
7. **`GET /api/tasks/{id}` includes attachments** — confirm the `Attachments` array in `TaskDetailResponse` reflects current state after upload/delete.
8. **Regression** — re-run Story 02's Test Plan end to end to confirm comments/lifecycle endpoints are unaffected.

---

## Migration / Rollback

One additive EF Core migration (`AddTaskAttachments`, task 7) adding the `TaskAttachments` table only.

**Rollback.** `dotnet ef database update AddTasksAndComments --project EmpoloyeeManagment` (Story 02's migration) reverts the schema, then delete this story's migration files and revert the code changes (`Data/AppDbContext.cs`, `Dtos/Tasks/TaskDtos.cs`, `Endpoints/TaskEndpoints.cs`, `appsettings.Development.json`, `.gitignore`, the new `Models/TaskAttachment.cs`). Files already written to the configured storage path are not automatically cleaned up by a schema rollback — delete the directory manually if a full revert is needed.

**Half-applied upload.** `UploadAttachmentAsync` writes the file to disk *before* inserting the database row (task 6). If the process crashes between the two, an orphaned file with no matching row can be left on disk — harmless (it's simply never referenced or served, since lookups always go through the database row first) but not automatically cleaned up; no retention/cleanup policy is added in this story, matching this codebase's already-accepted "no batching/cleanup for v1" stance on `ApiCallLogs` (`CLAUDE.md`, logging section).

---

## Verification Steps

1. **Backend builds:** `dotnet build` from the repo root (or `EmpoloyeeManagment/`).
2. **Database check:** `dotnet ef database update --project EmpoloyeeManagment` — confirm only this story's migration applies.
3. **Run:** `dotnet run --project EmpoloyeeManagment`, open `/swagger`, and execute the full Test Plan above (steps 1–8).
4. **Regression:** confirm Story 02's full Test Plan still passes.

---

## Done Criteria

- [ ] `TaskAttachment` model added; `TaskAttachments` DbSet and EF configuration added; one additive migration applied.
- [ ] `Storage:TaskAttachmentsPath` config added to `appsettings.Development.json`; storage root created on demand, not committed to git.
- [ ] `POST /api/tasks/{id}/attachments`, `GET /api/tasks/{id}/attachments/{attachmentId}`, `DELETE /api/tasks/{id}/attachments/{attachmentId}` implemented with the visibility/delete-permission rules above.
- [ ] Uploaded files are never stored or served under a client-controlled name — only a server-generated GUID is ever used as a disk path.
- [ ] `GET /api/tasks/{id}` (`TaskDetailResponse`) includes attachment metadata.
- [ ] `README.md` and `CLAUDE.md` updated for the three new routes and the local-disk storage decision.
- [ ] Full manual Test Plan (steps 1–8) passes against a local run.
