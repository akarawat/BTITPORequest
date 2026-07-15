using Microsoft.AspNetCore.Mvc;

namespace BTITPORequest.Controllers;

public class InfoController : Controller
{
    [HttpGet]
    public IActionResult WorkInstruction()
    {
        return View();
    }
}
