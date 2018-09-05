﻿using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Newtonsoft.Json;
using SignalRServiceExtension.Tests.Utils;
using Xunit;

namespace SignalRServiceExtension.Tests
{
    public class AzureSignalRClientTests
    {
        [Fact]
        public void AzureSignalRClient_ParsesConnectionString()
        {
            var azureSignalR = new AzureSignalRClient("Endpoint=https://foo.service.signalr.net;AccessKey=/abcdefghijklmnopqrstu/v/wxyz11111111111111=;", null);
            Assert.Equal("https://foo.service.signalr.net", azureSignalR.BaseEndpoint);
            Assert.Equal("/abcdefghijklmnopqrstu/v/wxyz11111111111111=", azureSignalR.AccessKey);
        }

        [Fact]
        public void AzureSignalRClient_GetClientConnectionInfo_ReturnsValidInfo()
        {
            var azureSignalR = new AzureSignalRClient("Endpoint=https://foo.service.signalr.net;AccessKey=/abcdefghijklmnopqrstu/v/wxyz11111111111111=;", null);

            var info = azureSignalR.GetClientConnectionInfo("chat");

            const string expectedEndpoint = "https://foo.service.signalr.net:5001/client/?hub=chat";
            TestHelpers.EnsureValidAccessKey(
                audience: expectedEndpoint,
                signingKey: "/abcdefghijklmnopqrstu/v/wxyz11111111111111=", 
                accessKey: info.AccessKey);
            Assert.Equal(expectedEndpoint, info.Endpoint);
        }

        [Fact]
        public void AzureSignalRClient_GetServerConnectionInfo_ReturnsValidInfo()
        {
            var azureSignalR = new AzureSignalRClient("Endpoint=https://foo.service.signalr.net;AccessKey=/abcdefghijklmnopqrstu/v/wxyz11111111111111=;", null);

            var info = azureSignalR.GetServerConnectionInfo("chat");

            const string expectedEndpoint = "https://foo.service.signalr.net:5002/api/v1-preview/hub/chat";
            TestHelpers.EnsureValidAccessKey(
                audience: expectedEndpoint,
                signingKey: "/abcdefghijklmnopqrstu/v/wxyz11111111111111=", 
                accessKey: info.AccessKey);
            Assert.Equal(expectedEndpoint, info.Endpoint);
        }

        [Fact]
        public async Task SendMessage_CallsAzureSignalRService()
        {
            var connectionString = "Endpoint=https://foo.service.signalr.net;AccessKey=/abcdefghijklmnopqrstu/v/wxyz11111111111111=;";
            var hubName = "chat";
            var requestHandler = new FakeHttpMessageHandler();
            var httpClient = new HttpClient(requestHandler);
            var azureSignalR = new AzureSignalRClient(connectionString, httpClient);

            await azureSignalR.SendMessage(hubName, new SignalRMessage
            {
                Target = "newMessage",
                Arguments = new object[] { "arg1", "arg2" }
            });

            const string expectedEndpoint = "https://foo.service.signalr.net:5002/api/v1-preview/hub/chat";
            var request = requestHandler.HttpRequestMessage;
            Assert.Equal("application/json", request.Content.Headers.ContentType.MediaType);
            Assert.Equal(expectedEndpoint, request.RequestUri.AbsoluteUri);

            var actualRequestBody = JsonConvert.DeserializeObject<SignalRMessage>(await request.Content.ReadAsStringAsync());
            Assert.Equal("newMessage", actualRequestBody.Target);
            Assert.Equal("arg1", actualRequestBody.Arguments[0]);
            Assert.Equal("arg2", actualRequestBody.Arguments[1]);

            var authorizationHeader = request.Headers.Authorization;
            Assert.Equal("Bearer", authorizationHeader.Scheme);
            TestHelpers.EnsureValidAccessKey(
                audience: expectedEndpoint,
                signingKey: "/abcdefghijklmnopqrstu/v/wxyz11111111111111=", 
                accessKey: authorizationHeader.Parameter);
        }

        private class FakeHttpMessageHandler : HttpMessageHandler
        {
            public HttpRequestMessage HttpRequestMessage { get; private set; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                HttpRequestMessage = request;
                var response = new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
                response.Content = new StringContent("", Encoding.UTF8, "application/json");
                return Task.FromResult(response);
            }
        }
    }
}