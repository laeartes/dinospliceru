using DinoSplicer.Api.Models;

using FluentAssertions;

namespace DinoSplicer.Api.Tests.Models;

public class ResolutionTests
{
    [Fact]
    public void Resolution_ValidDimensions_ExposesWidthAndHeight()
    {
        Resolution resolution = new(1920, 1080);

        resolution.Width.Should().Be(1920);
        resolution.Height.Should().Be(1080);
    }

    [Theory]
    [InlineData(0, 1080)]
    [InlineData(-1, 1080)]
    [InlineData(1920, 0)]
    [InlineData(1920, -5)]
    public void Resolution_NonPositiveDimension_ThrowsArgumentOutOfRangeException(int width, int height)
    {
        Action act = () => _ = new Resolution(width, height);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Resolution_SameDimensions_AreEqualValues()
    {
        Resolution a = new(1280, 720);
        Resolution b = new(1280, 720);

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void Resolution_ToString_FormatsWidthByHeight()
    {
        new Resolution(1280, 720).ToString().Should().Be("1280x720");
    }

    [Fact]
    public void Video_NoMetadataSet_HasNullResolutionAndCodec()
    {
        Video video = new();

        video.Resolution.Should().BeNull();
        video.Codec.Should().BeNull();
    }
}
