using RadioFlow.Core.Enums;

namespace RadioFlow.Core.Models;

/// <param name="Kind">Olay türü.</param>
/// <param name="Generation">Olayın ait olduğu Play çağrısının nesil numarası; eski yayınlardan gelen gecikmiş olayları ayıklamak için.</param>
/// <param name="BufferPercent">Buffering olaylarında doluluk yüzdesi.</param>
/// <param name="Metadata">MetadataChanged olaylarında ham ICY "NowPlaying" metni.</param>
/// <param name="ErrorMessage">Teknik hata açıklaması (yalnızca log için).</param>
public sealed record AudioEngineEvent(
    AudioEngineEventKind Kind,
    long Generation,
    float BufferPercent = 0,
    string? Metadata = null,
    string? ErrorMessage = null);
