# Vaikunth Office

*Yogah karmasu kaushalam: Excellence in every action.* (Bhagavad Gita 2.50)

Three ASP.NET Core 10 MVC apps that share one sign-in, one database and one set of controls,
laid out the same way as ShankhNaad (DtoLibrary → ServiceLibrary → Web, stored procedures in `database/`).

| Project | What it is |
|---|---|
| `src/Vaikunth.AksharaPages.Web` | **Akshara Pages** (Word). http://localhost:5101 |
| `src/Vaikunth.HrishikeshaWorkbook.Web` | **Hrishikesha Workbook** (Excel). http://localhost:5102 |
| `src/Vaikunth.VishwaroopaSlides.Web` | **Vishwaroopa Slides** (PowerPoint). http://localhost:5103 |
| `src/Vaikunth.DtoLibrary` | Plain data classes (users, documents, sheet cells, slides). No dependencies. |
| `src/Vaikunth.ServiceLibrary` | All data access: `SqlDb` (ADO.NET, stored procedures only), `UserService`, `DocumentService`, plus in-memory `Mock*` services. |
| `src/Vaikunth.UI` | Razor class library shared by the three apps: tag helpers, layouts, sign-in page, launcher, suite CSS/JS, app icons. |
| `database/` | `Vaikunth_Update.sql`: the one script that creates or updates the whole database (all `Vaikunth_` objects). |

## Run it

```
dotnet build VaikunthOffice.sln
dotnet run --project src/Vaikunth.AksharaPages.Web
dotnet run --project src/Vaikunth.HrishikeshaWorkbook.Web
dotnet run --project src/Vaikunth.VishwaroopaSlides.Web
```

With `ConnectionStrings:Default` empty the apps run in **mock mode**: sample clients and files in memory (each app
keeps its own copy), password `vaikunth123` (`Mock:Password`):
- `superadmin@vaikunth.local` → the super admin's **Clients** page (`/admin`).
- any other email → an admin of the sample client "Acme Corp".

For SQL Server: create an empty database, run `database/Vaikunth_Update.sql` (safe to run again after every update), put the same
connection string in all three apps' `appsettings.json`, open `/setup` once to create the super admin, then add clients
on `/admin`. Signing in once works in all three apps (shared cookie + shared data-protection keys).

## Who is who

| Role | Where | Can do |
|---|---|---|
| Super admin (`Vaikunth_Users.TenantId` NULL) | `/admin` in any app | Create clients with their first admin, choose plan, user/storage limits and which of the three apps they get, switch a client off |
| Client admin (`TenantAdmin`) | launcher `/` | Use the apps in the client's plan |
| Member | launcher `/` | Use the apps in the client's plan |

## Where everything is saved

| What | Where |
|---|---|
| Clients, plans, users, passwords (hashed), profile photos, password links | SQL Server (`Vaikunth_*` tables) |
| The list of files: name, created by / date, updated by / date, deleted, size | SQL Server, `Vaikunth_Documents` |
| The content of each Word, Excel and PowerPoint file | **A file on the server**, in the folder `Storage:RootPath` |

Set the folder in each app's `appsettings.json`:

```json
"Storage": { "RootPath": "D:\\VaikunthFiles", "MaxFileMb": 200 }
```

Leave `RootPath` empty to use `App_Data\VaikunthFiles` inside the folder where the app is hosted. Each app only opens its
own kind of file, so separate folders work, but if you want all files in one place give the three apps the same folder
(a local folder or a share such as `\\fileserver\VaikunthFiles`; the app pool account needs read/write rights on it).

Inside the folder:

