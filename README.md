# Vaikunth Desk

*Yogah karmasu kaushalam: Excellence in every action.* (Bhagavad Gita 2.50)

One ASP.NET Core 10 MVC website with seven apps inside it (it was called Vaikunth Office; the code names, database objects and the `VaikunthOffice.sln` keep the old name). They share one sign-in, one database and one set of controls,
laid out the same way as ShankhNaad (DtoLibrary → ServiceLibrary → Web, stored procedures in `database/`).

| Project | What it is |
|---|---|
| `src/Vaikunth.Office.Web` | **The website you start** (http://localhost:5100). Sign-in, landing page, People, Clients. Set this as the only startup project. |
| `src/Vaikunth.AksharaPages` | **Akshara Pages** (Word) editor, at `/pages/editor`. Razor class library. |
| `src/Vaikunth.HrishikeshaWorkbook` | **Hrishikesha Workbook** (Excel) editor, at `/workbook/editor`. Razor class library. |
| `src/Vaikunth.VishwaroopaSlides` | **Vishwaroopa Slides** (PowerPoint) editor, at `/slides/editor`. Razor class library. |
| `src/Vaikunth.ShashvataReader` | **Shashvata Reader** (PDFs), at `/reader/editor`. Razor class library; pdf.js 3.11.174 in `wwwroot/lib/pdfjs`. |
| `src/Vaikunth.DhruvaPlanner` | **Dhruva Planner** (projects), at `/planner/editor`. Razor class library; Excel download via ClosedXML. |
| `src/Vaikunth.VaijayantiDays` | **Vaijayanti Days** (calendar), at `/days`. Razor class library; lives in the workspace with the side rail. |
| `src/Vaikunth.SudarshanaInbox` | **Sudarshana Inbox** (mail), at `/inbox`. Razor class library; lives in the workspace with the side rail. |
| `src/Vaikunth.DtoLibrary` | Plain data classes (users, documents, sheet cells, slides). No dependencies. |
| `src/Vaikunth.ServiceLibrary` | All data access: `SqlDb` (ADO.NET, stored procedures only), user, client and document services, the file store, plus in-memory `Mock*` services. |
| `src/Vaikunth.UI` | Razor class library: tag helpers, layouts, sign-in page, landing page, People, Clients, suite CSS/JS, app icons. |
| `database/` | `Vaikunth_Update.sql`: the one script that creates or updates the whole database (all `Vaikunth_` objects). |

## Sign-in page
The sign-in page shows all seven apps and their colours (the PDF crimson included): the colour circles drift slowly, the
window has a turning colour edge, the Sign in button has a moving gradient and four small cards float around it. Motion stops
when the computer asks for reduced motion.

## Run it

```
dotnet build VaikunthOffice.sln
dotnet run --project src/Vaikunth.Office.Web
```

In Visual Studio set **Vaikunth.Office.Web** as the single startup project. One browser tab opens on the sign-in page; after
signing in you land on the Vaikunth Desk home page. Clicking Word, Excel or PowerPoint (a tile, "+ New", a Blank button
or a recent file) opens that app **in its own new browser tab**, so all three can be open side by side.

With `ConnectionStrings:Default` empty the site runs in **mock mode**: sample clients and files in memory,
password `vaikunth123` (`Mock:Password`):
- `superadmin@vaikunth.local` → the super admin's **Clients** page (`/admin`).
- any other email → an admin of the sample client "Acme Corp".

For SQL Server: create an empty database, run `database/Vaikunth_Update.sql` (safe to run again after every update), put the
connection string in `src/Vaikunth.Office.Web/appsettings.json`, open `/setup` once to create the super admin, then add clients on `/admin`.

## Your settings and passwords
`src/Vaikunth.Office.Web/appsettings.json` comes with CloudWheel's own settings (Azure SQL server, smartoffice.cloudwheel.info,
App_Data/files, the aspire.herosite.pro mail server on port 465). The two passwords in it are `XXX` placeholders.
Put the real ones once in **`appsettings.Local.json`** next to it (copy `appsettings.Local.sample.json`). That file is read
last and wins, and it is never in the zip, so a new version never overwrites your passwords. Environment variables
(`ConnectionStrings__Default`, `Smtp__Password`) still win over both, if you prefer them on the server.

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
When you run the site on your own machine (localhost, 127.0.0.1), email links use the local address instead, so a link
from a test email opens your local copy and not the live site.
While `Smtp:Host` is empty, emails are written as `.html` files to the outbox folder
(default `%TEMP%/vaikunth-office-outbox`) and in Development you can read them at **/dev/outbox**.

