using Microsoft.AspNetCore.Mvc;

namespace LumenusErp.Controllers
{
    
    [ApiController]
    [Route("api/[controller]")]
    public class LumenusController : ControllerBase
    {
        [HttpGet]
        
        public IActionResult Index()
        {
            var request = Request;
            Console.WriteLine($"");
            if (IsValid(request))
            {
                return Ok(new { message = "Успешный успех!" });

            }


            return BadRequest(new { message = "Не суйса"});
        }



        private bool IsValid(HttpRequest request)
        {
            if (!request.Headers.ContainsKey("Authorization"))
            {
                return false;
            }

            string authHeader = request.Headers["Authorization"];

            // Проверяем, что заголовок начинается с "Bearer "
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return false;
            }

            // Извлекаем токен
            string token = authHeader.Substring("Bearer ".Length).Trim();

            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            // Здесь ваша логика валидации токена
            if (MySec.IsValidToken(token))
            {
                return true;
            }

            return false;
        }
    }
}
