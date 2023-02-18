using System.Text;
using OpenAI_API;
using OpenAI_API.Completions;
using OpenAI_API.Models;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TelegramBotConsole
{
    class Program
    {
        private static TelegramBotClient bot = null!;
        private static OpenAIAPI _openAiApi = null!;
        private static CancellationTokenSource _cts = null!;
        private const string openAIKey = "";
        private const string telegramBotToken = "";

        static async Task Main(string[] args)
        {
            // 初始化 Telegram Bot API
            bot = new TelegramBotClient(telegramBotToken);
            var me = await bot.GetMeAsync();
            Console.WriteLine($"Hello, I am {me.Username}!");

            // 初始化 OpenAI API
            var apiKeys = new APIAuthentication(openAIKey);
             _openAiApi = new OpenAIAPI(apiKeys);

             _cts = new CancellationTokenSource();

            // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()// receive all update types
            };

            bot.StartReceiving(
                updateHandler: HandleUpdateAsync,
                pollingErrorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: _cts.Token
            );

            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
            _cts.CancelAfter(0);
        }

        private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            // Only process Message updates: https://core.telegram.org/bots/api#message
            if (update.Message is not {} message)
                return;
            // Only process text messages
            if (message.Text is not {} messageText)
                return;

            var chatId = message.Chat.Id;

            Console.WriteLine($"{DateTime.Now:G}Received a '{messageText}' message in chat {chatId}({message.Chat.Username}).");

            if (messageText == "/start")
            {
                await bot.SendTextMessageAsync(
                    chatId: chatId,
                    text: $"Hi, {message.Chat.Username} \n我是 ChatGPT Bot",
                    cancellationToken: _cts.Token); 
                return;
            }

            var builder = new StringBuilder();
            await foreach (var token in _openAiApi.Completions.StreamCompletionEnumerableAsync(
                               new CompletionRequest(messageText, Model.DavinciText, 1000, 0.5, presencePenalty: 0.1, frequencyPenalty: 0.1)).WithCancellation(_cts.Token))
            {
                builder.Append(token);
            }
            
            Console.WriteLine($"AI Response `{builder}`");
            
            await bot.SendTextMessageAsync(
                chatId: chatId,
                text: builder.ToString(),
                cancellationToken: _cts.Token);
        }

        static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }
    }
}