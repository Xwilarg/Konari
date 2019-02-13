using Discord;
using Discord.WebSocket;
using DiscordUtils;
using Google.Cloud.Vision.V1;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Konari
{
    public static class Analyze
    {
        public static async Task<bool> SendToServerAsync(List<string> flags, string endpoint, string userId)
        {
            if (flags != null)
            {
                string val = flags.Count == 0 ? "SAFE" : string.Join(",", flags);
                try
                {
                    using (HttpClient httpClient = new HttpClient())
                    {
                        HttpRequestMessage httpMsg = new HttpRequestMessage(HttpMethod.Post, endpoint + "&token=" + Program.P.requestToken + "&flags=" + val + "&userId=" + userId);
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

        public static async Task<List<string>> CheckImage(SocketUserMessage msg, SocketMessage arg)
        {
            if (msg.Attachments.Count > 0)
                return (await CheckImageUrl(msg.Attachments.ToArray()[0].Url, msg, arg));
            return (null);
        }

        /// Generate a random filename when downloading images
        public static string GenerateFileName()
        {
            string finalName = "";
            for (int i = 0; i < 30; i++)
                finalName += Program.P.rand.Next('0', '9' + 1);
            return (finalName);
        }

        public static async Task<List<string>> CheckImageUrl(string url, SocketUserMessage msg, SocketMessage arg)
        {
            if (Utils.IsImage(url.Split('.').Last()))
            {
                var image = await Google.Cloud.Vision.V1.Image.FetchFromUriAsync(url);
                List<string> flags = new List<string>();
                SafeSearchAnnotation response = await Program.P.imageClient.DetectSafeSearchAsync(image);
                if (response.Adult > Likelihood.Possible || response.Medical > Likelihood.Possible
                    || response.Racy > Likelihood.Possible || response.Violence > Likelihood.Possible
                    || response.Spoof > Likelihood.Possible)
                {
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
                flags.Add("SAFE");
                return (flags);
            }
            return (null);
        }

        private static readonly Tuple<string, float>[] categories = new Tuple<string, float>[] {
            new Tuple<string, float>("TOXICITY", .80f),
            new Tuple<string, float>("SEVERE_TOXICITY", .60f),
            new Tuple<string, float>("IDENTITY_ATTACK", .40f),
            new Tuple<string, float>("INSULT", .60f),
            new Tuple<string, float>("PROFANITY", .80f),
            new Tuple<string, float>("THREAT", .60f),
            new Tuple<string, float>("INFLAMMATORY", .60f),
            new Tuple<string, float>("OBSCENE", .9f)
        };
        private static readonly string[] allowedLanguage = new string[]
        {
            "en", "fr", "es", "de"
        }; // Languages supported by Perspective API. Others message are translated to english
        public static async Task<List<string>> CheckText(SocketUserMessage msg, SocketMessage arg)
        {
            if (msg.Content.Length == 0)
                return (null);
            var detection = await Program.P.translationClient.DetectLanguageAsync(msg.Content);
            string finalMsg = msg.Content;
            if (!allowedLanguage.Contains(detection.Language))
            {
                finalMsg = (await Program.P.translationClient.TranslateTextAsync(msg.Content, "en")).TranslatedText;
            }
            using (HttpClient hc = new HttpClient())
            {
                HttpResponseMessage post = await hc.PostAsync("https://commentanalyzer.googleapis.com/v1alpha1/comments:analyze?key=" + Program.P.perspectiveApi, new StringContent(
                        JsonConvert.DeserializeObject("{comment: {text: \"" + Utils.EscapeString(finalMsg) + "\"},"
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
                    await arg.Channel.SendMessageAsync(arg.Author.Mention + " Your message was deleted because it trigger the following flags: " + string.Join(", ", flags) + Environment.NewLine + Environment.NewLine +
                        "Original message: ||" + msg.Content.Replace("|", "\\|") + "||");
                }
                return (flags.Select(x => x.Split('(')[0]).ToList());
            }
        }
    }
}
