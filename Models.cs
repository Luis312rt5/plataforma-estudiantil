using Microsoft.EntityFrameworkCore;

public class Group { public int Id { get; set; } public string Name { get; set; } = ""; }

public class Child {
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateOnly BirthDate { get; set; }
    public string? DocumentId { get; set; }
    public int GroupId { get; set; }
    public string Avatar { get; set; } = "";
    public string PinHash { get; set; } = "";
    public string? Allergies { get; set; }
    public string? Eps { get; set; }
    public bool Active { get; set; } = true;
    public bool DataConsent { get; set; }
    public bool PhotoConsent { get; set; }
    public DateTime ConsentDate { get; set; }
    public List<Guardian> Guardians { get; set; } = new();
}

public class Guardian {
    public int Id { get; set; }
    public int ChildId { get; set; }
    public string FullName { get; set; } = "";
    public string? DocumentId { get; set; }
    public string Relation { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public bool CanPickUp { get; set; } = true;
}

public class StaffUser {
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "Teacher"; // Admin | Teacher
}

public class GameResult { public int Id { get; set; } public int ChildId { get; set; } public string Game { get; set; } = ""; public int Stars { get; set; } public DateTime PlayedAt { get; set; } }

public class Db(DbContextOptions<Db> o) : DbContext(o) {
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Child> Children => Set<Child>();
    public DbSet<Guardian> Guardians => Set<Guardian>();
    public DbSet<StaffUser> Staff => Set<StaffUser>();
    public DbSet<GameResult> GameResults => Set<GameResult>();
}

public record NewGuardian(string FullName, string? DocumentId, string Relation, string Phone, string? Email, bool CanPickUp);
public record NewChild(string FirstName, string LastName, DateOnly BirthDate, string? DocumentId, int GroupId,
    string? Allergies, string? Eps, bool DataConsent, bool PhotoConsent, List<NewGuardian> Guardians);
