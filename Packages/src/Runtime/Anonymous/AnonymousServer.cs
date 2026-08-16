using oojjrs.oplat.anonymous.controllers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace oojjrs.oplat.anonymous
{
    internal sealed class AnonymousServer
    {
        private readonly HashSet<AnonymousServerConnection> Connections = new();
        private readonly object ConnectionsLock = new();
        private readonly CancellationTokenSource LifetimeCancellationSource = new();
        private readonly CancellationToken LifetimeCancellationToken;
        private readonly TcpListener Listener = new(IPAddress.Loopback, AnonymousTransport.Port);
        private readonly SemaphoreSlim RequestSemaphore = new(1, 1);
        private readonly AnonymousServerRoom.State RoomState = new();
        private readonly SemaphoreSlim StartSemaphore = new(1, 1);
        private readonly object StateLock = new();

        private bool _isOwner;
        private bool _isShutdown;
        private bool _isStarted;

        internal AnonymousServer()
        {
            LifetimeCancellationToken = LifetimeCancellationSource.Token;
        }

        internal static async Task<T> ReadJsonAsync<T>(string content, string invalidMessage, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(content))
                throw new FormatException(invalidMessage);

            try
            {
                return await AnonymousTransport.FromJsonAsync<T>(content, cancellationToken);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(invalidMessage, exception);
            }
        }

        private async Task AcceptAsync()
        {
            try
            {
                while (LifetimeCancellationToken.IsCancellationRequested == false)
                {
                    var client = await Listener.AcceptTcpClientAsync();
                    try
                    {
                        client.NoDelay = true;
                        var connection = new AnonymousServerConnection(client);
                        lock (ConnectionsLock)
                            Connections.Add(connection);

                        _ = RunConnectionAsync(connection);
                    }
                    catch (ObjectDisposedException)
                    {
                        client.Close();
                    }
                    catch (SocketException)
                    {
                        client.Close();
                    }
                    catch
                    {
                        client.Close();
                        throw;
                    }
                }
            }
            catch (ObjectDisposedException) when (LifetimeCancellationToken.IsCancellationRequested)
            {
            }
            catch (SocketException) when (LifetimeCancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Shutdown();
            }
        }

        private async Task<AnonymousServerResponse> CreateResponseAsync(AnonymousServerConnection connection, AnonymousTransport.Frame frame)
        {
            if (frame.Operation == AnonymousTransport.OperationEnum.Authenticate)
            {
                if (connection.Session != null)
                    return AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.Conflict);

                connection.Session = await AnonymousServerAuthenticate.RunAsync(frame.Content, LifetimeCancellationToken);
                return AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.Success);
            }

            var authenticatedSession = connection.Session;
            if (authenticatedSession == null)
                return AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.Unauthenticated);

            return frame.Operation switch
            {
                AnonymousTransport.OperationEnum.CreateRoom => await AnonymousServerCreateRoom.RunAsync(frame.Content, RoomState, authenticatedSession, LifetimeCancellationToken),
                AnonymousTransport.OperationEnum.ExitRoom => await AnonymousServerExitRoom.RunAsync(frame.Content, RoomState, authenticatedSession, LifetimeCancellationToken),
                AnonymousTransport.OperationEnum.GetRooms => await AnonymousServerGetRooms.RunAsync(RoomState, LifetimeCancellationToken),
                AnonymousTransport.OperationEnum.JoinRoom => await AnonymousServerJoinRoom.RunAsync(frame.Content, RoomState, authenticatedSession, LifetimeCancellationToken),
                AnonymousTransport.OperationEnum.UpdatePlayer => await AnonymousServerUpdatePlayer.RunAsync(frame.Content, RoomState, authenticatedSession, LifetimeCancellationToken),
                AnonymousTransport.OperationEnum.UpdateRoom => await AnonymousServerUpdateRoom.RunAsync(frame.Content, RoomState, authenticatedSession, LifetimeCancellationToken),
                _ => AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.UnsupportedOperation),
            };
        }

        private async Task RespondAsync(AnonymousServerConnection connection, AnonymousTransport.Frame frame)
        {
            AnonymousServerResponse response;
            await RequestSemaphore.WaitAsync(LifetimeCancellationToken);
            try
            {
                try
                {
                    response = await CreateResponseAsync(connection, frame);
                }
                catch (FormatException)
                {
                    response = AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.InvalidRequest);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    response = AnonymousServerResponse.Create(AnonymousTransport.ResultCodeEnum.InternalError);
                }
            }
            finally
            {
                RequestSemaphore.Release();
            }

            var responseFrame = new AnonymousTransport.Frame(AnonymousTransport.FrameTypeEnum.ControlResponse, frame.RequestId, frame.Operation, response.ResultCode, response.Content);
            await connection.SendAsync(responseFrame, LifetimeCancellationToken);
        }

        private async Task RunConnectionAsync(AnonymousServerConnection connection)
        {
            try
            {
                while (LifetimeCancellationToken.IsCancellationRequested == false)
                {
                    var frame = await connection.ReadAsync(LifetimeCancellationToken);
                    if (frame == null)
                        return;

                    if ((frame.Type != AnonymousTransport.FrameTypeEnum.ControlRequest) || (frame.RequestId < 1) || (frame.ResultCode != AnonymousTransport.ResultCodeEnum.None))
                        throw new FormatException("Invalid anonymous control request.");

                    await RespondAsync(connection, frame);
                }
            }
            catch (EndOfStreamException)
            {
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException)
            {
            }
            catch (OperationCanceledException) when (LifetimeCancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                lock (ConnectionsLock)
                    Connections.Remove(connection);

                connection.Shutdown();
            }
        }

        internal void Shutdown()
        {
            lock (StateLock)
            {
                if (_isShutdown)
                    return;

                _isShutdown = true;
                LifetimeCancellationSource.Cancel();
                if (_isOwner)
                {
                    Listener.Stop();
                    _isOwner = false;
                }
            }

            AnonymousServerConnection[] connections;
            lock (ConnectionsLock)
            {
                connections = new AnonymousServerConnection[Connections.Count];
                Connections.CopyTo(connections);
                Connections.Clear();
            }

            foreach (var connection in connections)
                connection.Shutdown();
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

                    if (_isStarted)
                        return;

                    try
                    {
                        Listener.Start();
                    }
                    catch (SocketException exception) when (exception.SocketErrorCode == SocketError.AddressAlreadyInUse)
                    {
                        _isStarted = true;
                        return;
                    }

                    _isOwner = true;
                    _isStarted = true;
                    _ = Task.Run(AcceptAsync);
                }
            }
            finally
            {
                StartSemaphore.Release();
            }
        }
    }
}
