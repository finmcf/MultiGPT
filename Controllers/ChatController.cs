using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace YourNamespace.Controllers
{
    public class Chatbot
    {
        private readonly string ApiEndpoint = "https://api.openai.com/v1/engines/davinci/completions";

        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public Chatbot(IConfiguration configuration)
        {
            _httpClient = new HttpClient();
            _apiKey = configuration["OpenAI:ApiKey"];
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        public async Task<string> GenerateResponse(string prompt)
        {
            var requestBody = new
            {
                prompt = prompt,
                max_tokens = 100,
                temperature = 0.6
            };

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(ApiEndpoint, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                Console.WriteLine(responseBody); // Print the response

                if (responseBody == null)
                {
                    throw new Exception("The response from the API was null.");
                }

                dynamic jsonResponse;
                try
                {
                    jsonResponse = JsonConvert.DeserializeObject(responseBody);
                }
                catch (JsonReaderException ex)
                {
                    Console.WriteLine("The response from the API was not valid JSON: " + ex.Message);
                    throw;
                }

                if (jsonResponse == null)
                {
                    throw new Exception("Unable to parse response from API.");
                }

                if (jsonResponse.choices != null && jsonResponse.choices[0] != null && jsonResponse.choices[0].text != null)
                {
                    var generatedResponse = jsonResponse.choices[0].text.Value;
                    return generatedResponse;
                }
                else
                {
                    throw new Exception("Unexpected response format from API.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message); // Print the error message
                throw;
            }
        }

        public List<string> BreakDownPrompt(string response)
        {
            var sentences = response.Split(new[] { ". " }, StringSplitOptions.None).ToList();
            return sentences;
        }

        public string AssembleResponses(List<string> responses)
        {
            var finalResponse = string.Join(" ", responses);
            return finalResponse;
        }
    }

    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public ChatController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> GetResponse(string prompt)
        {
            var managerBot = new Chatbot(_configuration);
            var workerBot1 = new Chatbot(_configuration);
            var workerBot2 = new Chatbot(_configuration);

            var conversation = new StringBuilder();
            conversation.AppendLine("Manager Bot: " + prompt);

            var managerResponse = await managerBot.GenerateResponse(prompt);
            conversation.AppendLine("Manager Bot: " + managerResponse);

            var breakdownPrompts = managerBot.BreakDownPrompt(managerResponse);

            List<Task<string>> workerTasks = new List<Task<string>>();
            foreach (var breakdownPrompt in breakdownPrompts)
            {
                workerTasks.Add(workerBot1.GenerateResponse(breakdownPrompt));
                workerTasks.Add(workerBot2.GenerateResponse(breakdownPrompt));
            }

            await Task.WhenAll(workerTasks);

            List<string> workerResponses = workerTasks.Select(task => task.Result).ToList();

            foreach (var response in workerResponses)
            {
                conversation.AppendLine("Worker Bot: " + response);
            }

            var finalResponse = managerBot.AssembleResponses(workerResponses);
            conversation.AppendLine("Final Response: " + finalResponse);

            return Ok(conversation.ToString());
        }
    }
}
