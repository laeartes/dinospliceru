using System.ComponentModel.DataAnnotations;

using DinoSplicer.Api.Models;

using FluentAssertions;

namespace DinoSplicer.Api.Tests.Models;

public class SpliceOutputTests
{
    private static List<ValidationResult> Validate(SpliceOutput output)
    {
        ValidationContext context = new(output);
        List<ValidationResult> results = [];
        Validator.TryValidateObject(output, context, results, validateAllProperties: true);
        return results;
    }

    private static SpliceOutput CreateOutput(int segmentIndex) => new()
    {
        SpliceJobId = Guid.NewGuid(),
        FileName = $"segment-{segmentIndex}.mp4",
        StoragePath = $"splices/segment-{segmentIndex}.mp4",
        SegmentIndex = segmentIndex,
        DurationSeconds = 60,
        SizeBytes = 1_048_576
    };

    // Validation

    [Fact]
    public void SpliceOutput_AllFieldsValid_PassesValidation()
    {
        Validate(CreateOutput(1)).Should().BeEmpty();
    }

    [Fact]
    public void SpliceOutput_MissingFileName_FailsValidation()
    {
        SpliceOutput output = CreateOutput(1);
        output.FileName = "";

        List<ValidationResult> results = Validate(output);

        results.Should().Contain(r => r.MemberNames.Contains(nameof(SpliceOutput.FileName)));
    }

    [Fact]
    public void SpliceOutput_MissingStoragePath_FailsValidation()
    {
        SpliceOutput output = CreateOutput(1);
        output.StoragePath = "";

        List<ValidationResult> results = Validate(output);

        results.Should().Contain(r => r.MemberNames.Contains(nameof(SpliceOutput.StoragePath)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SpliceOutput_SegmentIndexBelowOne_FailsValidation(int segmentIndex)
    {
        SpliceOutput output = CreateOutput(1);
        output.SegmentIndex = segmentIndex;

        List<ValidationResult> results = Validate(output);

        results.Should().Contain(r => r.MemberNames.Contains(nameof(SpliceOutput.SegmentIndex)));
    }

    [Fact]
    public void SpliceOutput_NegativeDurationSeconds_FailsValidation()
    {
        SpliceOutput output = CreateOutput(1);
        output.DurationSeconds = -1;

        List<ValidationResult> results = Validate(output);

        results.Should().Contain(r => r.MemberNames.Contains(nameof(SpliceOutput.DurationSeconds)));
    }

    [Fact]
    public void SpliceOutput_NegativeSizeBytes_FailsValidation()
    {
        SpliceOutput output = CreateOutput(1);
        output.SizeBytes = -1;

        List<ValidationResult> results = Validate(output);

        results.Should().Contain(r => r.MemberNames.Contains(nameof(SpliceOutput.SizeBytes)));
    }

    // Ordering (IComparable<SpliceOutput>)

    [Fact]
    public void CompareTo_LowerSegmentIndex_ReturnsNegative()
    {
        CreateOutput(1).CompareTo(CreateOutput(2)).Should().BeNegative();
    }

    [Fact]
    public void CompareTo_HigherSegmentIndex_ReturnsPositive()
    {
        CreateOutput(3).CompareTo(CreateOutput(2)).Should().BePositive();
    }

    [Fact]
    public void CompareTo_SameSegmentIndex_ReturnsZero()
    {
        CreateOutput(2).CompareTo(CreateOutput(2)).Should().Be(0);
    }

    [Fact]
    public void CompareTo_Null_ReturnsPositive()
    {
        CreateOutput(1).CompareTo(null).Should().BePositive();
    }

    [Fact]
    public void Sort_OutputsInRandomOrder_OrdersBySegmentIndex()
    {
        List<SpliceOutput> outputs = [CreateOutput(3), CreateOutput(1), CreateOutput(5), CreateOutput(2), CreateOutput(4)];

        outputs.Sort();

        outputs.Select(o => o.SegmentIndex).Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void OrderBy_OutputsInRandomOrder_OrdersBySegmentIndex()
    {
        List<SpliceOutput> outputs = [CreateOutput(3), CreateOutput(1), CreateOutput(2)];

        IEnumerable<SpliceOutput> sorted = outputs.OrderBy(o => o);

        sorted.Select(o => o.SegmentIndex).Should().Equal(1, 2, 3);
    }
}
