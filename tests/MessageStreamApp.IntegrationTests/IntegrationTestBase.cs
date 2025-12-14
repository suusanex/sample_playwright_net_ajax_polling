using System.Net.Http;
using System.Threading.Tasks;

namespace MessageStreamApp.IntegrationTests;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected TestWebApplicationFactory Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;

    public virtual Task InitializeAsync()
    {
        Factory = CreateFactory();
        Client = Factory.CreateClient();
        return Task.CompletedTask;
    }

    public virtual Task DisposeAsync()
    {
        Client.Dispose();
        Factory.Dispose();
        return Task.CompletedTask;
    }

    protected virtual TestWebApplicationFactory CreateFactory()
    {
        return new TestWebApplicationFactory(TestWebApplicationFactory.CreateFastConfig());
    }
}
