using Microsoft.Data.Sqlite;

namespace CltPlusPlus.Employee;

public static class DevelopmentSeed
{
    public static void Seed(WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return;
        if (!string.Equals(app.Configuration["CLTPP_DEV_SEED"], "true", StringComparison.OrdinalIgnoreCase)) return;

        var publicSafety = app.Configuration["CLTPP_PUBLIC_SAFETY_DB"] ?? Path.Combine(AppContext.BaseDirectory, "data", "public-safety.db");
        var cityServices = app.Configuration["CLTPP_GENERAL_CITY_DB"] ?? Path.Combine(AppContext.BaseDirectory, "data", "city-services.db");
        Directory.CreateDirectory(Path.GetDirectoryName(publicSafety)!);
        Directory.CreateDirectory(Path.GetDirectoryName(cityServices)!);

        SeedPublicSafety(publicSafety);
        SeedAnimalCare(cityServices);
    }

    private static void SeedPublicSafety(string path)
    {
        using var cn = Open(path);
        var now = DateTimeOffset.UtcNow;

        Exec(cn, "INSERT OR IGNORE INTO OfficerDutyStatus(OfficerKey,OnDuty,OperationalStatus,UpdatedAt,UpdatedBy) VALUES($o,1,'Available',$t,'dev-seed')",
            ("$o", "police.demo"), ("$t", now.ToString("O")));
        Exec(cn, "INSERT OR IGNORE INTO OfficerDutyStatusHistory(Id,OfficerKey,DutyState,OperationalStatus,ChangedBy,ChangedAt) VALUES($i,$o,'On duty','Available','dev-seed',$t)",
            ("$i", "10000000-0000-0000-0000-000000000001"), ("$o", "police.demo"), ("$t", now.AddHours(-1).ToString("O")));

        var calls = new[]
        {
            new { Id="20000000-0000-0000-0000-000000000001", Number="CALL-DEV-0001", Type="Noise complaint", Priority="Routine", Location="123 N Tryon St, Charlotte, NC", Narrative="Caller reports loud music from a nearby property.", Status="Queued" },
            new { Id="20000000-0000-0000-0000-000000000002", Number="CALL-DEV-0002", Type="Suspicious vehicle", Priority="Priority", Location="500 E Morehead St, Charlotte, NC", Narrative="Caller reports a vehicle parked for an extended period.", Status="On scene" },
            new { Id="20000000-0000-0000-0000-000000000003", Number="CALL-DEV-0003", Type="Welfare check", Priority="Routine", Location="300 W Trade St, Charlotte, NC", Narrative="Requested welfare check for a resident who has not been reached.", Status="Open" }
        };
        foreach (var x in calls)
            Exec(cn, "INSERT OR IGNORE INTO PoliceCalls(Id,CallNumber,CallType,Priority,Location,LocationDetails,CallerName,CallerPhone,CallerEmail,CallerRelationship,AnonymousCaller,SafetyFlags,InitialNarrative,Status,AssignedTo,Disposition,ExpressReportId,CreatedBy,CreatedAt,UpdatedAt) VALUES($i,$n,$ct,$p,$l,'','Development Caller','+17045550100','citizen@example.test','Caller',0,'',$nr,$s,NULL,NULL,NULL,'dev.dispatch',$c,$u)",
                ("$i",x.Id),("$n",x.Number),("$ct",x.Type),("$p",x.Priority),("$l",x.Location),("$nr",x.Narrative),("$s",x.Status),("$c",now.AddMinutes(-45).ToString("O")),("$u",now.AddMinutes(-10).ToString("O")));

        Exec(cn, "INSERT OR IGNORE INTO PoliceCallOfficerAssignments(Id,PoliceCallId,OfficerKey,Status,AssignedBy,AssignedAt,UpdatedAt) VALUES($i,$c,$o,'Queued','dev.dispatch',$t,$t)",
            ("$i","30000000-0000-0000-0000-000000000001"),("$c",calls[0].Id),("$o","police.demo"),("$t",now.AddMinutes(-20).ToString("O")));
        Exec(cn, "INSERT OR IGNORE INTO PoliceCallOfficerAssignments(Id,PoliceCallId,OfficerKey,Status,AssignedBy,AssignedAt,EnRouteAt,OnSceneAt,UpdatedAt) VALUES($i,$c,$o,'On scene','dev.dispatch',$a,$e,$s,$u)",
            ("$i","30000000-0000-0000-0000-000000000002"),("$c",calls[1].Id),("$o","police.demo"),("$a",now.AddMinutes(-35).ToString("O")),("$e",now.AddMinutes(-30).ToString("O")),("$s",now.AddMinutes(-20).ToString("O")),("$u",now.AddMinutes(-20).ToString("O")));

        Exec(cn, "INSERT OR IGNORE INTO PoliceCallHistory(Id,PoliceCallId,Actor,Action,Detail,OccurredAt) VALUES($i,$c,'dev.dispatch','Officer assigned','Assigned to police.demo',$t)",
            ("$i","40000000-0000-0000-0000-000000000001"),("$c",calls[0].Id),("$t",now.AddMinutes(-20).ToString("O")));
        Exec(cn, "INSERT OR IGNORE INTO PoliceCallHistory(Id,PoliceCallId,Actor,Action,Detail,OccurredAt) VALUES($i,$c,'police.demo','On scene','Officer arrived on scene.',$t)",
            ("$i","40000000-0000-0000-0000-000000000002"),("$c",calls[1].Id),("$t",now.AddMinutes(-20).ToString("O")));

        Exec(cn, "INSERT OR IGNORE INTO PublicSafetyAlerts(Id,Title,Severity,AffectedArea,Message,ExternalUrl,Status,StartsAt,EndsAt,CreatedBy,CreatedAt,UpdatedBy,UpdatedAt) VALUES($i,'Development traffic advisory','Advisory','Uptown Charlotte','Sample development alert for testing the citizen Public Safety Alerts feed.','', 'Published',$s,$e,'dev-seed',$c,'dev-seed',$c)",
            ("$i","50000000-0000-0000-0000-000000000001"),("$s",now.AddHours(-1).ToString("O")),("$e",now.AddHours(5).ToString("O")),("$c",now.ToString("O")));
    }