Mock people (password `vaikunth123`): superadmin@vaikunth.local; Acme Corp admin priya.sharma@acme.com,
members arjun.nair@acme.com, fatima.khan@acme.com (admin), rohan.das@acme.com, meera.iyer@acme.com (invited, no password yet),
kabir.singh@acme.com (switched off).

## Home (/)
Everyone lands on the same full-screen workspace as People and the super admin page, in light and dark: a hero with the
greeting (from the person's own clock), the verse and one-click new files, the seven apps circling the suite logo,
app cards (Open / New, locked when not in the plan), key figures (edited today, recent files, starred, apps, events this week, unread mail) and a
Recent files grid (modern-table: search, filters, columns, paging). Pick a file and use the bar above the list:
Open (or double-click), Download (Word .docx, Excel .xlsx, Planner .xlsx and PDF are made on the server at
/{app}/editor/download?id=; Slides opens its editor, which downloads the .pptx at once), Rename, Star / Unstar and
Delete (asks first; the file is kept in the database so an admin can bring it back). Routes: POST /files/rename,
/files/delete, /files/star. The side rail's highlight slides from the page you came from; the app cards have a slow
light running round them and icons that turn slowly in 3D (all motion stops when the computer asks for reduced motion).
File: Views/Home/Index.cshtml; styles in css/workspace.css (Home section).

## People page (/team, client admins)
The same full-screen workspace as the super admin page: a side rail (Apps, People), the header, a toolbar (Refresh,
Add person, Edit, Send link, Make admin / member, Switch off / on, Export, Print), key figures (people, licenses,
waiting to join, switched off, storage), a people grid with photos or initials, and a details pane for the selected person.
The Add and Edit windows use the modern-input, modern-dropdown-with-label and modern-datepicker-with-label tag helpers.
Saves go through AJAX with toasts, and changing a role or switching someone off asks first in a message box.
Files: Views/Team/Index.cshtml, js/team.js.

## The super admin screen (/admin)
Laid out like ShankhNaad, in glass: a side rail (Clients), a header card with the theme button, the clock and your menu
(About, My account, Profile photo, Maximize, Theme Light/Dark, Log out), then a title bar with the toolbar:
Refresh, New client, Edit, Delete, Record payment, Let in again, Export (CSV for Excel), Print, Pause / Resume and Email admin link.
Under it: key figures (clients, users, storage, money received this month, still to come) and three tabs.
- **Clients**: a grid with Filters (status, plan, admin), search, Columns, sorting and paging. Pick a row to open its
  details on the right: usage, plan, apps, files, admin, and Change plan.
- **Payments**: the payment rules, a Billed vs Received chart, "Who has paid" (one row per client) and "Money received".
- **Edit client** also corrects the client admin's name and email. If the admin hasn't signed in yet, a new welcome
  email goes to the corrected address at once and the old link stops working.
- When an email can't be sent, the message says why (password refused, server not reachable, SSL/port mismatch,
  sender not allowed), so the Smtp settings can be fixed.
- **Plans and limits**: one card per plan (licenses, storage, price, apps) with Save.
- Delete works only on a paused client. A ShankhNaad-style message box lists what will be lost and asks you to type
  its short name. It removes its people, the list of files in the database and its folder on the server.
  Deleting a payment asks in the same message box.
- **Messages** are toasts at the top right (as in ShankhNaad): animated, they close by themselves (errors stay longer),
  hovering keeps them open and × closes them. Scripts: js/web-control/toast.js and messagebox.js.
- **No page reloads.** Saving, Refresh, the windows and the toolbar actions send the form with AJAX; the server answers
  with the message, then only the page content (a partial view, header `X-Partial: 1`) is fetched and put in place.
  The open tab and selected row stay. F5 still loads the whole page. Script: js/ajax-panel.js (VKPanel).
- Pages: Views/Admin/Index.cshtml, Views/Shared/_AdminLayout.cshtml; scripts js/workspace.js and js/admin.js; styles css/workspace.css.

## Billing (super admin)
- **The bill is worked out for you**: licenses × the plan's price per user, every month. A plan priced ₹0 is free:
  its clients are never billed, never shown as late and never blocked.
- **Payment rules** (Payments tab): the due day (1st to 28th of every month), how many days after the due day a client
  is blocked, and whether blocking is automatic. Pick **Default rules** in the client list to set them for every client,
  or pick a client and turn on **Own rules** to give it its own due day, grace days and auto-block. Turn it off to put
  the client back on the defaults. The "Who has paid" grid shows each client's rule (for example "5th · 7 days · own").
  Picking a row in that grid opens its rules. Database: Tenants.DueDay / GraceDays / AutoBlock (empty = default).
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