| App | File |
|---|---|
| Akshara Pages (Word) | `{client id}/Pages/{file id}.json` (the page's HTML and page size) |
| Hrishikesha Workbook (Excel) | `{client id}/Workbook/{file id}.json` (sheets → cells, `=formula`, bold, fill, number format) |
| Vishwaroopa Slides (PowerPoint) | `{client id}/Slides/{file id}.json` (theme, slides → boxes, pictures, speaker notes) |

The JSON shapes are the classes in `Vaikunth.DtoLibrary/DocumentDtos.cs`. A save writes a temporary file first and swaps it
in only after the database has accepted it (rights and storage quota), so a failed save never leaves half a file.
Deleting a file sets `IsDeleted = 1` (with `DeletedBy`, `DeletedUtc`) and keeps the file on disk.
`MaxFileMb` is the largest one save may be; `web.config` in each app lets IIS accept that size too.

Back up both the database **and** the storage folder.

Upgrading a database made with an earlier version (file content inside the table): `Vaikunth_Update.sql` moves the
content to `Vaikunth_DocumentsToMove`, and each app writes its files to the storage folder when it next starts.

## Invites, passwords and email

1. **Super admin → client admin.** On `/admin`, *New client* asks for the admin's name and email and offers two choices:
   *Email a set-password link* (default) or *Set a temporary password*. With the link, the admin shows as **Admin invited**
   until they set a password; *Resend* sends a fresh link.
2. **Client admin → office members.** The client admin's **People** page (`/team`) invites each member by name and email,
   with role (Member or Admin) and the apps they get. Each member gets a set-password link. The admin can also resend an
   invite, send a reset link, switch someone off, or change their role.
3. **Set password.** `/account/set-password?token=…` checks the link, shows the password rules, saves the password,
   and sends the person to sign in with their email filled in.
4. **Forgot password.** `/account/forgot` sends a reset link. It answers the same way whether or not the email exists.

Links: invite links last 7 days, reset links 2 hours. Each link works once, a new link cancels older ones, and one link
per person per minute. Only a SHA-256 hash of the token is stored (`Vaikunth_PasswordLinks`).

### Email setup

Fill the `Smtp` section of each app's `appsettings.json` (Host, Port, EnableSsl, UserName, Password, FromAddress, FromName, optional OutboxPath),
and set `Suite:PublicBaseUrl` to the public address so links in emails point to the right site.
While `Smtp:Host` is empty, emails are written as `.html` files to the outbox folder
(default `%TEMP%/vaikunth-office-outbox`) and in Development you can read them at **/dev/outbox**.

Mock people (password `vaikunth123`): superadmin@vaikunth.local; Acme Corp admin priya.sharma@acme.com,
members arjun.nair@acme.com, fatima.khan@acme.com (admin), rohan.das@acme.com, meera.iyer@acme.com (invited, no password yet),
kabir.singh@acme.com (switched off).

## Tag helpers (Vaikunth.UI/TagHelpers)

`vk-app-icon`, `vk-app-tile`, `vk-input`, `vk-checkbox`, `vk-verse`, `vk-app-bar`,
`vk-ribbon` / `vk-ribbon-tab` / `vk-ribbon-group` / `vk-ribbon-btn` / `vk-ribbon-select` / `vk-ribbon-color` / `vk-ribbon-sep`,
`vk-dialog`, `vk-sheet-grid`, `vk-status-bar`. Ribbon buttons render `data-cmd`; each app's script handles the commands.

## What the editors do today (mock, basic features)

- **Pages**: fonts, sizes, bold/italic/underline/strike, colours, highlight, lists, indent, alignment, heading styles, tables, links, pictures, date, find, word count, page size and margins, zoom, autosave.
- **Workbook**: cell editing, formula bar, `+ - * /`, `SUM AVERAGE MIN MAX COUNT ROUND` over ranges, number formats (₹, %, number), bold/italic/fill/colour/alignment, AutoSum, sort, insert/delete row, copy/paste, column chart, multiple sheets, selection sum/average in the status bar.
- **Slides**: thumbnails, new/duplicate/delete slide, layouts, four themes, text boxes and shapes you can move and resize, text formatting, pictures, speaker notes, full-screen slide show.
