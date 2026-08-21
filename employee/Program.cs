using System.Security.Claims;
using System.Net;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using CltPlusPlus.Employee;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<RequestRepository>();
builder.Services.AddAntiforgery();
builder.Services.AddHttpClient();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/forbidden";
        options.Cookie.Name = "cltpp.employee";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
TowZoneAdmin.Map(app);
BoloAdmin.Map(app);
ExpressReports.Map(app);
VehicleCameraLookup.Map(app);
DriverVehicleVerification.Map(app);
TrespassAdmin.Map(app);
TrespassIdentityEvidence.Map(app);

var users = LoadUsers(app.Configuration, app.Environment);

app.MapGet("/login", (HttpContext ctx, IAntiforgery anti) =>
{
    if (ctx.User.Identity?.IsAuthenticated == true) return Results.Redirect("/");
    var token = anti.GetAndStoreTokens(ctx).RequestToken!;
    return Html(Page("Employee sign in", $"""
      <main class="login-shell"><section class="login-card">
        <div class="brand">CLT<span>++</span> <small>EMPLOYEE</small></div>
        <h1>Employee sign in</h1><p>Use your authorized employee account to process requests assigned to your department.</p>
        <form method="post" action="/login">
          <input type="hidden" name="__RequestVerificationToken" value="{H(token)}" />
          <label>Username<input name="username" autocomplete="username" required /></label>
          <label>Password<input name="password" type="password" autocomplete="current-password" required /></label>
          <button type="submit">Sign in</button>
        </form>
        {(app.Environment.IsDevelopment() ? "<div class='dev-note'><b>Development demo accounts</b><br>police.demo / ChangeMe!<br>fire.demo / ChangeMe!<br>supervisor.demo / ChangeMe!</div>" : "")}
      </section></main>
    """, null));
});

app.MapPost("/login", async (HttpContext ctx, IAntiforgery anti) =>
{
    await anti.ValidateRequestAsync(ctx);
    var form = await ctx.Request.ReadFormAsync();
    var username = form["username"].ToString().Trim();
    var password = form["password"].ToString();
    var user = users.FirstOrDefault(x => string.Equals(x.Username, username, StringComparison.OrdinalIgnoreCase) && x.Password == password);
    if (user is null) return Html(Page("Sign in failed", "<main class='login-shell'><section class='login-card'><h1>Sign in failed</h1><p>Invalid username or password.</p><a class='button' href='/login'>Try again</a></section></main>", null), 401);

    var claims = new List<Claim> { new(ClaimTypes.Name, user.Username), new(ClaimTypes.Role, user.Role) };
    claims.AddRange(user.Departments.Select(d => new Claim(DepartmentAccess.ClaimType, d)));
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    return Results.Redirect("/");
});

app.MapPost("/logout", async (HttpContext ctx, IAntiforgery anti) =>
{
    await anti.ValidateRequestAsync(ctx);
    await ctx.SignOutAsync();
    return Results.Redirect("/login");
}).RequireAuthorization();

