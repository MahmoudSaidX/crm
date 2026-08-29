using System.ComponentModel.DataAnnotations;
using SquadCrm.BuildingBlocks.Http;

namespace SquadCrm.UnitTests;

public sealed class PaginationTests
{
    [Fact]
    public void Defaults_AreStableAndValid()
    {
        PaginationRequest request = new();

        Assert.Equal(1, request.Page);
        Assert.Equal(20, request.PageSize);
        Assert.Empty(Validate(request));
    }

    [Theory]
    [InlineData(0, 20, "Page")]
    [InlineData(1, 0, "PageSize")]
    [InlineData(1, 201, "PageSize")]
    public void Bounds_AreExecutableBusinessRules(int page, int pageSize, string memberName)
    {
        PaginationRequest request = new(page, pageSize);

        ValidationResult result = Assert.Single(Validate(request));

        Assert.Contains(memberName, result.MemberNames);
    }

    private static List<ValidationResult> Validate(PaginationRequest request)
    {
        List<ValidationResult> results = [];
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }
}
