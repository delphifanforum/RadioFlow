using System.Threading.Tasks;

namespace RadioFlow.App.Services;

/// <summary>Pencereler, tepsi ve uygulama yaşam döngüsü (ViewModel'lerin pencere türlerini bilmemesi için).</summary>
public interface IShellService
{
    void ShowWidget();

    void HideWidget();

    void ShowMainWindow();

    void ShowSettings();

    /// <summary>Widget kapatıldığında ayara göre tepsiye küçültür veya uygulamadan çıkar.</summary>
    void CloseWidget();

    Task ExitAsync();

    void ShowError(string message);

    /// <summary>Kullanıcıdan evet/hayır onayı alır.</summary>
    bool Confirm(string message, string title);
}
