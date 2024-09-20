using Microsoft.AspNetCore.Mvc;

namespace WaferMapViewer.Controllers
{
    public class LCRController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
