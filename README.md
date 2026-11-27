# Vaikunth Office

*Yogah karmasu kaushalam: Excellence in every action.* (Bhagavad Gita 2.50)

One ASP.NET Core 10 MVC website with five apps inside it. They share one sign-in, one database and one set of controls,
laid out the same way as ShankhNaad (DtoLibrary → ServiceLibrary → Web, stored procedures in `database/`).

| Project | What it is |
|---|---|
| `src/Vaikunth.Office.Web` | **The website you start** (http://localhost:5100). Sign-in, landing page, People, Clients. Set this as the only startup project. |
| `src/Vaikunth.AksharaPages` | **Akshara Pages** (Word) editor, at `/pages/editor`. Razor class library. |
| `src/Vaikunth.HrishikeshaWorkbook` | **Hrishikesha Workbook** (Excel) editor, at `/workbook/editor`. Razor class library. |
| `src/Vaikunth.VishwaroopaSlides` | **Vishwaroopa Slides** (PowerPoint) editor, at `/slides/editor`. Razor class library. |
| `src/Vaikunth.ShashvataReader` | **Shashvata Reader** (PDFs), at `/reader/editor`. Razor class library; pdf.js 3.11.174 in `wwwroot/lib/pdfjs`. |
| `src/Vaikunth.DhruvaPlanner` | **Dhruva Planner** (projects), at `/planner/editor`. Razor class library; Excel download via ClosedXML. |
| `src/Vaikunth.DtoLibrary` | Plain data classes (users, documents, sheet cells, slides). No dependencies. |
| `src/Vaikunth.ServiceLibrary` | All data access: `SqlDb` (ADO.NET, stored procedures only), user, client and document services, the file store, plus in-memory `Mock*` services. |
| `src/Vaikunth.UI` | Razor class library: tag helpers, layouts, sign-in page, landing page, People, Clients, suite CSS/JS, app icons. |
| `database/` | `Vaikunth_Update.sql`: the one script that creates or updates the whole database (all `Vaikunth_` objects). |

## Run it

```
dotnet build VaikunthOffice.sln
dotnet run --project src/Vaikunth.Office.Web
```

In Visual Studio set **Vaikunth.Office.Web** as the single startup project. One browser tab opens on the sign-in page; after
signing in you land on the Vaikunth Office home page. Clicking Word, Excel or PowerPoint (a tile, "+ New", a Blank button
or a recent file) opens that app **in its own new browser tab**, so all three can be open side by side.

With `ConnectionStrings:Default` empty the site runs in **mock mode**: sample clients and files in memory,
password `vaikunth123` (`Mock:Password`):
- `superadmin@vaikunth.local` → the super admin's **Clients** page (`/admin`).
- any other email → an admin of the sample client "Acme Corp".

For SQL Server: create an empty database, run `database/Vaikunth_Update.sql` (safe to run again after every update), put the
connection string in `src/Vaikunth.Office.Web/appsettings.json`, open `/setup` once to create the super admin, then add clients on `/admin`.

## The whole flow

1. **Super admin** (`/admin` → New client): company name, short name, plan, **Licenses (number of users)**, which apps,
   and the client's first admin (name + email). Saved in `Vaikunth_Tenants` and `Vaikunth_Users`.
2. The **client admin** gets a welcome email with a link to set their password (valid 7 days, works once).
3. The client admin opens **People** (`/team`) → Add people: name, work email, role (Member or Admin), **department,
   job title and joining date**. All of it is stored in `Vaikunth_Users`. Each person uses one license; when all are in
   use, adding is refused until the super admin raises Licenses (Clients → Edit) or someone is switched off.
   Edit on a row corrects name, department, job title or joining date.
4. Each person gets an email with their own set-password link, then signs in.
5. After sign-in they see the home page and open Word, Excel or PowerPoint, one tab each.
6. In each editor: **File → Save on the server** (also autosaves) keeps the file in the storage folder on the server,
   with only its name, who made it, dates and IsDeleted in `Vaikunth_Documents`. **File → Download as Word / Excel /
   PowerPoint** (or the Download button in the top bar) saves a `.docx`, `.xlsx` or `.pptx` copy on the person's computer.

### Download formats

| App | File | How it's made |
|---|---|---|
| Akshara Pages | `.docx` | On the server with `HtmlToOpenXml` (headings, lists, tables, bold/italic, colours, pasted pictures; A4 or Letter). |
| Hrishikesha Workbook | `.xlsx` | On the server with `ClosedXML`: every sheet, values, live formulas (`=SUM(B2:B6)`), bold/italic, fills, colours, alignment, ₹ / % / number formats. |
| Vishwaroopa Slides | `.pptx` | In the browser with PptxGenJS 4.0.1 (MIT, in `wwwroot/lib/pptxgenjs`, loaded only when someone downloads): 16:9 slides, theme colours, text boxes with bullets, shapes, pictures, speaker notes. |

The download uses what is on screen, saved or not, and never changes the copy on the server.

## Who is who

| Role | Where | Can do |
|---|---|---|
| Super admin (`Vaikunth_Users.TenantId` NULL) | `/admin` | Create clients with their first admin, choose plan, licenses (users), storage and which of the five apps they get, switch a client off |
| Client admin (`TenantAdmin`) | home `/`, People `/team` | Add people (name, email, role, department, job title, joining date), resend invites, reset passwords, switch people off; use the apps |
| Member | home `/` | Use the apps in the client's plan |

## Where everything is saved

| What | Where |
|---|---|
| Clients, plans, users, passwords (hashed), profile photos, password links | SQL Server (`Vaikunth_*` tables) |
| The list of files: name, created by / date, updated by / date, deleted, size | SQL Server, `Vaikunth_Documents` |
| The content of each Word, Excel and PowerPoint file | **A file on the server**, in the folder `Storage:RootPath` |

Set the folder in `src/Vaikunth.Office.Web/appsettings.json`:

```json
"Storage": { "RootPath": "D:\\VaikunthFiles", "MaxFileMb": 200 }
```

Leave `RootPath` empty to use `App_Data\VaikunthFiles` inside the folder where the site is hosted, or give a local folder
or a share such as `\\fileserver\VaikunthFiles` (the app pool account needs read/write rights on it).

Inside the folder:

| App | File |
|---|---|
| Akshara Pages (Word) | `{client id}/Pages/{file id}.json` (the page's HTML and page size) |
| Hrishikesha Workbook (Excel) | `{client id}/Workbook/{file id}.json` (sheets → cells, `=formula`, bold, fill, number format) |
| Vishwaroopa Slides (PowerPoint) | `{client id}/Slides/{file id}.json` (theme, slides → boxes, pictures, speaker notes) |

The JSON shapes are the classes in `Vaikunth.DtoLibrary/DocumentDtos.cs`. A save writes a temporary file first and swaps it
in only after the database has accepted it (rights and storage quota), so a failed save never leaves half a file.
Deleting a file sets `IsDeleted = 1` (with `DeletedBy`, `DeletedUtc`) and keeps the file on disk.
`MaxFileMb` is the largest one save may be; `web.config` in Vaikunth.Office.Web lets IIS accept that size too.

Back up both the database **and** the storage folder.

Upgrading a database made with an earlier version (file content inside the table): `Vaikunth_Update.sql` moves the
content to `Vaikunth_DocumentsToMove`, and the site writes the files to the storage folder when it next starts.

## My account (everyone, the super admin too)

Profile menu → **My account** changes your own name, email (the one you sign in with) and password. Every change needs
your current password. With a database, the super admin is created on `/setup` and changes their email and password here.
In mock mode the super admin starts as `Mock:SuperAdminEmail` / `Mock:Password` from appsettings.json; changes made in
My account last until the app restarts. **Profile photo** in the same menu stores a small photo in the database.

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

Fill the `Smtp` section of `src/Vaikunth.Office.Web/appsettings.json` (Host, Port, EnableSsl, UserName, Password, FromAddress, FromName, optional OutboxPath),
and set `Suite:PublicBaseUrl` to the public address so links in emails point to the right site.
While `Smtp:Host` is empty, emails are written as `.html` files to the outbox folder
(default `%TEMP%/vaikunth-office-outbox`) and in Development you can read them at **/dev/outbox**.

Mock people (password `vaikunth123`): superadmin@vaikunth.local; Acme Corp admin priya.sharma@acme.com,
members arjun.nair@acme.com, fatima.khan@acme.com (admin), rohan.das@acme.com, meera.iyer@acme.com (invited, no password yet),
kabir.singh@acme.com (switched off).

## The super admin screen (/admin)
Laid out like ShankhNaad, in glass: a side rail (Clients), a header card with the theme button, the clock and your menu
(About, My account, Profile photo, Maximize, Theme Light/Dark, Log out), then a title bar with the toolbar:
Refresh, New client, Edit, Delete, Record payment, Let in again, Export (CSV for Excel), Print, Pause / Resume and Email admin link.
Under it: key figures (clients, users, storage, money received this month, still to come) and three tabs.
- **Clients**: a grid with Filters (status, plan, admin), search, Columns, sorting and paging. Pick a row to open its
  details on the right: usage, plan, apps, files, admin, and Change plan.
- **Payments**: the payment rules, a Billed vs Received chart, "Who has paid" (one row per client) and "Money received".
- **Plans and limits**: one card per plan (licenses, storage, price, apps) with Save.
- Delete works only on a paused client and asks you to type its short name. It removes its people, the list of files
  in the database and its folder on the server.
- Pages: Views/Admin/Index.cshtml, Views/Shared/_AdminLayout.cshtml; scripts js/workspace.js and js/admin.js; styles css/workspace.css.

## Billing (super admin)
- **The bill is worked out for you**: licenses × the plan's price per user, every month. A plan priced ₹0 is free:
  its clients are never billed, never shown as late and never blocked.
- **Payment rules** (Payments tab): the due day (1st to 28th of every month), how many days after the due day a client
  is blocked, and whether blocking is automatic.
- **States**: Paid, Due (before the due day), Overdue (after the due day, shown in amber on the client for the grace days),
  Blocked (grace days over and Auto-block on), On hold, Free, Not started.
- **Record payment**: pick the client, the month it pays for, the amount, the date, how it was paid (Bank transfer, UPI,
  Cheque, Cash, Card), a reference and a note. A month is paid when what was received covers that month's bill.
  A blocked client that pays everything it owes is let in again at once.
- **Let in again**: lets a blocked client sign in until a date you pick, even before it pays. Take it off to block again.
- Blocked people see a clear message at sign-in, and anyone already signed in is signed out. Tenant admins see a banner
  on their home page when the bill is due within 7 days, overdue or on hold.
- The check runs when the admin page opens, after every money action, and every hour in the background (BillingSweepService).
- Database: Tenants.BillingFrom / BillingHoldUntil / BillingBlocked, Vaikunth_BillingSettings, Vaikunth_Payments.
  When an existing database is upgraded, its clients are billed from the 1st of next month, so nobody is blocked by surprise.
  Change "Billing starts" in Edit client to bill from an earlier month.

## Shashvata Reader (PDFs)
- **Open PDF** (or drop a PDF on the page), up to 100 MB. It is saved on the server in `{client}/Reader/{file}.json`
  with the PDF inside (base64), so it counts toward the client's storage.
- Pages drawn by pdf.js, with thumbnails, page number box, zoom (Fit width, Whole page, 50 to 200%), rotate and Night reading.
- **Find** searches the text of every page and highlights the matches. Text can be selected and copied.
- **Notes** (four colours) and **bookmarks** per page, saved with the file, so everyone who opens it sees them.
- **Download** gives back the original PDF.
- Opening another PDF into a file that already has one makes a new Reader file, so notes never end up on the wrong PDF.

## Dhruva Planner (projects)
- One file = one project: name, start and end dates, a description, board columns and tasks.
- Each task: title, status (column), priority, owner, start, due, progress, milestone, notes.
- **Board**: drag cards between columns; rename a column by typing on it; add or remove (empty) columns.
- **List**: sortable table; tick a task to mark it done.
- **Timeline**: days or weeks, today line, weekends shaded; drag a bar to move a task, drag its right edge to change the due date.
- Summary strip: tasks, % done, overdue, due in 7 days, next milestone. Filter by owner, find a task, hide done.
- **Download / Excel** makes an .xlsx with a Tasks sheet (filter, frozen header) and a Summary sheet with live formulas.

## Upgrading a database from three apps to five
Run `database/Vaikunth_Update.sql` again. The first time, plans and clients that had all three apps (7) get the two new
apps too (31), the Documents check allows AppKind 4 (Reader) and 5 (Planner), and the defaults become 31. A plan or client
that had only some apps keeps exactly those; turn the new apps on in Plans and limits or Edit client. Running it again changes nothing.

## Tag helpers (Vaikunth.UI/TagHelpers)

`vk-app-icon`, `vk-app-tile`, `vk-input`, `vk-checkbox`, `vk-verse`, `vk-app-bar`,
`vk-ribbon` / `vk-ribbon-tab` / `vk-ribbon-group` / `vk-ribbon-btn` / `vk-ribbon-select` / `vk-ribbon-color` / `vk-ribbon-sep`,
`vk-dialog`, `vk-sheet-grid`, `vk-status-bar`. Ribbon buttons render `data-cmd`; each app's script handles the commands.

ShankhNaad's controls are in too (TagHelpers/Modern, wwwroot/js/web-control, wwwroot/css/web-control, lib/modern-controls),
in Vaikunth colours, light and dark (css/Theme/vaikunth-controls.css): `modern-table` (grid with filters, search,
columns, sorting, paging, badges), `modern-input`, `modern-dropdown-with-label`, `modern-datepicker-with-label`,
`modern-month-year-picker`, `modern-radio-group`, `modern-checkbox` and `modern-column-chart`. The super admin screen uses them everywhere.

## What the editors do today (mock, basic features)

- **Pages**: fonts, sizes, bold/italic/underline/strike, colours, highlight, lists, indent, alignment, heading styles, tables, links, pictures, date, find, word count, page size and margins, zoom, autosave.
- **Workbook**: cell editing, formula bar, `+ - * /`, `SUM AVERAGE MIN MAX COUNT ROUND` over ranges, number formats (₹, %, number), bold/italic/fill/colour/alignment, AutoSum, sort, insert/delete row, copy/paste, column chart, multiple sheets, selection sum/average in the status bar.
- **Slides**: thumbnails, new/duplicate/delete slide, layouts, four themes, text boxes and shapes you can move and resize, text formatting, pictures, speaker notes, full-screen slide show.