## Vaijayanti Days (calendar, /days)
- Month, week, day and agenda views; a mini month, "Coming up" and a colour key on the left. Keys: T today, M/W/D/A views, N new, arrows move.
- Click a day or an hour to add an event there. Each event: title, start and end (half-hour steps) or all day, place or link, notes, colour, and people of the same company.
- Invited people see it in their calendar and get "Invitation: ..." in Sudarshana Inbox (only people added since the last save). They answer Yes, Maybe or No; the organiser sees who is going.
- Only the organiser can change or delete an event. Times are stored in UTC and shown in each browser's own time; an all-day event covers whole days.
- Tables `Vaikunth_Events`, `Vaikunth_EventPeople`; procedures `Vaikunth_Event_*`. Service: `ICalendarService` (SQL and mock).

## Sudarshana Inbox (mail, /inbox)
- Folders (Inbox, Starred, Sent, Drafts, Trash with counts), the message list (search, All / Unread) and a reading pane. The open message is shown in a sandboxed frame, and bodies are cleaned of scripts and event handlers when saved.
- New email, Reply, Reply all, Forward, Star, Mark unread, Delete (to Trash; in Trash, for good after a question), Put back, Empty Trash, Print, drafts.
- To / Cc suggest the company's people as you type. Their addresses are delivered inside Vaikunth Desk at once; any other address gets a real email through the Smtp settings (or the outbox while Smtp:Host is empty).
- Each person has their own copy (folder, read, starred). New mail is looked for every minute; the rail shows the unread count on every page.
- Keys: C new, R reply, A reply all, F forward, S star, U unread, Delete, J/K next and back, Ctrl+Enter sends.
- Tables `Vaikunth_Mail`, `Vaikunth_MailBox`; procedures `Vaikunth_Mail_*`. Service: `IMailService` (SQL and mock).

## Upgrading a database to seven apps
Run `database/Vaikunth_Update.sql` again. The first time, plans and clients that had all five apps (31) get the calendar and
mail too (127) and the defaults become 127; the four new tables and their procedures are added. A plan or client that had only
some apps keeps exactly those; turn the new apps on in Plans and limits or Edit client. Until the script has run, the calendar and
mail pages show a note instead of failing, and the home page shows 0 for them.

## Upgrading a database from three apps to five
Run `database/Vaikunth_Update.sql` again. The first time, plans and clients that had all three apps (7) get the two new
apps too (31), the Documents check allows AppKind 4 (Reader) and 5 (Planner), and the defaults become 31. A plan or client
that had only some apps keeps exactly those; turn the new apps on in Plans and limits or Edit client. Running it again changes nothing.

## Moving between pages without reloading (Apps, Calendar, Inbox, People, Clients)
- A click on a side-rail item (or the brand, or the Calendar and Inbox cards on Home) fetches only the page's content
  (`X-Partial: 1`, rendered with `Views/Shared/_PanelLayout.cshtml`) and puts it inside `<main>`. The rail, header,
  theme, fonts, jQuery and the controls stay loaded. Code: `navigation` part of `Vaikunth.UI/wwwroot/js/ajax-panel.js`.
- A page's styles are added to `<head>` once and its scripts run once; a link hovered for a moment is fetched ahead
  (kept 20 seconds; any save clears it). Back and Forward work. F5, Ctrl+click and the editors (own tabs) load normally.
- Pages that can be opened again this way: home.js, team.js, admin.js, days.js and inbox.js only act when their
  page is on screen, and keep their document-wide listeners to one.
- Super admin and People: the header, toolbar and figures stay put, only the part under them (`.vk-ws-scroll`) scrolls.
- Bundles go out with Brotli or gzip (scripts, styles, SVG only) and are cached for a year by their `?v=hash`.

## Bundles (one request for all styles, one for all scripts)
All CSS and JS are joined into bundles (Vaikunth.UI/Infrastructure/Bundles.cs), served at /bundles/{name}?v={hash}:
site.css and site.js (every page), workspace.css and workspace.js (Home, People, super admin), one small script per
workspace page (home.js, team.js, admin.js) and per editor one CSS bundle (editor.css + the app's) and one JS bundle
({pages|workbook|slides|reader|planner}.css/.js); the calendar and mail add days.css/js and inbox.css/js to the workspace bundles. Every page loads 4 files; the Reader also loads pdf.js's worker,
which pdf.js starts itself. The shared bundles are fetched once and then come from the browser's cache. Outside Development they are minified (NUglify) and the browser keeps
them for a year; a changed file gives a new hash, so it is fetched again. In a view: `<vk-bundle name="site.css" />`.
To add a file, add its path to `BundleConfig.All`.

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
