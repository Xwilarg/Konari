using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Google.Cloud.Translation.V2;
using Google.Cloud.Vision.V1;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
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

        public static Program P { private set; get; }

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
            client.Log += DiscordUtils.Utils.Log;
            commands.Log += DiscordUtils.Utils.LogError;


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
            await CheckText(msg, arg);
            await CheckImage(msg, arg);
        }

        private async Task CheckImage(SocketUserMessage msg, SocketMessage arg)
        {
            if (msg.Attachments.Count > 0)
            {
                string url = msg.Attachments.ToArray()[0].Url;
                if (DiscordUtils.Utils.IsImage(url.Split('.').Last()))
                {
                    var image = await Google.Cloud.Vision.V1.Image.FetchFromUriAsync(url);
                    SafeSearchAnnotation response = await imageClient.DetectSafeSearchAsync(image);
                    if (response.Adult > Likelihood.Possible || response.Medical > Likelihood.Possible
                        || response.Racy > Likelihood.Possible || response.Violence > Likelihood.Possible
                        || response.Spoof > Likelihood.Possible)
                    {
                        await msg.DeleteAsync();
                        EmbedBuilder embed = new EmbedBuilder()
                        {
                            Description = arg.Author.Mention + " Your image was deleted"
                        };
                        int score = (int)response.Adult + (int)response.Medical + (int)response.Racy + (int)response.Spoof + (int)response.Violence - 5;
                        embed.AddField("Adult", response.Adult);
                        embed.AddField("Medical", response.Medical);
                        embed.AddField("Racy", response.Racy);
                        embed.AddField("Violence", response.Violence);
                        embed.AddField("Spoof", response.Spoof);
                        float red = score / 20f;
                        embed.Color = new Color(red, 1f - red, 0f);
                        await msg.Channel.SendMessageAsync("", false, embed.Build());
                    }
                }
            }
        }

        private async Task CheckText(SocketUserMessage msg, SocketMessage arg)
        {
            if (msg.Content.Length == 0)
                return;
            var detection = await translationClient.DetectLanguageAsync(msg.Content);
            string finalMsg = msg.Content;
            if (detection.Language != "en")
                finalMsg = (await translationClient.TranslateTextAsync(msg.Content, "en")).TranslatedText;
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
                foreach (var s in categories)
                {
                    double value = json.attributeScores[s.Item1].summaryScore.value;
                    if (value < s.Item2)
                        continue;
                    await msg.DeleteAsync();
                    await arg.Channel.SendMessageAsync("", false, new EmbedBuilder()
                    {
                        Title = s.Item1,
                        Description = arg.Author.Mention + " Your message was deleted because it trigger the " + s + " flag with a score of " + value,
                        Color = Color.Red,
                        Footer = new EmbedFooterBuilder()
                        {
                            Text = finalMsg
                        }
                    }.Build());
                    break;
                }
            }
        }
    }
}
