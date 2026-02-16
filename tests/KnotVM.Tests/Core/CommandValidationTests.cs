using FluentAssertions;
using KnotVM.CLI.Utils;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using Xunit;

namespace KnotVM.Tests.Core;

public class CommandValidationTests
{
    [Fact]
    public void EnsureExactlyOne_ShouldThrowUnexpectedError_WhenNoSelection()
    {
        Action act = () => CommandValidation.EnsureExactlyOne("none", "many", false, false, false);

        var ex = act.Should().Throw<KnotVMException>().Which;
        ex.ErrorCode.Should().Be(KnotErrorCode.UnexpectedError);
        ex.Message.Should().Be("none");
    }

    [Fact]
    public void EnsureExactlyOne_ShouldThrowUnexpectedError_WhenMultipleSelections()
    {
        Action act = () => CommandValidation.EnsureExactlyOne("none", "many", true, false, true);

        var ex = act.Should().Throw<KnotVMException>().Which;
        ex.ErrorCode.Should().Be(KnotErrorCode.UnexpectedError);
        ex.Message.Should().Be("many");
    }

    [Fact]
    public void EnsureExactlyOne_ShouldNotThrow_WhenSingleSelection()
    {
        Action act = () => CommandValidation.EnsureExactlyOne("none", "many", false, true, false);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureExactlyOne_ShouldThrowArgumentException_WhenNoFlagsProvided()
    {
        Action act = () => CommandValidation.EnsureExactlyOne("none", "many");

        act.Should().Throw<ArgumentException>();
    }
}