app.MapGet("/", async (HttpContext ctx, RequestRepository repo, IAntiforgery anti, string? status, string? department, string? q) =>
{
    var all = await repo.ListAsync();
    var accessible = all.Where(r => DepartmentAccess.CanAccess(ctx.User, r));
    if (!string.IsNullOrWhiteSpace(status)) accessible = accessible.Where(r => string.Equals(r.Status, status, StringComparison.OrdinalIgnoreCase));
    if (!string.IsNullOrWhiteSpace(department)) accessible = accessible.Where(r => string.Equals(DepartmentAccess.For(r), department, StringComparison.OrdinalIgnoreCase));
    if (!string.IsNullOrWhiteSpace(q)) accessible = accessible.Where(r => $"{r.TrackingNumber} {r.ServiceTitle} {r.Location}".Contains(q, StringComparison.OrdinalIgnoreCase));
    var list = accessible.ToArray();
    var token = anti.GetAndStoreTokens(ctx).RequestToken!;
    var departments = ctx.User.IsInRole("Administrator") || ctx.User.IsInRole("Supervisor")
        ? all.Select(DepartmentAccess.For).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray()
        : DepartmentAccess.UserDepartments(ctx.User);

    var rows = string.Join("", list.Select(r => $"""
      <tr><td><a href="/requests/{U(r.TrackingNumber)}"><b>{H(r.TrackingNumber)}</b></a><small>{H(r.ServiceTitle)}</small></td><td>{H(DepartmentAccess.For(r))}</td><td>{H(r.Location)}</td><td><span class="status">{H(r.Status)}</span></td><td>{H(r.Processing?.AssignedTo ?? "Unassigned")}</td><td>{r.CreatedAt.LocalDateTime:g}</td></tr>
    """));
    var options = string.Join("", departments.Select(d => $"<option value='{H(d)}' {(string.Equals(d,department,StringComparison.OrdinalIgnoreCase)?"selected":"")}>{H(d)}</option>"));
    var isPolice = DepartmentAccess.UserDepartments(ctx.User).Contains("Police", StringComparer.OrdinalIgnoreCase) || ctx.User.IsInRole("Supervisor") || ctx.User.IsInRole("Administrator");
    var policeTools = isPolice ? "<div style='display:flex;gap:.5rem;flex-wrap:wrap'><a class='button' href='/tow-zones'>Tow zones</a><a class='button' href='/bolos'>BOLOs</a><a class='button' href='/express-reports'>Express reports</a><a class='button' href='/vehicle-lookup'>Vehicle camera lookup</a><a class='button' href='/identity-verification'>Driver & registration verification</a><a class='button' href='/trespass-records'>Trespass records</a><a class='button' href='/trespass-identity'>Trespass ID & photo</a></div>" : "";
    var body = $"""
      <main class="wrap"><section class="page-head"><div><span class="eyebrow">WORK QUEUE</span><h1>Service requests</h1><p>{list.Length} accessible request(s)</p>{policeTools}</div>{Logout(token)}</section>
      <div class="access-strip"><b>{H(ctx.User.Identity?.Name ?? "")}</b><span>{H(string.Join(" · ", DepartmentAccess.UserDepartments(ctx.User)))}</span><strong>{H(ctx.User.FindFirstValue(ClaimTypes.Role) ?? "Employee")}</strong></div>
      <form class="filters" method="get"><input name="q" value="{H(q ?? "")}" placeholder="Tracking number, service or address"/><select name="department"><option value="">All permitted departments</option>{options}</select><select name="status"><option value="">All statuses</option>{StatusOptions(status)}</select><button>Filter</button></form>
      <section class="table-card"><table><thead><tr><th>Request</th><th>Department</th><th>Location</th><th>Status</th><th>Assigned</th><th>Created</th></tr></thead><tbody>{(rows.Length>0?rows:"<tr><td colspan='6' class='empty'>No requests match this queue.</td></tr>")}</tbody></table></section></main>
    """;
    return Html(Page("Work queue", body, ctx.User));
}).RequireAuthorization();

app.MapGet("/requests/{tracking}", async (string tracking, HttpContext ctx, RequestRepository repo, IAntiforgery anti) =>
{
    var r = await repo.FindAsync(tracking);
    if (r is null) return Results.NotFound();
    if (!DepartmentAccess.CanAccess(ctx.User, r)) return Results.Forbid();
    var token = anti.GetAndStoreTokens(ctx).RequestToken!;
    var details = string.Join("", r.Details.Select(kv => $"<div><span>{H(Pretty(kv.Key))}</span><b>{H(kv.Value.ToString())}</b></div>"));
    var notes = r.Processing?.InternalNotes?.Count > 0 ? string.Join("", r.Processing.InternalNotes.OrderByDescending(n=>n.CreatedAt).Select(n=>$"<article><b>{H(n.Author)}</b><time>{n.CreatedAt.LocalDateTime:g}</time><p>{H(n.Text)}</p></article>")) : "<p class='muted'>No internal notes.</p>";
    var body = $"""
      <main class="wrap"><a class="back" href="/">← Work queue</a><section class="page-head"><div><span class="eyebrow">{H(DepartmentAccess.For(r))}</span><h1>{H(r.ServiceTitle)}</h1><p>{H(r.TrackingNumber)}</p></div>{Logout(token)}</section>
      <div class="detail-grid"><section class="card"><h2>Request</h2><dl><dt>Status</dt><dd>{H(r.Status)}</dd><dt>Location</dt><dd>{H(r.Location)}</dd><dt>Created</dt><dd>{r.CreatedAt.LocalDateTime:f}</dd><dt>Citizen contact</dt><dd>{(r.Contact.Anonymous?"Anonymous":H(string.Join(" · ",new[]{r.Contact.Name,r.Contact.Email,r.Contact.Phone}.Where(x=>!string.IsNullOrWhiteSpace(x)))))}</dd><dt>Assigned to</dt><dd>{H(r.Processing?.AssignedTo ?? "Unassigned")}</dd></dl><div class="detail-list">{details}</div></section>
      <aside><section class="card"><h2>Process request</h2><form method="post" action="/requests/{U(r.TrackingNumber)}/process"><input type="hidden" name="__RequestVerificationToken" value="{H(token)}"/><label>Status<select name="status">{StatusOptions(r.Status)}</select></label><label>Assign to<input name="assignedTo" value="{H(r.Processing?.AssignedTo ?? "")}" placeholder="Employee username or team"/></label><label>Internal note<textarea name="note" rows="5" placeholder="Visible to employees only"></textarea></label><button type="submit">Save processing update</button></form></section><section class="card notes"><h2>Internal history</h2>{notes}</section></aside></div></main>
    """;
    return Html(Page(r.TrackingNumber, body, ctx.User));
}).RequireAuthorization();

