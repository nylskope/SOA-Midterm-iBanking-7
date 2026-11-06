using IBankingTuition.Infrastructure.Adapters;
using IBankingTuition.PaymentOrchestrator.Sagas;
using Microsoft.AspNetCore.Mvc;

namespace IBankingTuition.PaymentOrchestrator.Controllers;

[ApiController]
[Route("api/transactions")]
public class TransactionController : ControllerBase {
    private readonly PaymentSaga _saga;
    private readonly IConfiguration _config;

    public TransactionController(PaymentSaga saga, IConfiguration config) {
        _saga = saga;
        _config = config;
    }

    [HttpPost("pay")]
    public async Task<IActionResult> PayTuition([FromBody] PaymentCommand command) {
        var result = await _saga.ExecuteAsync(command);
        if (!result.Success) return BadRequest(new { message = result.Message });
        return Ok(result);
    }

    [HttpGet("return")]
    public async Task<IActionResult> VNPayReturn(
        [FromQuery] string vnp_TxnRef,
        [FromQuery] int userRowVersion,
        [FromQuery] int tuitionRowVersion,
        [FromQuery] string email)
    {
        var query = Request.Query;
        var hashSecret = _config["VNPay:HashSecret"] ?? throw new ArgumentNullException("VNPay HashSecret missing");

        var vnpay = new VnPayLibrary();

        foreach (var kvp in query) {
            if (!string.IsNullOrEmpty(kvp.Key) && kvp.Key.StartsWith("vnp_")) {
                vnpay.AddResponseData(kvp.Key, kvp.Value.ToString());
            }
        }

        var vnp_SecureHash = Request.Query["vnp_SecureHash"].ToString();

        bool checkSignature = vnpay.ValidateSignature(vnp_SecureHash, hashSecret);

        if (checkSignature) {
            if (vnpay.GetResponseData("vnp_ResponseCode") == "00" && vnpay.GetResponseData("vnp_TransactionStatus") == "00") {

                var finalizationSuccess = await _saga.CompletePaymentAsync(vnp_TxnRef, userRowVersion, tuitionRowVersion, email);

                if (finalizationSuccess) {
                    return Content("<html><body><h2>Payment Successful!</h2><p>Your tuition has been paid via VNPAY and your balance has been updated.</p></body></html>", "text/html");
                } else {
                    return Content("<html><body><h2>Internal Error</h2><p>VNPAY succeeded, but internal database synchronization failed due to a concurrency conflict.</p></body></html>", "text/html");
                }
            }
            return Content("<html><body><h2>Payment Failed or Canceled</h2><p>The transaction was aborted on the VNPAY terminal.</p></body></html>", "text/html");
        }

        return BadRequest("Invalid Signature");
    }
}
