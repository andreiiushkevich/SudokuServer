using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using SudokuServer.Dtos;
using SudokuServer.Models;
using SudokuServer.Services;

namespace SudokuServer
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<SudokuBoard>();
            services.AddSingleton<SudokuSolver>();
            services.AddSingleton<SudokuGameProcessor>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, SudokuGameProcessor gameProcessor)
        {
            var webSocketOptions = new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromSeconds(120),
                ReceiveBufferSize = 4 * 1024
            };

            app.UseWebSockets(webSocketOptions);
            gameProcessor.GenerateRandomPuzzle();

            app.Use(async (context, next) =>
            {
                if (context.Request.Path == "/board")
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        var webSocket = await context.WebSockets.AcceptWebSocketAsync();

                        gameProcessor.CellUpdated += async (sender, cell) =>
                        {
                            var cells = new[] {cell};
                            var strCells = JsonConvert.SerializeObject(cells);
                            var binCells = Encoding.UTF8.GetBytes(strCells);

                            try
                            {
                                await webSocket.SendAsync(new ArraySegment<byte>(binCells), WebSocketMessageType.Text,
                                    true, CancellationToken.None);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                            }
                        };

                        gameProcessor.GameCompleted += async (sender, winner) =>
                        {
                            await SendAllCellsAsync(webSocket, gameProcessor, CancellationToken.None);
                        };

                        var participant = new Participant
                        {
                            Id = context.Request.Cookies["user-id"] != null
                                ? Guid.Parse(context.Request.Cookies["user-id"])
                                : Guid.Empty,
                            Name = context.Request.Cookies["user-name"]
                        };

                        await SendAllCellsAsync(webSocket, gameProcessor, CancellationToken.None);
                        await ProcessMessageAsync(webSocket, participant, gameProcessor, CancellationToken.None);
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                    }
                }
                else if (context.Request.Path == "/winner")
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        var webSocket = await context.WebSockets.AcceptWebSocketAsync();

                        gameProcessor.GameCompleted += async (sender, participant) =>
                        {
                            var strParticipant = JsonConvert.SerializeObject(participant);
                            var binParticipant = Encoding.UTF8.GetBytes(strParticipant);

                            try
                            {
                                await webSocket.SendAsync(new ArraySegment<byte>(binParticipant),
                                    WebSocketMessageType.Text,
                                    true, CancellationToken.None);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                            }
                        };

                        var buffer = new byte[1024 * 4];

                        while (webSocket.State == WebSocketState.Open)
                            try
                            {
                                await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                                var message = Encoding.UTF8.GetString(buffer);
                                Console.WriteLine(message);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                                return;
                            }
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                    }
                }
                else
                {
                    await next();
                }
            });
        }

        private static async Task ProcessMessageAsync(WebSocket webSocket, Participant participant,
            SudokuGameProcessor gameProcessor, CancellationToken cancellationToken)
        {
            var buffer = new byte[1024 * 4];

            while (!cancellationToken.IsCancellationRequested || webSocket.State == WebSocketState.Open)
            {
                var rawMessage = new List<byte>();
                WebSocketReceiveResult result;

                do
                {
                    try
                    {
                        result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        return;
                    }

                    rawMessage.AddRange(buffer);
                } while (!result.EndOfMessage);

                var strMessage = Encoding.UTF8.GetString(rawMessage.ToArray());
                var receivedCell = JsonConvert.DeserializeObject<CellDto>(strMessage);

                gameProcessor.SetCellValue(receivedCell, participant);
            }
        }

        private static async Task SendAllCellsAsync(WebSocket webSocket, SudokuGameProcessor gameProcessor,
            CancellationToken cancellationToken)
        {
            var cells = gameProcessor.FilledCells;
            var serializedCells = JsonConvert.SerializeObject(cells);
            var binCells = Encoding.UTF8.GetBytes(serializedCells);
            try
            {
                await webSocket.SendAsync(new ArraySegment<byte>(binCells), WebSocketMessageType.Text, true,
                    cancellationToken);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}