app.MapPost("/requests/{tracking}/process", async (string tracking, HttpContext ctx, RequestRepository repo, IAntiforgery anti) =>
{
    await anti.ValidateRequestAsync(ctx);
    var form = await ctx.Request.ReadFormAsync();
    var status = form["status"].ToString().Trim();
    var assignedTo = form["assignedTo"].ToString().Trim();
    var note = form["note"].ToString().Trim();
    var allowedStatuses = new HashSet<string>(new[]{"Submitted","Pending review","In review","Needs information","Assigned","In progress","Approved","Rejected","Resolved","Closed"}, StringComparer.OrdinalIgnoreCase);
    if (!allowedStatuses.Contains(status)) return Results.BadRequest("Invalid status.");
    var updated = await repo.UpdateAsync(tracking, r => DepartmentAccess.CanAccess(ctx.User, r), r =>
    {
        r.Status = status;
        r.Processing ??= new ProcessingRecord();
        r.Processing.AssignedDepartment ??= DepartmentAccess.For(r);
        r.Processing.AssignedTo = string.IsNullOrWhiteSpace(assignedTo) ? null : assignedTo;
        r.Processing.UpdatedAt = DateTimeOffset.UtcNow;
        r.Processing.UpdatedBy = ctx.User.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(note)) r.Processing.InternalNotes.Add(new InternalNote { Author = ctx.User.Identity?.Name ?? "employee", Text = note });
    });
    return updated is null ? Results.Forbid() : Results.Redirect($"/requests/{U(tracking)}");
}).RequireAuthorization();

app.MapGet("/forbidden", () => Html(Page("Access denied", "<main class='login-shell'><section class='login-card'><h1>Access denied</h1><p>Your account does not have permission for that department.</p><a class='button' href='/'>Return to work queue</a></section></main>", null), 403));
app.MapGet("/health", () => Results.Ok(new { status = "ok", product = "CLT++ Employee Portal" }));

app.Run();

static List<AppUser> LoadUsers(IConfiguration config, IWebHostEnvironment env)
{
    var raw = config["EMPLOYEE_USERS"];
    if (string.IsNullOrWhiteSpace(raw) && env.IsDevelopment())
        raw = "police.demo:ChangeMe!:Employee:Police;fire.demo:ChangeMe!:Employee:Fire;supervisor.demo:ChangeMe!:Supervisor:Police|Fire|Transportation|Solid Waste|Housing & Neighborhood Services|General Services|Charlotte Water|Animal Care & Control";
    if (string.IsNullOrWhiteSpace(raw)) return new();
    return raw.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(entry =>
    {
        var parts = entry.Split(':', 4);
        return new AppUser(parts[0], parts.Length>1?parts[1]:"", parts.Length>2?parts[2]:"Employee", parts.Length>3?parts[3].Split('|',StringSplitOptions.RemoveEmptyEntries):Array.Empty<string>());
    }).ToList();
}
static IResult Html(string text, int status=200) => Results.Content(text,"text/html; charset=utf-8",statusCode:status);
static string H(string? value) => WebUtility.HtmlEncode(value ?? "");
static string U(string value) => Uri.EscapeDataString(value);
static string Pretty(string value) => string.Concat(value.Select((c,i)=>char.IsUpper(c)&&i>0?" "+c:c.ToString())).Replace("_"," ");
static string StatusOptions(string? selected) => string.Join("", new[]{"Submitted","Pending review","In review","Needs information","Assigned","In progress","Approved","Rejected","Resolved","Closed"}.Select(x=>$"<option {(string.Equals(x,selected,StringComparison.OrdinalIgnoreCase)?"selected":"")}>{H(x)}</option>"));
static string Logout(string token) => $"<form method='post' action='/logout' class='logout'><input type='hidden' name='__RequestVerificationToken' value='{H(token)}'/><button>Sign out</button></form>";
static string Page(string title, string body, ClaimsPrincipal? user) => $"""<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{H(title)} · CLT++ Employee</title><link rel="stylesheet" href="/app.css"></head><body><header class="top"><a href="/" class="brand">CLT<span>++</span> <small>EMPLOYEE</small></a><div>Internal request processing</div></header>{body}<footer>CLT++ employee prototype · Department permissions are enforced server-side.</footer></body></html>""";
