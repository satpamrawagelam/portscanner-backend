using System;
using System.Net.Http;
using System.Text;
using System.Text.Json; 
using System.Threading.Tasks;

namespace portscanner_backend.Controllers 
{
    public class AlertController
    {
        

        public static async Task SendAlertAsync(string message)
        {
            string url = $"https://api.telegram.org/bot{BotToken}/sendMessage";
            
            var payload = new 
            {
                chat_id = ChatId,
                text = message,
                parse_mode = "HTML"
            };

            string jsonPayload = JsonSerializer.Serialize(payload);

            using (var client = new HttpClient())
            {
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                
                try
                {
                    HttpResponseMessage response = await client.PostAsync(url, content);
                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Notifikasi Telegram berhasil dikirim!");
                    }
                    else
                    {
                        string errorMsg = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Gagal kirim Telegram: {response.StatusCode} - {errorMsg}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error Telegram: {ex.Message}");
                }
            }
        }
    }
}
