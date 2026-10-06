using TransactionAggregationUI.Models.Banks;

namespace TransactionAggregationUI.Services
{
    public class BankService(ApiClient api)
    {
        private const string AdminPath = "api/v1/admin/webhook-sources";

        private Dictionary<string, BankModel>? _banks;

        public async Task<IReadOnlyDictionary<string, BankModel>> GetBanksAsync(bool refresh = false)
        {
            if (_banks is null || refresh)
            {
                var banks = (await api.GetAsync<List<BankModel>>("api/v1/banks")).Value ?? [];
                _banks = banks.ToDictionary(b => b.Code, StringComparer.OrdinalIgnoreCase);
            }
            return _banks;
        }

        public Task<List<BankSourceModel>> GetSourcesAsync() =>
            api.GetAllPagesAsync<BankSourceModel>(AdminPath);

        public async Task<(CreateBankResultModel? Result, string? Error)> CreateAsync(string code, string displayName, string color)
        {
            var (result, error) = await api.SendAsync<CreateBankResultModel>(
                HttpMethod.Post, AdminPath, new { Code = code, DisplayName = displayName, Color = color });
            _banks = null;
            return (result, error);
        }

        public async Task<string?> UpdateAsync(Guid id, string displayName, string color)
        {
            var error = await api.SendAsync(HttpMethod.Put, $"{AdminPath}/{id}", new { DisplayName = displayName, Color = color });
            _banks = null;
            return error;
        }

        public async Task<(string? ApiKey, string? Error)> RotateKeyAsync(Guid id)
        {
            var (result, error) = await api.SendAsync<RotateKeyResultModel>(HttpMethod.Post, $"{AdminPath}/{id}/rotate", body: null);
            return (result?.ApiKey, error);
        }

        public async Task<string?> RegisterSigningKeyAsync(Guid id, string publicKey) =>
            await api.SendAsync(HttpMethod.Put, $"{AdminPath}/{id}/signing-key", new { PublicKey = publicKey });

        public async Task<string?> SetActiveAsync(Guid id, bool active)
        {
            var error = await api.SendAsync(HttpMethod.Post, $"{AdminPath}/{id}/{(active ? "activate" : "deactivate")}");
            _banks = null;
            return error;
        }
    }
}