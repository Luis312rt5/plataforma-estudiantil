using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

string[] Symbols = ["🐶", "🐱", "🐸", "🦁", "🐼", "🐵", "🦊", "🐰"];   // símbolos del PIN
string[] Avatars = ["🐯", "🐨", "🦄", "🐙", "🐢", "🦉", "🐧", "🐞"];  // avatares de los niños
var hasher = new PasswordHasher<object>();

var b = WebApplication.CreateBuilder(args);
var cs = b.Configuration.GetConnectionString("Default")!;
b.Services.AddDbContext<Db>(o => o.UseMySql(cs, ServerVersion.AutoDetect(cs)));
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(b.Configuration["Jwt:Key"]!));
b.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new() { IssuerSigningKey = key, ValidateIssuer = false, ValidateAudience = false });
b.Services.AddAuthorization();
var app = b.Build();

using (var s = app.Services.CreateScope()) {
    var db = s.ServiceProvider.GetRequiredService<Db>();
    db.Database.EnsureCreated(); // para producción, migrar a EF Migrations
    // Fase 2: crea la tabla de resultados si la base ya existía de la Fase 1
    db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS `GameResults` (`Id` int NOT NULL AUTO_INCREMENT, `ChildId` int NOT NULL, `Game` varchar(40) NOT NULL, `Stars` int NOT NULL, `PlayedAt` datetime(6) NOT NULL, PRIMARY KEY (`Id`), KEY `IX_GameResults_ChildId` (`ChildId`)) CHARACTER SET utf8mb4");
    if (!db.Staff.Any()) {
        db.Staff.Add(new StaffUser { Name = "Directora", Role = "Admin",
            Email = app.Configuration["Seed:AdminEmail"]!, PasswordHash = hasher.HashPassword(0, app.Configuration["Seed:AdminPassword"]!) });
        db.Groups.Add(new Group { Name = "Párvulos" });
        db.SaveChanges();
    }
}

app.UseDefaultFiles(); app.UseStaticFiles(); app.UseAuthentication(); app.UseAuthorization();

string Token(string role, int id, string name, int hours) {
    var claims = new[] { new Claim(ClaimTypes.Role, role), new Claim("id", id.ToString()), new Claim("name", name) };
    var t = new JwtSecurityToken(claims: claims, expires: DateTime.UtcNow.AddHours(hours), signingCredentials: new(key, SecurityAlgorithms.HmacSha256));
    return new JwtSecurityTokenHandler().WriteToken(t);
}
string NewPin() => string.Concat(Enumerable.Range(0, 4).Select(_ => RandomNumberGenerator.GetInt32(8)));
string Emoji(string pin) => string.Concat(pin.Select(c => Symbols[c - '0']));
bool PinOk(Child c, string pin) => hasher.VerifyHashedPassword(0, c.PinHash, pin) != PasswordVerificationResult.Failed;

string UniquePin(Db db, int groupId, int? exceptId = null) { // PIN sin repetir dentro del grupo
    var mates = db.Children.Where(c => c.GroupId == groupId && c.Active && c.Id != exceptId).ToList();
    while (true) { var p = NewPin(); if (!mates.Any(m => PinOk(m, p))) return p; }
}

// ---------- Público (pantalla de los niños) ----------
var pub = app.MapGroup("/api/public");
pub.MapGet("/symbols", () => Symbols);
pub.MapGet("/groups", (Db db) => db.Groups.OrderBy(g => g.Name).ToList());
pub.MapGet("/groups/{id}/kids", (Db db, int id) =>
    db.Children.Where(c => c.GroupId == id && c.Active).OrderBy(c => c.FirstName).Select(c => new { c.Id, c.FirstName, c.Avatar }).ToList());

// ---------- Autenticación ----------
var locks = new System.Collections.Concurrent.ConcurrentDictionary<string, (int Fails, DateTime Until)>();
bool Locked(string k) => locks.TryGetValue(k, out var l) && l.Until > DateTime.UtcNow;
void Fail(string k) { var f = (locks.TryGetValue(k, out var l) ? l.Fails : 0) + 1; locks[k] = f >= 5 ? (0, DateTime.UtcNow.AddMinutes(5)) : (f, DateTime.MinValue); }
IResult Wait() => Results.Json(new { message = "Espera 5 minutos y vuelve a intentarlo." }, statusCode: 429);

