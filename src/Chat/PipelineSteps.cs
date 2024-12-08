﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

using Microsoft.Extensions.AI;

namespace Chat;
internal static class PipelineSteps
{
    public static ChatClientBuilder UseLanguage(this ChatClientBuilder builder, string language)
        => builder.Use(i => new LanguageChatClient(i, language));

    public static ChatClientBuilder UseRateLimit(this ChatClientBuilder builder, TimeSpan window)
        => builder.Use(i => new RateLimitChatClient(i, window));

    private class LanguageChatClient(IChatClient innerClient, string language) : DelegatingChatClient(innerClient)
    {
        public override async Task<ChatCompletion> CompleteAsync(IList<ChatMessage> chatMessages, ChatOptions? options = null,
            CancellationToken cancellationToken = new CancellationToken())
        {
            var message = new ChatMessage(ChatRole.User, $"Rispondi sempre in {language}");

            try
            {
                chatMessages.Add(message);
                return await base.CompleteAsync(chatMessages, options, cancellationToken);
            }
            finally
            {
                chatMessages.Remove(message);
            }
        }
    }

    private class RateLimitChatClient(IChatClient innerClient, TimeSpan window) : DelegatingChatClient(innerClient)
    {

        private readonly RateLimiter _rateLimit = new FixedWindowRateLimiter(new()
        {
            Window = window,
            QueueLimit = 1,
            PermitLimit = 1
        });

        public override async Task<ChatCompletion> CompleteAsync(IList<ChatMessage> chatMessages, ChatOptions? options = null,
            CancellationToken cancellationToken = new CancellationToken())
        {
            var lease = _rateLimit.AttemptAcquire();
            if (!lease.IsAcquired)
            {
                return new(new ChatMessage(ChatRole.Assistant, "Troppe richieste. Riprova più tardi"));
            }

            return await base.CompleteAsync(chatMessages, options, cancellationToken);
        }
    }
}
