using IBankingTuition.PaymentOrchestrator.Sagas;
using Microsoft.AspNetCore.Mvc;

namespace IBankingTuition.PaymentOrchestrator.Controllers;

[ApiController]
[Route("api/transactions")]
public class TransactionController : ControllerBase {
    private readonly PaymentSaga _saga;

    public TransactionController(PaymentSaga saga) {
        _saga = saga;
    }

    [HttpPost("pay")]
    public async Task<IActionResult> PayTuition([FromBody] PaymentCommand command) {
        var result = await _saga.ExecuteAsync(command);
        
        if (!result.Success) return BadRequest(new { message = result.Message });
        
        return Ok(result);
    }
}