app.MapPost("/api/auth/child", (Db db, ChildLogin r) => {
    var k = "c" + r.ChildId;
    if (Locked(k)) return Wait();
    var c = db.Children.Find(r.ChildId);
    if (c is null || !c.Active || !PinOk(c, r.Pin)) { Fail(k); return Results.Unauthorized(); }
    locks.TryRemove(k, out _);
    return Results.Ok(new { token = Token("Child", c.Id, c.FirstName, 2), name = c.FirstName, avatar = c.Avatar });
});
app.MapPost("/api/auth/staff", (Db db, StaffLogin r) => {
    var k = "s" + r.Email.ToLowerInvariant();
    if (Locked(k)) return Wait();
    var u = db.Staff.FirstOrDefault(x => x.Email == r.Email);
    if (u is null || hasher.VerifyHashedPassword(0, u.PasswordHash, r.Password) == PasswordVerificationResult.Failed) { Fail(k); return Results.Unauthorized(); }
    locks.TryRemove(k, out _);
    return Results.Ok(new { token = Token(u.Role, u.Id, u.Name, 8), name = u.Name, role = u.Role });
});

// ---------- Panel de la directora y docentes ----------
var staff = app.MapGroup("/api/staff").RequireAuthorization(p => p.RequireRole("Admin", "Teacher"));
staff.MapPost("/groups", (Db db, GroupDto r) => { db.Groups.Add(new Group { Name = r.Name }); db.SaveChanges(); return Results.Ok(); });
staff.MapPut("/groups/{id}", (Db db, int id, GroupDto r) => {
    var x = db.Groups.Find(id); if (x is null) return Results.NotFound();
    x.Name = r.Name; db.SaveChanges(); return Results.Ok();
});
staff.MapDelete("/groups/{id}", (Db db, int id) => {
    var x = db.Groups.Find(id); if (x is null) return Results.NotFound();
    if (db.Children.Any(c => c.GroupId == id)) return Results.BadRequest("Este grupo tiene niños. Cámbialos de grupo o retíralos antes de eliminarlo.");
    db.Groups.Remove(x); db.SaveChanges(); return Results.Ok();
});
staff.MapPost("/users", (Db db, NewUser r) => {
    db.Staff.Add(new StaffUser { Name = r.Name, Email = r.Email, Role = r.Role == "Admin" ? "Admin" : "Teacher", PasswordHash = hasher.HashPassword(0, r.Password) });
    db.SaveChanges(); return Results.Ok();
}).RequireAuthorization(p => p.RequireRole("Admin"));

staff.MapGet("/children", (Db db, string? q, int? groupId, bool? active) =>
    db.Children.Include(c => c.Guardians)
        .Where(c => (groupId == null || c.GroupId == groupId) && (active == null || c.Active == active)
                 && (q == null || (c.FirstName + " " + c.LastName).Contains(q)))
        .OrderBy(c => c.FirstName).Select(c => new { c.Id, c.FirstName, c.LastName, c.BirthDate, c.GroupId, c.Avatar, c.Allergies, c.Eps, c.Active, c.PhotoConsent, c.Guardians }).ToList());

staff.MapPost("/children", (Db db, NewChild r) => {
    if (!r.DataConsent) return Results.BadRequest("Se requiere la autorización de tratamiento de datos (Ley 1581 de 2012).");
    if (r.Guardians.Count == 0) return Results.BadRequest("Registra al menos un acudiente.");
    var pin = UniquePin(db, r.GroupId);
    var c = new Child { FirstName = r.FirstName, LastName = r.LastName, BirthDate = r.BirthDate, DocumentId = r.DocumentId, GroupId = r.GroupId,
        Allergies = r.Allergies, Eps = r.Eps, DataConsent = true, PhotoConsent = r.PhotoConsent, ConsentDate = DateTime.UtcNow,
        Avatar = Avatars[RandomNumberGenerator.GetInt32(Avatars.Length)], PinHash = hasher.HashPassword(0, pin),
        Guardians = r.Guardians.Select(g => new Guardian { FullName = g.FullName, DocumentId = g.DocumentId, Relation = g.Relation, Phone = g.Phone, Email = g.Email, CanPickUp = g.CanPickUp }).ToList() };
    db.Children.Add(c); db.SaveChanges();
    return Results.Ok(new { c.Id, c.FirstName, c.Avatar, pin = Emoji(pin) }); // el PIN solo se muestra ahora
});
staff.MapPost("/children/{id}/reset-pin", (Db db, int id) => {
    var c = db.Children.Find(id); if (c is null) return Results.NotFound();
    var pin = UniquePin(db, c.GroupId, c.Id); c.PinHash = hasher.HashPassword(0, pin); db.SaveChanges(); locks.TryRemove("c" + c.Id, out _);
    return Results.Ok(new { c.Id, c.FirstName, c.Avatar, pin = Emoji(pin) });
});
staff.MapPost("/children/{id}/retire", (Db db, int id) => {
    var c = db.Children.Find(id); if (c is null) return Results.NotFound();
    c.Active = false; db.SaveChanges(); return Results.Ok();
});

