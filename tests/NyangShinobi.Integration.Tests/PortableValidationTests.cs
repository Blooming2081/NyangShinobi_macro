using NyangShinobi.App.Diagnostics;

namespace NyangShinobi.Integration.Tests;

public sealed class PortableValidationTests
{
    [Fact]
    public async Task FrameworkDependentTestHostCannotPretendToUseBundledRuntime()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PortableValidation.VerifyAsync(AppContext.BaseDirectory, TestContext.Current.CancellationToken));
    }
}
