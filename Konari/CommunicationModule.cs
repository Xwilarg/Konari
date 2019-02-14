using Discord.Commands;
using System.Threading.Tasks;

namespace Konari
{
    public class CommunicationModule : ModuleBase
    {
        [Command("Status")]
        private async Task Status(params string[] args)
        {
            await ReplyAsync("Coming soon...");
        }
    }
}
