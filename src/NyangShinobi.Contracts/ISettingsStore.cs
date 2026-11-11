namespace NyangShinobi.Contracts;

public interface ISettingsStore
{
    Task<GameSessionSettings> LoadAsync(CancellationToken cancellationToken);
}