// ---------- Juegos y estrellas (solo niños) ----------
var kidApi = app.MapGroup("/api/child").RequireAuthorization(p => p.RequireRole("Child"));
int Me(ClaimsPrincipal u) => int.Parse(u.FindFirst("id")!.Value);
kidApi.MapGet("/stars", (Db db, ClaimsPrincipal u) => { var id = Me(u); return Results.Ok(new { total = db.GameResults.Where(r => r.ChildId == id).Sum(r => (int?)r.Stars) ?? 0 }); });
var Badges = new (string Id, string Emoji, string Name, Func<List<GameResult>, bool> Ok)[] {
    ("primero", "🌱", "Primer juego", r => r.Count >= 1),
    ("e10", "⭐", "10 estrellas", r => r.Sum(x => x.Stars) >= 10),
    ("e30", "🌟", "30 estrellas", r => r.Sum(x => x.Stars) >= 30),
    ("e100", "🏆", "100 estrellas", r => r.Sum(x => x.Stars) >= 100),
    ("colores", "🎨", "Experto en colores", r => r.Count(x => x.Game == "colores" && x.Stars == 3) >= 3),
    ("numeros", "🔢", "Experto en números", r => r.Count(x => x.Game == "numeros" && x.Stars == 3) >= 3),
    ("memoria", "🧠", "Experto en memoria", r => r.Count(x => x.Game == "memoria" && x.Stars == 3) >= 3),
    ("explorador", "🧭", "Explorador", r => r.Select(x => x.Game).Distinct().Count() >= 3),
    ("matematico", "🧮", "Pequeño matemático", r => r.Count(x => x.Game == "sumas" && x.Stars == 3) >= 3),
    ("letras", "🔤", "Amigo de las letras", r => r.Count(x => x.Game == "vocales" && x.Stars == 3) >= 3),
    ("gran", "🗺️", "Gran explorador", r => r.Select(x => x.Game).Distinct().Count() >= 7),
};
kidApi.MapGet("/badges", (Db db, ClaimsPrincipal u) => {
    var id = Me(u); var rs = db.GameResults.Where(r => r.ChildId == id).ToList();
    return Badges.Select(b => new { b.Id, b.Emoji, b.Name, got = b.Ok(rs) }).ToList();
});
kidApi.MapPost("/play", (Db db, ClaimsPrincipal u, PlayDto r) => {
    if (!new[] { "colores", "numeros", "memoria", "vocales", "formas", "sumas", "patrones" }.Contains(r.Game) || r.Stars < 1 || r.Stars > 3) return Results.BadRequest("Resultado no válido.");
    var id = Me(u); var before = db.GameResults.Where(x => x.ChildId == id).ToList();
    var had = Badges.Where(b => b.Ok(before)).Select(b => b.Id).ToHashSet();
    var add = new GameResult { ChildId = id, Game = r.Game, Stars = r.Stars, PlayedAt = DateTime.UtcNow };
    db.GameResults.Add(add); db.SaveChanges();
    before.Add(add);
    var fresh = Badges.Where(b => b.Ok(before) && !had.Contains(b.Id)).Select(b => new { b.Emoji, b.Name }).ToList();
    return Results.Ok(new { newBadges = fresh });
});
staff.MapGet("/progress", (Db db) => db.GameResults.GroupBy(r => r.ChildId).Select(x => new { childId = x.Key, stars = x.Sum(r => r.Stars), plays = x.Count() }).ToList());

app.Run();

record ChildLogin(int ChildId, string Pin);
record StaffLogin(string Email, string Password);
record GroupDto(string Name);
record NewUser(string Name, string Email, string Password, string Role);
record PlayDto(string Game, int Stars);
