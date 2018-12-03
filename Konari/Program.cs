using Discord;
using Discord.Commands;
using Discord.WebSocket;
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

        public DateTime StartTime { private set; get; }

        public static Program P { private set; get; }

        private readonly Tuple<string, float>[] categories = new Tuple<string, float>[] {
            new Tuple<string, float>("TOXICITY", .80f),
            new Tuple<string, float>("SEVERE_TOXICITY", .40f),
            new Tuple<string, float>("IDENTITY_ATTACK", .40f),
            new Tuple<string, float>("INSULT", .40f),
            new Tuple<string, float>("PROFANITY", .40f),
            new Tuple<string, float>("THREAT", .40f),
            new Tuple<string, float>("INFLAMMATORY", .60f),
            new Tuple<string, float>("OBSCENE", .80f),
            new Tuple<string, float>("FLIRTATION", .80f),
            new Tuple<string, float>("SPAM", .40f)
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
        }

        private async Task CheckText(SocketUserMessage msg, SocketMessage arg)
        {
            using (HttpClient hc = new HttpClient())
            {
                HttpResponseMessage post = await hc.PostAsync("https://commentanalyzer.googleapis.com/v1alpha1/comments:analyze?key=" + perspectiveApi, new StringContent(
                        JsonConvert.DeserializeObject("{comment: {text: \"" + DiscordUtils.Utils.EscapeString(msg.Content) + "\"},"
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
                    string text;
                    if (value > .80 && value > s.Item2)
                        text = "🇪";
                    else if (value > .60 && value > s.Item2)
                        text = "🇩";
                    else if (value > .40 && value > s.Item2)
                        text = "🇨";
                    else
                        continue;
                    await msg.DeleteAsync();
                    await arg.Channel.SendMessageAsync("", false, new EmbedBuilder()
                    {
                        Title = s.Item1,
                        Description = arg.Author.Mention + " Your message was deleted because it trigger the " + s + " flag with a score of " + text,
                        Color = Color.Red,
                        Footer = new EmbedFooterBuilder()
                        {
                            Text = msg.Content
                        }
                    }.Build());
                    break;
                }
            }
        }
    }
}