    private static void SeedAnimalCare(string path)
    {
        using var cn = Open(path);
        var now = DateTimeOffset.UtcNow.ToString("O");
        Exec(cn, "INSERT OR IGNORE INTO ShelterSpaces(Id,Name,SpaceType,Capacity,Active,Notes,CreatedAt,CreatedBy) VALUES('60000000-0000-0000-0000-000000000001','Dog Kennel A','Kennel',12,1,'Development sample kennel',$t,'dev-seed')", ("$t",now));
        Exec(cn, "INSERT OR IGNORE INTO ShelterSpaces(Id,Name,SpaceType,Capacity,Active,Notes,CreatedAt,CreatedBy) VALUES('60000000-0000-0000-0000-000000000002','Cat Room A','Cat room',16,1,'Development sample cat room',$t,'dev-seed')", ("$t",now));
        Exec(cn, "INSERT OR IGNORE INTO ShelterAnimals(Id,AnimalNumber,Name,Species,Breed,Sex,ApproxAge,ColorMarkings,Microchip,CustodyStatus,AdoptionStatus,PublishToPublic,SpaceId,PublicDescription,InternalNotes,IntakeAt,CreatedBy,UpdatedAt) VALUES('70000000-0000-0000-0000-000000000001','AC-DEV-001','Milo','Dog','Labrador mix','Male','2 years','Black','','In custody','Available',1,'60000000-0000-0000-0000-000000000001','Friendly development sample dog who enjoys walks.','Sample data only',$t,'dev-seed',$t)", ("$t",now));
        Exec(cn, "INSERT OR IGNORE INTO ShelterAnimals(Id,AnimalNumber,Name,Species,Breed,Sex,ApproxAge,ColorMarkings,Microchip,CustodyStatus,AdoptionStatus,PublishToPublic,SpaceId,PublicDescription,InternalNotes,IntakeAt,CreatedBy,UpdatedAt) VALUES('70000000-0000-0000-0000-000000000002','AC-DEV-002','Luna','Cat','Domestic shorthair','Female','1 year','Gray tabby','','In custody','Available',1,'60000000-0000-0000-0000-000000000002','Calm development sample cat who likes quiet spaces.','Sample data only',$t,'dev-seed',$t)", ("$t",now));
    }

    private static SqliteConnection Open(string path)
    {
        var cn = new SqliteConnection($"Data Source={path}");
        cn.Open();
        return cn;
    }

    private static void Exec(SqliteConnection cn, string sql, params (string Name, object? Value)[] parameters)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Name, p.Value ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }
}
