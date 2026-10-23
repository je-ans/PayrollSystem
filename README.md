# Payroll System — Setup Guide

Follow this guide to run the app locally with **Visual Studio 2022 or later**.

## Prerequisites

Install these on Windows:

1. [Visual Studio](https://visualstudio.microsoft.com/vs/) **2022 (17.12+) or a newer Visual Studio** (2022, 2025, or Insiders). Community, Professional, or Enterprise all work.
2. During setup (or **Tools → Get Tools and Features…**), enable:
   - **ASP.NET and web development**
   - Individual components: **.NET 8.0 SDK** (minimum) **or any newer SDK** (.NET 9, .NET 10, …). A newer SDK can build this project; you do not need to stay on 8 only.
3. [SQL Server](https://www.microsoft.com/sql-server) Express, Developer, or LocalDB (LocalDB is often installed with Visual Studio)
4. [SQL Server Management Studio](https://learn.microsoft.com/sql/ssms/download-sql-server-management-studio-ssms) (optional, useful for inspecting the database)

The web project currently targets `net8.0`. Visual Studio with an 8+ SDK will restore, build, and run it. If you retarget the project to a newer TFM later, install that SDK as well.

Confirm the SDK: **Help → About Microsoft Visual Studio**, or **File → New → Project** → ASP.NET Core Web App and check that **.NET 8.0 or later** appears in the framework list.

## Clone the repository

Remote: [github.com/je-ans/PayrollSystem](https://github.com/je-ans/PayrollSystem)

| Method | URL / command |
| --- | --- |
| HTTPS | `https://github.com/je-ans/PayrollSystem.git` |
| SSH | `git@github.com:je-ans/PayrollSystem.git` |
| GitHub CLI | `gh repo clone je-ans/PayrollSystem` |

You need permission to the `je-ans/PayrollSystem` GitHub repo. Prefer **HTTPS + Visual Studio** unless you already use SSH keys or `gh`.

### Clone from Visual Studio

1. Open Visual Studio.
2. On the start window, choose **Clone a repository**.  
   If Visual Studio is already open: **File → Clone Repository…**, or open **Git Changes** (**View → Git Changes**) and choose **Clone**.
3. **Repository location**: paste the HTTPS URL:

   `https://github.com/je-ans/PayrollSystem.git`

   Use SSH instead if your GitHub account is set up with a key:

   `git@github.com:je-ans/PayrollSystem.git`
4. **Path**: choose a local folder. Visual Studio will create a `PayrollSystem` subfolder there. Example: `C:\Users\<you>\source\repos\`.
5. Select **Clone**.
6. Sign in if prompted (**GitHub** account). Allow the Git credential manager to store the token.
7. When clone finishes, Visual Studio asks which folder/solution to open. Choose **PayrollSystem.sln**. If that dialog is skipped, use **File → Open → Project/Solution…** and select `PayrollSystem.sln` in the cloned folder.

### Clone with GitHub CLI (optional)

If [GitHub CLI](https://cli.github.com/) is installed and you are signed in (`gh auth login`):

```powershell
cd C:\Users\<you>\source\repos
gh repo clone je-ans/PayrollSystem
```

Then in Visual Studio: **File → Open → Project/Solution…** → `PayrollSystem.sln`.

### After clone

1. Wait for NuGet restore to finish (status bar, lower left). If packages did not restore, right-click the solution in **Solution Explorer** → **Restore NuGet Packages**.
2. In **Solution Explorer**, right-click the **PayrollSystem** project → **Set as Startup Project** (the name is bold when it is the startup project).
3. Confirm Git is connected: **Git Changes** should list the current branch (usually `main` or `master`). Use **Git → Pull** before you start work so you have the latest commits.

### Already have the folder on disk

If a teammate copied the repo or you cloned it earlier:

1. **File → Open → Project/Solution…**
2. Browse to `PayrollSystem.sln` (repo root, next to the `PayrollSystem` project folder).
3. Continue with NuGet restore and **Set as Startup Project** as above.

## Configure SQL Server

1. Create an empty database, or let the app create it on first run via migrations. The default name is `Payroll_System_DB`.
2. In **Solution Explorer**, open `appsettings.json` under the **PayrollSystem** project.
3. Set `ConnectionStrings:Default` to your local instance.

Windows Authentication (typical on a local machine):

```json
"ConnectionStrings": {
  "Default": "Server=YOUR_SERVER_NAME;Database=Payroll_System_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
}
```

SQL Authentication:

```json
"ConnectionStrings": {
  "Default": "Server=YOUR_SERVER_NAME;Database=Payroll_System_DB;User Id=YOUR_USERNAME;Password=YOUR_PASSWORD;MultipleActiveResultSets=true;TrustServerCertificate=True"
}
```

Examples for `YOUR_SERVER_NAME`:

| Setup | Example |
| --- | --- |
| SQL Server Express named instance | `localhost\\SQLEXPRESS` or `MACHINENAME\\SQLEXPRESS` |
| Default instance | `localhost` or `.` |
| LocalDB (often installed with Visual Studio) | `(localdb)\\MSSQLLocalDB` |

Do not commit a machine-specific connection string or SQL passwords.

## Build and run

1. Select the **https** launch profile in the toolbar (next to the green Start button). Alternatives:
   - **http** — `http://localhost:5006`
   - **IIS Express** — `https://localhost:44369` / `http://localhost:60221`
2. Press **F5** (Start Debugging) or **Ctrl+F5** (Start Without Debugging).
3. Visual Studio builds the project, starts Kestrel or IIS Express, and opens the browser.

The **https** profile binds to:

- `https://localhost:7193`
- `http://localhost:5006`

The login page is the default route (`/Account/Index`).

If you get an HTTPS certificate prompt, accept it (or run **Tools → Command Line → Developer Command Prompt** and `dotnet dev-certs https --trust`).

## What happens on first run

Startup (`Program.cs`) will:

1. Apply pending Entity Framework Core migrations (`DbInitializer`).
2. Create the built-in **Admin** role and a bootstrap admin user if they do not exist.
3. Seed sample master data, employees, payroll periods, and a draft payroll run (`SeedData`).

If the connection string is wrong, the process exits during initialization. Open **View → Output**, show output from **Debug**, and look for the SQL exception.

To start from a clean database: drop `Payroll_System_DB` in SSMS (or SQL Server Object Explorer in Visual Studio: **View → SQL Server Object Explorer**), then run the app again with **F5**.

## Seed data and test accounts

Sample data is created on startup by `Data/SeedData.cs`. Full inventory, payroll test steps, and expected pay figures: **[README_SeedData.md](PayrollSystem/Data/README_SeedData.md)**. End-to-end walkthrough: **[Data/PAYROLL_TEST_FLOW.md](PayrollSystem/Data/PAYROLL_TEST_FLOW.md)**.

These accounts are for local testing only. Change them before any shared or production environment.

| Role | Email / username | Password |
| --- | --- | --- |
| Bootstrap Admin (`DbInitializer`) | `admin@gmail.com` (username `admin`) | `Admin@1234` |
| SuperAdmin (seed) | `admin@payroll.com` | `Admin123!` |
| Employee | `john.doe@company.com` | `Employee123!` |
| Employee | `jane.smith@company.com` | `Employee123!` |
| Employee | `mike.johnson@company.com` | `Employee123!` |

Use `admin@payroll.com` / `Admin123!` for day-to-day feature work.

What gets seeded (see [README_SeedData.md](PayrollSystem/Data/README_SeedData.md) for the full list):

- Payroll configuration, day types, departments, designations, salary grades SG 1–SG 33
- SSS, PhilHealth, Pag-IBIG tables and BIR tax brackets
- Shifts, work schedules, employee allowances
- Three payroll periods for the current month (first half, second half, full month), time cards, and one **Draft** payroll run
- Employees: John Doe (`EMP001`, IT, SG 11), Jane Smith (`EMP002`, HR, SG 22), Mike Johnson (`EMP003`, Finance, SG 2)

Attendance records and time entries are **not** seeded; use DTR or manual attendance if you need them.

To re-seed: drop or clear the database, then **F5**. Seeding skips rows that already exist; a fresh database is the reliable reset.

## EF Core migrations

This project uses **Entity Framework Core** with SQL Server. Migration files live in `PayrollSystem/Migrations/`. The NuGet package **Microsoft.EntityFrameworkCore.Tools** is already referenced, so Package Manager Console commands work after restore.

### First run (no extra steps)

You do **not** need to add or apply migrations by hand to get a local database. On startup, `DbInitializer` calls `Database.Migrate()` and applies anything pending, then seed data runs. After **F5**, you should see `Payroll_System_DB` with tables.

If you want to apply migrations yourself before the first run (optional):

1. Save `appsettings.json` with a working connection string.
2. Open **Tools → NuGet Package Manager → Package Manager Console**.
3. Set **Default project** to **PayrollSystem** (dropdown at the top of the console).
4. Run:

```powershell
Update-Database
```

### When you change the model

Do this whenever you add/rename properties, new entities, or change `ApplicationDbContext`:

1. Build the project (**Build → Build Solution**, or **Ctrl+Shift+B**) so the console uses the latest model.
2. **Tools → NuGet Package Manager → Package Manager Console**.
3. **Default project** = **PayrollSystem**.
4. Add a migration. Use a short name that describes the change:

```powershell
Add-Migration AddDutyExchangeRequest
```

Visual Studio creates two files under `Migrations/`:

- `YYYYMMDDHHMMSS_AddDutyExchangeRequest.cs` — `Up` / `Down`
- matching `Designer.cs`

5. Review `Up()` before applying. Then either:
   - Press **F5** and let startup migrate, or
   - Apply now:

```powershell
Update-Database
```

6. Commit the new migration files with your model changes. Teammates get the schema when they pull and run the app.

### Useful Package Manager Console commands

Set **Default project** to **PayrollSystem** before each of these.

| Command | What it does |
| --- | --- |
| `Add-Migration Name` | Scaffold a new migration from the current model vs. last snapshot |
| `Remove-Migration` | Delete the last migration if it has **not** been applied to the database |
| `Update-Database` | Apply all pending migrations to `ConnectionStrings:Default` |
| `Update-Database -Migration Initial` | Roll the database back to a named migration |
| `Get-Migration` | List migrations and whether they are applied |
| `Script-Migration` | Generate a SQL script instead of applying directly |

Roll back the last applied migration (then you can `Remove-Migration` if you still need to rewrite it):

```powershell
Update-Database -Migration <PreviousMigrationName>
Remove-Migration
```

### Fresh database

To rebuild schema from scratch:

1. **View → SQL Server Object Explorer** (or SSMS).
2. Connect to your instance, expand **Databases**, right-click `Payroll_System_DB` → **Delete** (check close existing connections).
3. Press **F5**, or run `Update-Database` in Package Manager Console.

Startup will recreate the database, apply all migrations, then seed.

### Migration troubleshooting

- **`Add-Migration` not recognized** — Restore NuGet, confirm `Microsoft.EntityFrameworkCore.Tools` is in the project, close and reopen Package Manager Console, set **Default project** to **PayrollSystem**.
- **Cannot connect / login failed** — Fix `appsettings.json` first; PMC uses the same `Default` connection string.
- **Pending model changes after Add-Migration** — You edited entities after the last migration. Add another migration; do not edit an already-shared migration by hand.
- **Startup fails on `Migrate()`** — The database is out of sync (partial apply, or someone changed the schema in SSMS). Check **Output → Debug**. Easiest local fix: drop the database and **F5** again.
- **Designer / snapshot conflicts after a pull** — Two people added migrations in parallel. Keep both migration files, but you may need a merge migration: `Add-Migration Merge_<topic>` after resolving the snapshot.

## Project layout

```
Controllers/     MVC controllers
Data/            DbContext, initializer, seed data
Migrations/      EF Core migrations
Models/          Domain entities
Services/        Payroll, attendance, approvals
Views/           Razor views
wwwroot/         Static assets and uploads
Program.cs       App startup
appsettings.json Connection string and file settings
```

Uploads go under `wwwroot/uploads` (`FileSettings:UploadFolder`).

## Troubleshooting

**Cannot connect to SQL Server**  
Confirm the instance name, that SQL Server is running (Windows Services, or the SQL Server Object Explorer), and that `TrustServerCertificate=True` is present for local certs. For Express, use `Server=localhost\\SQLEXPRESS`.

**NuGet restore failed**  
Right-click the solution → **Restore NuGet Packages**. Check **Tools → NuGet Package Manager → Package Manager Settings** that nuget.org is enabled.

**Login fails after a successful start**  
Use the seed SuperAdmin (`admin@payroll.com` / `Admin123!`). The bootstrap account is `admin@gmail.com` / `Admin@1234`.

**Seeding or migrate errors on startup**  
The app rethrows database init failures. Check the **Output** window, drop the database, and press **F5** again. See [README_SeedData.md](PayrollSystem/Data/README_SeedData.md).

**Port already in use**  
Stop the previous debug session (**Shift+F5**). Or change URLs in `Properties/launchSettings.json` (or project **Properties → Debug → General → Open debug launch profiles UI**).

**HTTPS developer certificate**  
Accept the Visual Studio trust prompt, or from **Developer Command Prompt for VS**: `dotnet dev-certs https --trust`.
