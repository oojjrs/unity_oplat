using oojjrs.oplat.anonymous.controllers;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace oojjrs.oplat.anonymous
{
    internal sealed class AnonymousServer
    {
        private const string Address = "http://127.0.0.1:45831/";
        internal const string ApiAuthenticate = "authenticate";
        internal const string ApiCreateRoom = "create_room";
        internal const string ApiExitRoom = "exit_room";
        internal const string ApiGetRooms = "get_rooms";
        internal const string ApiHealth = "health";
        internal const string ApiJoinRoom = "join_room";
        internal const string HealthResponse = "oojjrs.oplat.anonymous/8";

        private readonly HttpListener Listener = new();
        private readonly AnonymousServerRoom.State RoomState = new();
        private readonly AnonymousServerSession.State SessionState = new();
        private readonly SemaphoreSlim StartSemaphore = new(1, 1);
        private readonly object StateLock = new();

        private bool _isOwner;
        private bool _isShutdown;

        internal AnonymousServer()
        {
            Listener.Prefixes.Add(Address);
        }

        internal static string GetUri(string relativePath)
        {
            return Address + relativePath;
        }

        internal static async Task<string> ReadContentAsync(HttpListenerRequest request)
        {
            if (request.HasEntityBody == false)
                throw new FormatException("Anonymous request body is empty.");

            try
            {
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                    return await reader.ReadToEndAsync();
            }
            catch (ArgumentException exception)
            {
                throw new FormatException("Invalid anonymous request encoding.", exception);
            }
        }

        private async void ListenAsync()
        {
            try
            {
                while (Listener.IsListening)
                {
                    var context = await Listener.GetContextAsync().ConfigureAwait(false);

                    try
                    {
                        await RespondAsync(context);
                    }
                    catch (HttpListenerException) when (Listener.IsListening)
                    {
                    }
                    catch (IOException) when (Listener.IsListening)
                    {
                    }
                    catch (ObjectDisposedException) when (Listener.IsListening)
                    {
                    }
                    catch (Exception exception) when (Listener.IsListening)
                    {
                        Debug.LogException(exception);
                    }
                }
            }
            catch (HttpListenerException) when (Listener.IsListening == false)
            {
            }
            catch (IOException) when (Listener.IsListening == false)
            {
            }
            catch (ObjectDisposedException) when (Listener.IsListening == false)
            {
            }
        }

        private async Task RespondAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            response.StatusCode = (int)HttpStatusCode.InternalServerError;
            try
            {
                switch (request.Url.AbsolutePath[1..])
                {
                    case ApiAuthenticate:
                        await AnonymousServerAuthenticate.RunAsync(request, response, SessionState);
                        break;
                    case ApiHealth:
                        await AnonymousServerHealth.RunAsync(request, response);
                        break;
                    case ApiGetRooms:
                        await AnonymousServerGetRooms.RunAsync(request, response, RoomState, SessionState);
                        break;
                    case ApiCreateRoom:
                        await AnonymousServerCreateRoom.RunAsync(request, response, RoomState, SessionState);
                        break;
                    case ApiExitRoom:
                        await AnonymousServerExitRoom.RunAsync(request, response, RoomState, SessionState);
                        break;
                    case ApiJoinRoom:
                        await AnonymousServerJoinRoom.RunAsync(request, response, RoomState, SessionState);
                        break;
                    default:
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                        break;
                }
            }
            finally
            {
                response.Close();
            }
        }

        internal async Task StartAsync(CancellationToken cancellationToken)
        {
            await StartSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                lock (StateLock)
                {
                    if (_isShutdown)
                        throw new ObjectDisposedException(nameof(AnonymousServer));

                    if (_isOwner)
                        return;

                    Listener.Start();
                    _isOwner = true;

                    ListenAsync();
                }
            }
            finally
            {
                StartSemaphore.Release();
            }
        }

        internal void Shutdown()
        {
            lock (StateLock)
            {
                if (_isShutdown == false)
                {
                    _isShutdown = true;
                    if (_isOwner)
                    {
                        try
                        {
                            Listener.Stop();
                        }
                        finally
                        {
                            _isOwner = false;
                        }
                    }
                }
            }
        }
    }
}
