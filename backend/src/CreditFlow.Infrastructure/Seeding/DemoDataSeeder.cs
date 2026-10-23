using CreditFlow.Application.Abstractions;
using CreditFlow.Application.Common;
using CreditFlow.Domain.Applications;
using CreditFlow.Domain.Clients;
using CreditFlow.Domain.Common;
using CreditFlow.Domain.Products;
using CreditFlow.Domain.Scoring;
using CreditFlow.Infrastructure.Identity;
using CreditFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CreditFlow.Infrastructure.Seeding;

/// <summary>Comptes et demandes de démonstration (Development uniquement, mots de passe issus de la configuration).</summary>
internal static class DemoDataSeeder
{
    private static readonly byte[] SamplePdf = "%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 595 842]>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n"u8.ToArray();

    public static async Task SeedAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken)
    {
        var options = services.GetRequiredService<IOptions<SeedOptions>>().Value;
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var clock = services.GetRequiredService<TimeProvider>();

        await EnsureUserAsync(userManager, clock, options.AdminEmail, options.AdminPassword, "Amina", "Benali", Roles.Admin, logger);
        var analyst = await EnsureUserAsync(userManager, clock, options.AnalystEmail, options.AnalystPassword, "Youssef", "El Idrissi", Roles.Analyst, logger);
        var client = await EnsureUserAsync(userManager, clock, options.ClientEmail, options.ClientPassword, "Salma", "Alaoui", Roles.Client, logger);
        if (client is null || analyst is null)
        {
            return;
        }

        var db = services.GetRequiredService<CreditFlowDbContext>();
        if (await db.CreditApplications.AnyAsync(a => a.ClientId == client.Id, cancellationToken))
        {
            return;
        }

        var now = clock.GetUtcNow();
        var profile = await db.ClientProfiles.FirstOrDefaultAsync(p => p.UserId == client.Id, cancellationToken);
        if (profile is null)
        {
            profile = ClientProfile.CreateFor(client.Id, now);
            db.ClientProfiles.Add(profile);
        }

        profile.Update(new DateOnly(1990, 4, 12), "+212 600 000 000", "Tanger", EmploymentType.Cdi, "Société Démo SARL", 18_000m, 1_500m, 6, now);

        var products = await db.CreditProducts.ToDictionaryAsync(p => p.Type, cancellationToken);
        var references = services.GetRequiredService<IReferenceGenerator>();
        var storage = services.GetRequiredService<IFileStorage>();
        var threshold = services.GetRequiredService<IOptions<ScoringOptions>>().Value.MaxDebtRatio;

        async Task<CreditApplication> DraftAsync(ProductType type, decimal amount, int months, string purpose, int daysAgo)
        {
            var at = now.AddDays(-daysAgo);
            var application = CreditApplication.CreateDraft(await references.NextAsync(at.Year, cancellationToken), client.Id, type, amount, months, purpose, at);
            db.CreditApplications.Add(application);
            return application;
        }

        async Task SubmitAsync(CreditApplication application, int daysAgo)
        {
            var at = now.AddDays(-daysAgo);
            foreach (var (type, name) in new[] { (DocumentType.IdCard, "cin.pdf"), (DocumentType.Payslip, "bulletin-salaire.pdf") })
            {
                using var content = new MemoryStream(SamplePdf);
                var key = await storage.SaveAsync(content, cancellationToken);
                application.AddDocument(type, name, "application/pdf", SamplePdf.Length, key, client.Id, at);
            }

            var assessment = RiskScoring.Assess(
                new ScoringInput(application.ProductType, application.Amount, application.DurationMonths, products[application.ProductType].AnnualRate, 18_000m, 1_500m, EmploymentType.Cdi, 6),
                threshold);
            application.Submit(assessment, client.Id, at);
        }

        await DraftAsync(ProductType.Auto, 120_000m, 60, "Achat d'un véhicule familial", 1);

        var submitted = await DraftAsync(ProductType.Consommation, 50_000m, 36, "Travaux de rénovation", 3);
        await SubmitAsync(submitted, 2);

        var underReview = await DraftAsync(ProductType.Immobilier, 900_000m, 240, "Acquisition d'un appartement", 12);
        await SubmitAsync(underReview, 10);
        underReview.AssignTo(analyst.Id, Roles.Analyst, now.AddDays(-9));

        var approved = await DraftAsync(ProductType.Consommation, 30_000m, 24, "Équipement de la maison", 45);
        await SubmitAsync(approved, 40);
        approved.AssignTo(analyst.Id, Roles.Analyst, now.AddDays(-39));
        approved.Approve(analyst.Id, Roles.Analyst, "Dossier complet, capacité de remboursement confirmée.", now.AddDays(-37));

        var infoRequested = await DraftAsync(ProductType.Auto, 250_000m, 84, "Véhicule utilitaire", 22);
        await SubmitAsync(infoRequested, 20);
        infoRequested.AssignTo(analyst.Id, Roles.Analyst, now.AddDays(-19));
        infoRequested.RequestInformation(analyst.Id, Roles.Analyst, "Merci de fournir vos trois derniers relevés bancaires.", now.AddDays(-18));

        var rejected = await DraftAsync(ProductType.Consommation, 280_000m, 84, "Projet personnel", 75);
        await SubmitAsync(rejected, 70);
        rejected.AssignTo(analyst.Id, Roles.Analyst, now.AddDays(-69));
        rejected.Reject(analyst.Id, Roles.Analyst, "Montant disproportionné au regard des revenus déclarés.", now.AddDays(-65));

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Données de démonstration créées pour {Email}", options.ClientEmail);
    }

    private static async Task<ApplicationUser?> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        TimeProvider clock,
        string email,
        string? password,
        string firstName,
        string lastName,
        string role,
        ILogger logger)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return existing;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Compte de démonstration {Role} ignoré : Seed:{Role}Password n'est pas configuré", role, role);
            return null;
        }

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            IsActive = true,
            CreatedAt = clock.GetUtcNow(),
        };
        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            logger.LogWarning("Compte de démonstration {Email} non créé : {Errors}", email, string.Join(", ", created.Errors.Select(e => e.Code)));
            return null;
        }

        await userManager.AddToRoleAsync(user, role);
        return user;
    }
}
