using FluentAssertions;
using SyncLib.Core.Helpers;
using Xunit;

namespace SyncLib.Tests.Helpers;

public class CopyLoggerTests
{
    [Fact]
    public void LogCopy_ShouldExecuteWithoutException()
    {
        var action = () => CopyLogger.LogCopy(
            sourcePath: "C:\\Source\\One Piece 01.cbz",
            mediaType: "Manga",
            finalFileName: "One Piece - Volume 01.cbz",
            destinationDir: "D:\\Dest\\One Piece"
        );

        action.Should().NotThrow();
    }

    [Fact]
    public void LogFilePath_ShouldBeValidString()
    {
        CopyLogger.LogFilePath.Should().NotBeNullOrWhiteSpace();
        CopyLogger.LogFilePath.Should().EndWith("copias_log.txt");
    }
}
