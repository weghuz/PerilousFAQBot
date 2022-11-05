# Perilous Pirates FAQ Bot

Appsettings.json should be in the following format:
Both "DiscordToken" and "FAQDB" are required.
DiscordToken should be the Token to the Discord bot that you want to run the Bot on.

It needs Bot and Application.Commands rights to work.

FAQDB should be a connection string to a Postgres SQL server in the format: "Host=localhost; Database=postgres; Username=postgres; Password=password;"

}
```json
{
  "DiscordToken": "",
  "ConnectionStrings": {
    "FAQDB": ""
  }
}
```

to run the PP bot you need to
1. dotnet restore
2. Open the "Package Manager Console" in View -> Other Windows -> Package Manager Console
3. Run update-database
4. dotnet run