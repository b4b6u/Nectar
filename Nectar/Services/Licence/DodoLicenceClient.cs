// Nectar Pro - proprietary. Not licensed under the repository's MIT licence.
// Copyright (c) 2026 b4b6u. All rights reserved. See PRO-LICENCE.md.

using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Nectar.Services;

namespace Nectar.Services.Licence;

/// <summary>
/// Talks to Dodo Payments' public license endpoints.
///
/// Dodo documents these as callable straight from desktop software, which is exactly what this is:
/// activation binds the key to one installation ("Nectar on DESKTOP-…") and validation checks that
/// both the key and this installation are still live. Nothing here needs the merchant API key, so
/// there is no secret in the shipped binary and nothing to rotate if the app is decompiled.
/// </summary>
internal sealed class DodoLicenceClient : ILicenceClient
{
    private const string LiveBase = "https://live.dodopayments.com";
    private const string TestBase = "https://test.dodopayments.com";

    private readonly HttpClient _http;

    internal DodoLicenceClient(HttpClient? http = null, bool useTestMode = false)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        BaseAddress = useTestMode ? TestBase : LiveBase;
    }

    internal string BaseAddress { get; }

    public async Task<LicenceActivation?> ActivateAsync(
        string licenceKey,
        string deviceName,
        CancellationToken cancellationToken)
    {
        using var body = new StringContent(
            JsonSerializer.Serialize(new { license_key = licenceKey, name = deviceName }),
            Encoding.UTF8,
            "application/json");

        using var response = await _http
            .PostAsync($"{BaseAddress}/licenses/activate", body, cancellationToken)
            .ConfigureAwait(false);

        // 404 unknown key, 403 known but inactive, 422 the activation limit is reached: all three
        // are "this key cannot run here", and none of them should look like a network failure.
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden
            or HttpStatusCode.UnprocessableEntity)
        {
            AppLogger.Log($"Licence activation refused: {(int)response.StatusCode}");
            return null;
        }

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        if (!document.RootElement.TryGetProperty("id", out var id) || id.GetString() is not { Length: > 0 } instanceId)
        {
            AppLogger.Log("Licence activation response had no id");
            return null;
        }

        return new LicenceActivation(instanceId);
    }

    public async Task<bool> ValidateAsync(string licenceKey, string instanceId, CancellationToken cancellationToken)
    {
        using var body = new StringContent(
            JsonSerializer.Serialize(new
            {
                license_key = licenceKey,
                license_key_instance_id = instanceId
            }),
            Encoding.UTF8,
            "application/json");

        using var response = await _http
            .PostAsync($"{BaseAddress}/licenses/validate", body, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        return document.RootElement.TryGetProperty("valid", out var valid) && valid.ValueKind == JsonValueKind.True;
    }
}
