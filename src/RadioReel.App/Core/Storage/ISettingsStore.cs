namespace RadioReel.App.Core.Storage;

public interface ISettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
}
