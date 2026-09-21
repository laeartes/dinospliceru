using FluentAssertions;

namespace DinoSplicer.Api.Tests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        int expected = 1 + 1;

        int actual = 2;

        actual.Should().Be(expected);
    }
}