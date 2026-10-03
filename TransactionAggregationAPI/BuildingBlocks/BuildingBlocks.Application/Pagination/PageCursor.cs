using System.Buffers.Text;
using System.Text;
using System.Text.Json;

namespace BuildingBlocks.Application.Pagination
{
    public sealed record PageCursor(string Sort, bool Descending, string Key, Guid Id)
    {
        public const int MaxEncodedLength = 512;
        private const int CurrentVersion = 1;

        public string Encode()
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(new Payload(CurrentVersion, Sort, Descending, Key, Id));
            return Base64Url.EncodeToString(json);
        }

        public static bool TryDecode(string? encoded, out PageCursor? cursor)
        {
            cursor = null;
            if (string.IsNullOrWhiteSpace(encoded) || encoded.Length > MaxEncodedLength)
                return false;

            try
            {
                var payload = JsonSerializer.Deserialize<Payload>(Base64Url.DecodeFromChars(encoded));
                if (payload is not { V: CurrentVersion } || string.IsNullOrEmpty(payload.S) || payload.K is null || payload.I == Guid.Empty)
                    return false;

                cursor = new PageCursor(payload.S, payload.D, payload.K, payload.I);
                return true;
            }
            catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException or DecoderFallbackException)
            {
                return false;
            }
        }

        private sealed record Payload(int V, string S, bool D, string K, Guid I);
    }
}