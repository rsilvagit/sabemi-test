using System.Text.Json;
using SabemiTec.Api.ACL.Responses;

namespace SabemiTec.Api.ACL;

/// <summary>
/// Named after the capability, not the vendor — swapping partner banks is a new subfolder
/// implementing this interface, without touching the worker or the dashboard.
/// </summary>
public interface IPaymentWebhookAcl
{
    PaymentTranslationResult Translate(JsonElement body);
}
