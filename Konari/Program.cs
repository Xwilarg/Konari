using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordUtils;
using Google.Cloud.Translation.V2;
using Google.Cloud.Vision.V1;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Konari
{
    class Program
    {
        public static void Main(string[] args)
                 => new Program().MainAsync().GetAwaiter().GetResult();

        public readonly DiscordSocketClient client;
        private readonly CommandService commands = new CommandService();

        private readonly string perspectiveApi;
        private ImageAnnotatorClient imageClient;
        private TranslationClient translationClient;

        public DateTime StartTime { private set; get; }
        private Random rand;

        public static Program P { private set; get; }

        private readonly string requestUrlText;
        private readonly string requestUrlImage;
        private readonly string requestToken;

        private readonly Tuple<string, float>[] categories = new Tuple<string, float>[] {
            new Tuple<string, float>("TOXICITY", .80f),
            new Tuple<string, float>("SEVERE_TOXICITY", .60f),
            new Tuple<string, float>("IDENTITY_ATTACK", .40f),
            new Tuple<string, float>("INSULT", .60f),
            new Tuple<string, float>("PROFANITY", .80f),
            new Tuple<string, float>("THREAT", .60f),
            new Tuple<string, float>("INFLAMMATORY", .60f),
            new Tuple<string, float>("OBSCENE", .9f)
        };

        private Program()
        {
            P = this;
            perspectiveApi = File.ReadAllText("Keys/perspectiveAPI.txt");
            client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Verbose,
            });
            client.Log += Utils.Log;
            commands.Log += Utils.LogError;
            string[] request = File.ReadAllLines("Keys/url.txt");
            requestUrlText = request[0];
            requestUrlImage = request[1];
            requestToken = request[2];
            rand = new Random();

            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "Keys/imageAPI.json");
            imageClient = ImageAnnotatorClient.Create();

            translationClient = TranslationClient.Create();
        }

        private async Task MainAsync()
        {
            client.MessageReceived += HandleCommandAsync;

            await client.LoginAsync(TokenType.Bot, File.ReadAllText("Keys/token.txt"));
            StartTime = DateTime.Now;
            await client.StartAsync();

            await Task.Delay(-1);
        }

        private async Task<bool> SendToServerAsync(List<string> flags, string endpoint, string userId)
        {
            if (flags != null)
            {
                string val = flags.Count == 0 ? "SAFE" : string.Join(",", flags);
                try
                {
                    using (HttpClient httpClient = new HttpClient())
                    {
                        HttpRequestMessage httpMsg = new HttpRequestMessage(HttpMethod.Post, endpoint + "?token=" + requestToken + "&flags=" + val + "&userId=" + userId);
                        await httpClient.SendAsync(httpMsg);
                    }
                }
                catch (HttpRequestException http)
                {
                    await Utils.Log(new LogMessage(LogSeverity.Error, http.Source, http.Message, http));
                }
                return (val != "SAFE");
            }
            return (false);
        }

        private async Task HandleCommandAsync(SocketMessage arg)
        {
            SocketUserMessage msg = arg as SocketUserMessage;
            if (msg == null || arg.Author.IsBot) return;
            int pos = 0;
            if (msg.HasMentionPrefix(client.CurrentUser, ref pos) || msg.HasStringPrefix("k.", ref pos))
            {
                SocketCommandContext context = new SocketCommandContext(client, msg);
                await commands.ExecuteAsync(context, pos);
            }
#pragma warning disable 4014
            Task.Run(async () =>
            {
                await SendToServerAsync(await CheckText(msg, arg), requestUrlText, arg.Author.Id.ToString());

            });
            Task.Run(async () =>
            {
                await SendToServerAsync(await CheckImage(msg, arg), requestUrlImage, arg.Author.Id.ToString());
            });
            Task.Run(async () =>
            {
                foreach (Match m in Regex.Matches(msg.Content, "https?:\\/\\/[^ ]+"))
                {
                    if (Utils.IsLinkValid(m.Value))
                        if (await SendToServerAsync(await CheckImageUrl(m.Value, msg, arg), requestUrlImage, arg.Author.Id.ToString()))
                            break;
                }
            });
#pragma warning restore 4014
        }

        private async Task<List<string>> CheckImage(SocketUserMessage msg, SocketMessage arg)
        {
            if (msg.Attachments.Count > 0)
                return (await CheckImageUrl(msg.Attachments.ToArray()[0].Url, msg, arg));
            return (null);
        }

        private string GenerateFileName()
        {
            string finalName = "";
            for (int i = 0; i < 30; i++)
                finalName += rand.Next('0', '9' + 1);
            return (finalName);
        }

        private async Task<List<string>> CheckImageUrl(string url, SocketUserMessage msg, SocketMessage arg)
        {
            if (Utils.IsImage(url.Split('.').Last()))
            {
                var image = await Google.Cloud.Vision.V1.Image.FetchFromUriAsync(url);
                SafeSearchAnnotation response = await imageClient.DetectSafeSearchAsync(image);
                if (response.Adult > Likelihood.Possible || response.Medical > Likelihood.Possible
                    || response.Racy > Likelihood.Possible || response.Violence > Likelihood.Possible
                    || response.Spoof > Likelihood.Possible)
                {
                    List<string> flags = new List<string>();
                    if (response.Adult > Likelihood.Possible)
                        flags.Add("Adult(" + response.Adult.ToString() + ")");
                    if (response.Medical > Likelihood.Possible)
                        flags.Add("Medical(" + response.Medical.ToString() + ")");
                    if (response.Racy > Likelihood.Possible)
                        flags.Add("Racy(" + response.Racy.ToString() + ")");
                    if (response.Violence > Likelihood.Possible)
                        flags.Add("Violence(" + response.Violence.ToString() + ")");
                    if (response.Spoof > Likelihood.Possible)
                        flags.Add("Spoof(" + response.Spoof.ToString() + ")");
                    string fileName = "SPOILER_" + GenerateFileName() + "." + url.Split('.').Last();
                    using (HttpClient hc = new HttpClient())
                        File.WriteAllBytes(fileName, await hc.GetByteArrayAsync(url));
                    await msg.Channel.SendMessageAsync(arg.Author.Mention + " Your image was deleted because it trigger the following flags: " + string.Join(", ", flags));
                    await msg.Channel.SendFileAsync(fileName);
                    await msg.DeleteAsync();
                    File.Delete(fileName);
                    return (flags.Select(x => x.Split('(')[0]).ToList());
                }
            }
            return (null);
        }

        private readonly string[] allowedLanguage = new string[]
        {
            "en", "fr", "es", "de"
        }; // Languages supported by Perspective API. Others message are translated to english
        private async Task<List<string>> CheckText(SocketUserMessage msg, SocketMessage arg)
        {
            if (msg.Content.Length == 0)
                return (null);
            var detection = await translationClient.DetectLanguageAsync(msg.Content);
            string finalMsg = msg.Content;
            if (!allowedLanguage.Contains(detection.Language))
            {
                finalMsg = (await translationClient.TranslateTextAsync(msg.Content, "en")).TranslatedText;
            }
            using (HttpClient hc = new HttpClient())
            {
                HttpResponseMessage post = await hc.PostAsync("https://commentanalyzer.googleapis.com/v1alpha1/comments:analyze?key=" + perspectiveApi, new StringContent(
                        JsonConvert.DeserializeObject("{comment: {text: \"" + DiscordUtils.Utils.EscapeString(finalMsg) + "\"},"
                                                    + "languages: [\"en\"],"
                                                    + "requestedAttributes: {" + string.Join(":{}, ", categories.Select(x => x.Item1)) + ":{}} }").ToString(), Encoding.UTF8, "application/json"));

                dynamic json = JsonConvert.DeserializeObject(await post.Content.ReadAsStringAsync());
                EmbedBuilder embed = new EmbedBuilder()
                {
                    Title = "Identification"
                };
                List<string> flags = new List<string>();
                foreach (var s in categories)
                {
                    double value = json.attributeScores[s.Item1].summaryScore.value;
                    if (value >= s.Item2)
                        flags.Add(s.Item1 + "(" + value.ToString("0.00") + ")");
                }
                if (flags.Count > 0)
                {
                    await msg.DeleteAsync();
                    await arg.Channel.SendMessageAsync(arg.Author.Mention + " Your message was deleted because it trigger the following flags: " + string.Join(", ", flags) +Environment.NewLine + Environment.NewLine +
                        "Original message: ||" + msg.Content.Replace("|", "\\|") + "||");
                }
                return (flags.Select(x => x.Split('(')[0]).ToList());
            }
        }
    }
}
