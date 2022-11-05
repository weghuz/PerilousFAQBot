using Discord.Commands;
using Discord.WebSocket;
using Discord;
using FAQBot.Models;
using Microsoft.Extensions.Configuration;
using System.Text;
using Microsoft.EntityFrameworkCore;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace FAQBot
{
    internal class FAQBot
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandHandler _commands;
        private readonly FAQDB _db;
        private readonly IConfiguration _config;
        private const ulong ROLE_TRUE_PIRATE_ID = 968880274517655612;
        private const ulong PIRATES_GUILD_ID = 940071768679395369;
        private List<FAQEntry> _faq;

        public FAQBot(IConfiguration config, FAQDB db)
        {
            _db = db;
            _config = config;
            _faq = new();
            _client = new DiscordSocketClient(new DiscordSocketConfig { MessageCacheSize = 100 });
            _commands = new CommandHandler(_client, new CommandService(new CommandServiceConfig() { DefaultRunMode = RunMode.Async }));
        }

        public async Task MainAsync(string[] args)
        {
            Console.WriteLine("Fetching FAQ DB!");
            _faq = await _db.FAQs
                .Include(faq => faq.Tags)
                .ToListAsync();
            Console.WriteLine($"Fetched {_faq.Count} rows.");
            _client.Log += Log;
            var token = _config.GetValue<string>("DiscordToken");

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
            await _commands.InstallCommandsAsync();
            _client.MessageReceived += MessageReceived;
            _client.Ready += InitiateSlashCommands;
            _client.SlashCommandExecuted += SlashCommandExecuted;
            await Task.Delay(-1);
        }

        private async Task MessageReceived(SocketMessage arg)
        {
            var msg = arg as SocketUserMessage;
            if (msg is null || msg.Author.IsBot)
            {
                return;
            }

            if (msg.Content.Contains(_client.CurrentUser.Mention.Replace("!", "")))
            {
                await msg.ReplyAsync("This is a FAQ Bot! \n to know more execute the /faq command");
            }
        }

        private async Task SlashCommandExecuted(SocketSlashCommand command)
        {
            try
            {
                switch (command.CommandName)
                {
                    case "faq-help":
                        await FAQHelpCommand(command);
                        break;
                    case "faq":
                        await FAQCommand(command);
                        break;
                    case "faq-list":
                        await FAQListCommand(command);
                        break;
                    case "faq-add":
                        await FAQAddCommand(command);
                        break;
                    case "faq-add-tag":
                        await FAQAddTagCommand(command);
                        break;
                    default:
                        await command.RespondAsync($"{command.CommandName} is not implemented yet.");
                        break;
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private async Task FAQAddTagCommand(SocketSlashCommand command)
        {
            if (await IsTruePirateAsync(command) is false)
                return;
            if(int.TryParse(command.Data.Options.FirstOrDefault(op => op.Name == "id")?.Value.ToString(), out int faqId))
            {
                string tag = command.Data.Options.FirstOrDefault(op => op.Name == "tag")?.Value.ToString();
                var faq = await _db.FAQs
                    .Where(f => f.Id == faqId)
                    .Include(f => f.Tags)
                    .FirstOrDefaultAsync();
                faq.Tags.Add(new FAQTag() { Tag = tag });
                await _db.SaveChangesAsync();
                await command.RespondAsync($"Added Tag {tag} to FAQ Entry #{faq.Id} {faq.Name}.");
            }
            else
            {
                await command.RespondAsync($"Couldn't find FAQ.");
            }
        }
        private async Task FAQHelpCommand(SocketSlashCommand command)
        {
            await command.RespondAsync($"The FAQ bot was built by the Perilous Pirates guild member WegHuZ.\n" +
                $"\\Commands are:\n" +
                $"\\faq\n" +
                $"\\faq-help\n" +
                $"\\faq-list\n" +
                $"(TRUE PIRATE)\\faq-add\n" +
                $"(TRUE PIRATE)\\faq-edit\n" +
                $"(TRUE PIRATE)\\faq-add-tag");
        }

        private async Task<bool> IsTruePirateAsync(SocketSlashCommand command)
        {
            var piratesGuild = _client.GetGuild(PIRATES_GUILD_ID);
            var piratesUser = piratesGuild.Users.FirstOrDefault(user => user.Id == command.User.Id);
            if (piratesUser == null)
            {
                await command.RespondAsync($"Couldn't find user.");
                return false;
            };
            bool allowed = piratesUser.Roles.Any(role => role.Id == ROLE_TRUE_PIRATE_ID);

            if (allowed is false)
            {
                await command.RespondAsync($"You're not a TRUE pirate.");
            };
            return allowed;
        }

        private async Task FAQAddCommand(SocketSlashCommand command)
        {
            if(await IsTruePirateAsync(command) is false)
                return;
            try
            {
                string name = command.Data.Options.FirstOrDefault(op => op.Name == "name")?.Value.ToString();
                string desc = command.Data.Options.FirstOrDefault(op => op.Name == "description")?.Value.ToString();
                string image = command.Data.Options.FirstOrDefault(op => op.Name == "image")?.Value.ToString();
                string link = command.Data.Options.FirstOrDefault(op => op.Name == "link")?.Value.ToString();
                string tags = command.Data.Options.FirstOrDefault(op => op.Name == "tags")?.Value.ToString();
                List<FAQTag> FAQTags = new();
                foreach(string tag in tags.Split(','))
                {
                    FAQTags.Add(new() { Tag = tag.Trim() });
                }
                var faq = new FAQEntry(name, link, image, desc, FAQTags);
                await _db.AddAsync(faq);
                await _db.SaveChangesAsync();
                await command.RespondAsync($"Added FAQ Entry #{faq.Id}:\n**[{faq.Name}]({faq.Link})**:\n{faq.Description}\n[Image]({faq.Image})");
                _faq.Add(faq);
            }
            catch(Exception e)
            {
                await command.RespondAsync($"Tried to Add FAQ but failed with error: {e.Message}\n");
                Console.WriteLine($"Tried to Add FAQ but failed with error: {e.Message}\n");
            }
        }

        private async Task FAQListCommand(SocketSlashCommand command)
        {
            StringBuilder msg = new();
            foreach(FAQEntry faq in _faq)
            {
                msg.Append($"Faq {faq.Id}: {faq.Name}\n");
                foreach(FAQTag tag in faq.Tags)
                {
                    msg.Append($"\tTag {tag.Id}: {tag.Tag}\n");
                }
                msg.Append("\n");
            }
            await command.RespondAsync(msg.ToString());
        }

        private async Task FAQCommand(SocketSlashCommand command)
        {
            var data = command.Data.Options.FirstOrDefault(op => op.Name == "data")?.Value;
            if (data is null)
            {
                return;
            }
            var faq = _faq.FirstOrDefault(faq => faq.Tags.Any(tag => tag.Tag.ToLower().Contains(data.ToString()!.ToLower())));
            if (faq is not null)
            {
                await command.RespondAsync($"**[{faq.Name}]({faq.Link})**:\n{faq.Description}\n[Image]({faq.Image})");
            }
            else
            {
                await command.RespondAsync($"FAQ entry not found!");
            }
        }

        private async Task InitiateSlashCommands()
        {
            var piratesGuild = _client.GetGuild((ulong)940071768679395369);
            var commands = await _client.GetGlobalApplicationCommandsAsync();
            foreach(var command in commands)
            {
                await command.DeleteAsync();
            }
            var guildCommand = new SlashCommandBuilder();
            guildCommand.WithName("faq-help");
            guildCommand.WithDescription("What is the FAQ Bot?");
            await piratesGuild.CreateApplicationCommandAsync(guildCommand.Build());

            guildCommand = new SlashCommandBuilder();
            guildCommand.WithName("faq-list");
            guildCommand.WithDescription("List all FAQs.");
            await piratesGuild.CreateApplicationCommandAsync(guildCommand.Build());

            guildCommand = new SlashCommandBuilder();
            guildCommand.WithName("faq");
            guildCommand.WithDescription("Search the FAQ with a tag.");
            guildCommand.AddOption("data", ApplicationCommandOptionType.String, "The data to look for in the FAQ database.", true);
            await piratesGuild.CreateApplicationCommandAsync(guildCommand.Build());

            guildCommand = new SlashCommandBuilder();
            guildCommand.WithName("faq-add");
            guildCommand.WithDescription("(TRUE PIRATE) Add a new FAQ entry.");
            guildCommand.AddOptions(new SlashCommandOptionBuilder[]
            {
                new()
                {
                    Name = "name",
                    Description = "The name of the FAQ Entry.",
                    Type = ApplicationCommandOptionType.String,
                    IsRequired = true,
                },
                new()
                {
                    Name = "description",
                    Description = "The Description of the FAQ Entry.",
                    Type = ApplicationCommandOptionType.String,
                    IsRequired = true,
                },
                new()
                {
                    Name = "link",
                    Description = "A link to elaborate on the FAQ Entry.",
                    Type = ApplicationCommandOptionType.String,
                    IsRequired = false,
                },
                new()
                {
                    Name = "image",
                    Description = "A URI to an image for the FAQ.",
                    Type = ApplicationCommandOptionType.String,
                    IsRequired = false,
                },
                new()
                {
                    Name = "tags",
                    Description = "Comma separated tags for example: \"Hero, Heroes, Class\"",
                    Type = ApplicationCommandOptionType.String,
                    IsRequired = true,
                }
            });
            await piratesGuild.CreateApplicationCommandAsync(guildCommand.Build());

            guildCommand = new SlashCommandBuilder();
            guildCommand.WithName("faq-add-tag");
            guildCommand.WithDescription("(TRUE PIRATE) Add a FAQ Tag.");
            guildCommand.AddOptions(new SlashCommandOptionBuilder[]
            {
                new()
                {
                    Name = "id",
                    Description = "Id of the FAQ to add a TAG to.",
                    Type = ApplicationCommandOptionType.Integer,
                    IsRequired = true,
                },
                new()
                {
                    Name = "tag",
                    Description = "The tag to add.",
                    Type = ApplicationCommandOptionType.String,
                    IsRequired = true,
                },
            });
            await piratesGuild.CreateApplicationCommandAsync(guildCommand.Build());
            guildCommand = new SlashCommandBuilder();
            guildCommand.WithName("faq-edit");
            guildCommand.WithDescription("(TRUE PIRATE) Edit a FAQ.");
            guildCommand.AddOptions(new SlashCommandOptionBuilder[]
            {
                new()
                {
                    Name = "id",
                    Description = "Id of the FAQ to edit.",
                    Type = ApplicationCommandOptionType.String,
                    IsRequired = true,
                },
                new()
                {
                    Name = "name",
                    Description = "The name of the FAQ Entry.",
                    Type = ApplicationCommandOptionType.String,
                    IsRequired = true,
                },
                new()
                {
                    Name = "description",
                    Description = "The Description of the FAQ Entry.",
                    Type = ApplicationCommandOptionType.String,
                    IsRequired = true,
                },
                new()
                {
                    Name = "link",
                    Description = "A link to elaborate on the FAQ Entry.",
                    Type = ApplicationCommandOptionType.String,
                    IsRequired = false,
                },
                new()
                {
                    Name = "image",
                    Description = "A URI to an image for the FAQ.",
                    Type = ApplicationCommandOptionType.String,
                    IsRequired = true,
                },
                new()
                {
                    Name = "tags",
                    Description = "Comma separated tags for example: \"Hero, Heroes, Class\"",
                    Type = ApplicationCommandOptionType.String,
                    IsRequired = true,
                }
            });
            await piratesGuild.CreateApplicationCommandAsync(guildCommand.Build());
        }

        private Task Log(LogMessage message)
        {
            if (message.Exception is CommandException cmdException)
            {
                Console.WriteLine($"[Command/{message.Severity}] {cmdException.Command.Aliases.First()}"
                    + $" failed to execute in {cmdException.Context.Channel}.");
                Console.WriteLine(cmdException);
            }
            else
                Console.WriteLine($"[General/{message.Severity}] {message}");
            if (message.Message == "Ready")
                Console.WriteLine("To input new FAQ entries hit enter.");
            return Task.CompletedTask;
        }
    }
}
