using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BoneSearchAPI.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class SearchController : ControllerBase
    {
        [HttpGet]
        //get method
        public string Get()
        {
            return "Testing";
        }
    }
}
