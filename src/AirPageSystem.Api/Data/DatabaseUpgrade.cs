using Microsoft.EntityFrameworkCore;

namespace AirPageSystem.Api.Data;

public static class DatabaseUpgrade
{
    public static async Task ApplyAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS "Users" ("Id" TEXT NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY,"Username" TEXT NOT NULL,"DisplayName" TEXT NOT NULL,"PasswordHash" TEXT NOT NULL,"IsActive" INTEGER NOT NULL,"IsAdmin" INTEGER NOT NULL,"MustChangePassword" INTEGER NOT NULL,"CreatedAt" TEXT NOT NULL);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Username" ON "Users" ("Username");
CREATE TABLE IF NOT EXISTS "Roles" ("Id" TEXT NOT NULL CONSTRAINT "PK_Roles" PRIMARY KEY,"Name" TEXT NOT NULL,"Description" TEXT NOT NULL,"PermissionsJson" TEXT NOT NULL);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Roles_Name" ON "Roles" ("Name");
CREATE TABLE IF NOT EXISTS "UserRoles" ("UserId" TEXT NOT NULL,"RoleId" TEXT NOT NULL,CONSTRAINT "PK_UserRoles" PRIMARY KEY ("UserId","RoleId"));
CREATE TABLE IF NOT EXISTS "RetryPolicies" ("Id" TEXT NOT NULL CONSTRAINT "PK_RetryPolicies" PRIMARY KEY,"OwnerUserId" TEXT NOT NULL,"Name" TEXT NOT NULL,"MaxAttempts" INTEGER NOT NULL,"InitialDelayMs" INTEGER NOT NULL,"BackoffFactor" REAL NOT NULL,"MaxDelayMs" INTEGER NOT NULL,"RetryPreview" INTEGER NOT NULL,"RetryPush" INTEGER NOT NULL,"IsDefault" INTEGER NOT NULL);
""");
        foreach (var (table, column, type) in new[]
        {
            ("Devices","OwnerUserId","TEXT NULL"),("Templates","OwnerUserId","TEXT NULL"),
            ("DataSources","OwnerUserId","TEXT NULL"),("Schedules","OwnerUserId","TEXT NULL"),("Schedules","RetryPolicyId","TEXT NULL"),
            ("PushRecords","OwnerUserId","TEXT NULL"),("PushRecords","AttemptCount","INTEGER NOT NULL DEFAULT 1"),
            ("Templates","IsDeleted","INTEGER NOT NULL DEFAULT 0")
        })
        {
            var exists = await db.Database.SqlQueryRaw<int>($"SELECT COUNT(*) AS Value FROM pragma_table_info('{table}') WHERE name='{column}'").SingleAsync() > 0;
            if (!exists) await db.Database.ExecuteSqlRawAsync($"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {type}");
        }
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_Devices_OwnerUserId_IsDefault\" ON \"Devices\" (\"OwnerUserId\",\"IsDefault\")");
    }
}
