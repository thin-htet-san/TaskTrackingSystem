using System.ComponentModel.DataAnnotations;
using TaskTrackingSystem.Shared.Localization;
using TaskTrackingSystem.Shared.Models.Project;
using TaskTrackingSystem.Shared.Models.User;
using Xunit;

namespace TaskTrackingSystem.Tests;

public sealed class BilingualPhaseTests
{
    [Fact]
    public void LanguageDetector_ClassifiesBurmeseEnglishMixedAndEmpty()
    {
        var detector = new LanguageDetectionService();

        Assert.Equal(DetectedContentLanguage.Empty, detector.Detect(" ").Language);
        Assert.Equal(DetectedContentLanguage.Burmese, detector.Detect("စီမံကိန်း").Language);
        Assert.Equal(DetectedContentLanguage.English, detector.Detect("Project Dashboard").Language);
        Assert.Equal(DetectedContentLanguage.Mixed, detector.Detect("Dashboard စီမံကိန်း").Language);
    }

    [Fact]
    public void ProjectValidation_AllowsEitherLanguageButRejectsNeither()
    {
        Assert.Empty(Validate(new CreateProjectDto { Name = "English" }));
        Assert.Empty(Validate(new CreateProjectDto { NameMy = "မြန်မာ" }));
        Assert.NotEmpty(Validate(new CreateProjectDto()));
    }

    [Fact]
    public void UserValidation_RequiresACompleteNameInOneLanguage()
    {
        Assert.DoesNotContain(Validate(User(firstName: "A", lastName: "User")), result => result.ErrorMessage?.Contains("complete name", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain(Validate(User(firstNameMy: "အ", lastNameMy: "သုံး")), result => result.ErrorMessage?.Contains("complete name", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(Validate(User(firstName: "A", firstNameMy: "အ")), result => result.ErrorMessage?.Contains("complete name", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task UnconfiguredTranslationDoesNotFakeText()
    {
        var result = await new NoOpContentTranslationService().TranslateAsync("Project", "en", "my");

        Assert.False(result.Success);
        Assert.Null(result.TranslatedText);
        Assert.False(result.WasGenerated);
        Assert.Equal("none", result.Provider);
    }

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        return results;
    }

    private static CreateUserDto User(string? firstName = null, string? lastName = null, string? firstNameMy = null, string? lastNameMy = null) => new()
    {
        Username = "testuser",
        Email = "test@example.com",
        Password = "Password1!",
        RoleId = 1,
        FirstName = firstName ?? string.Empty,
        LastName = lastName ?? string.Empty,
        FirstNameMy = firstNameMy,
        LastNameMy = lastNameMy
    };
}
