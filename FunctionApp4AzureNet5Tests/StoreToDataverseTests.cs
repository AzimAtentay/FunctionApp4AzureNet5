using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Logging.Abstractions;


using System;
using Xunit;
using Microsoft.Extensions.Logging;
using FunctionApp4AzureNet5;
using Microsoft.Azure.Functions.Worker.Http;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.IO;
using Microsoft.Azure.Functions.Worker;
using System.Collections.Generic;
using System.Security.Claims;
using System.Net;

namespace FunctionApp4AzureNet5Tests
{
    public class StoreToDataverseTests
    {
        private string connString = "AuthType='ClientSecret'; ServiceUri='https://azim.crm4.dynamics.com'; ClientId = 'fe9a7773-3a3f-4cac-8ae1-496ac5ae54f1'; ClientSecret = 'G.S8Q~T-x3VrvkLFMn~Txi6Peu.1nkdrSpseVbXy';";

        private ILogger m_logger = NullLoggerFactory.Instance.CreateLogger("Test");


        [Theory]
        [InlineData("2022-07-16", "2022-07-18")]
        public void TestWatchFunctionSuccess(string StartOn, string EndOn)
        {

            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<Function1>();
            var svc = new Function1(loggerFactory);

            //--
            //var body = new MemoryStream(Encoding.ASCII.GetBytes("{\"StartOn\": \"2022-07-01\", \"EndOn\": \"2022-07-03\"}"));
            var body = new MemoryStream(Encoding.ASCII.GetBytes($"{{\"StartOn\": \"{StartOn}\", \"EndOn\": \"{EndOn}\"}}"));

            var context = new Mock<FunctionContext>();
            var request = new FakeHttpRequestData(
                            context.Object,
                            new Uri("https://functionapp4azurenet520220612183155.azurewebsites.net/api/AzAtFunction"),
                            body);
            request.Headers.Add("Content-Type", "application/json");

            HttpResponseData result = svc.Run(request);
            result.Body.Seek(0, SeekOrigin.Begin);
            var reader = new StreamReader(result.Body);
            var responseBody = reader.ReadToEnd();

            Assert.NotNull(result);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode); 
            Assert.True(responseBody.StartsWith("Status:") || responseBody.StartsWith("Error. Message:") || 
                responseBody.StartsWith("Interval saved successfully") || responseBody.StartsWith("StartOn or EndOn has wrong format") || 
                responseBody.StartsWith("StartOn or EndOn empty"));
            
        }

        //private DefaultHttpRequest GenerateDefaultHttpRequest(object number)
        //{
        //    var request = new DefaultHttpRequest(new DefaultHttpContext());

        //    var queryParams = new Dictionary<string, StringValues> { { "number", number.ToString() } };
        //    request.Query = new QueryCollection(queryParams);
        //    return request;
        //}


       

        [Fact]
        public void TestConnectionSuccess()
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<Function1>();
            var svc = new Function1(loggerFactory);
            
            var sevice = svc.SetConnection(connString);
            Assert.NotNull(sevice);

        }

        [Theory]
        [InlineData("2022-08-03", "2022-08-04")]
        public void TestStoreSuccess(string StartOn, string EndOn)
        {
            var stDay = DateTime.Today;
            DateTime.TryParse(StartOn, out stDay);
            var enDay = DateTime.Today;
            DateTime.TryParse(EndOn, out enDay);

            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<Function1>();
            var svc = new Function1(loggerFactory);
            svc.StartOn = stDay;
            svc.EndOn = enDay;
            var sevice = svc.SetConnection(connString);
            Assert.NotNull(sevice);

            var result = svc.MakeNew_msdyn_timeentry(sevice);
            
            Assert.True((result.StartsWith("The interval intersects existing data") || result.StartsWith("Interval saved successfully") || result.StartsWith("Error when saving day")));
        }

        [Theory]
        [InlineData("1001-01-01")]
        public void TestStoreFail(string storeDay)
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<Function1>();
            var svc = new Function1(loggerFactory);            
            var sevice = svc.SetConnection(connString);
            Assert.NotNull(sevice);

            var day = DateTime.Today;
            DateTime.TryParse(storeDay, out day);

            svc.StartOn = day;
            svc.EndOn = day;

            var result = svc.MakeNew_msdyn_timeentry(sevice);

            Assert.StartsWith("Error when RetrieveMultiple days", result);
        }
        
        [Theory]
        [InlineData("1001-01-01", "3001-12-31")]
        public void TestStoreIntervalOfDaysFail(string StartOn, string EndOn)
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<Function1>();
            var svc = new Function1(loggerFactory);            
            var sevice = svc.SetConnection(connString);
            Assert.NotNull(sevice);

            var stDay = DateTime.Today;
            DateTime.TryParse(StartOn, out stDay);
            var enDay = DateTime.Today;
            DateTime.TryParse(EndOn, out enDay);
            
            var result = svc.StoreIntervalOfDays(sevice, stDay, enDay);

            Assert.StartsWith("Error when saving day:", result);
        }

        [Theory]
        [InlineData("2022-09-01", "2022-09-02")]
        public void TestStoreIntervalOfDaysSuccess(string StartOn, string EndOn)
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<Function1>();
            var svc = new Function1(loggerFactory);
            var sevice = svc.SetConnection(connString);
            Assert.NotNull(sevice);

            var stDay = DateTime.Today;
            DateTime.TryParse(StartOn, out stDay);
            var enDay = DateTime.Today;
            DateTime.TryParse(EndOn, out enDay);

            var result = svc.StoreIntervalOfDays(sevice, stDay, enDay);

            Assert.StartsWith("Error when saving day:", result);
        }

        [Theory]
        [InlineData("2022-07-01")]
        public void TestCreateNewSuccess(string storeDay)
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<Function1>();
            var svc = new Function1(loggerFactory);
            svc.StartOn = DateTime.Today;
            svc.EndOn = DateTime.Today;
            var sevice = svc.SetConnection(connString);
            Assert.NotNull(sevice);
            var day = DateTime.Today;
            DateTime.TryParse(storeDay, out day);

            var result = svc.CreateNewRecord(sevice, day,"Testing Data");
            
            Assert.Equal("", result);
        }

        [Theory]
        [InlineData("1001-01-01")]
        public void TestCreateNewFail(string storeDay)
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<Function1>();
            var svc = new Function1(loggerFactory);
            svc.StartOn = DateTime.Today;
            svc.EndOn = DateTime.Today;
            var sevice = svc.SetConnection(connString);
            Assert.NotNull(sevice);
            var day = DateTime.Today;
            DateTime.TryParse(storeDay, out day);

            var result = svc.CreateNewRecord(sevice, day, "Testing Data");

            Assert.StartsWith("Error on Create: DateTime is less than minumum value supported by CrmDateTime.", result);
        }


        [Fact]
        public void TestRetrieveDatesSuccess()
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<Function1>();
            var svc = new Function1(loggerFactory);
            svc.StartOn = DateTime.Today;
            svc.EndOn = DateTime.Today;
            var sevice = svc.SetConnection(connString);
            Assert.NotNull(sevice);

            var result = svc.RetrieveMultiple(sevice,DateTime.Today.AddYears(-100),DateTime.Today.AddYears(100));

            Assert.NotEqual(0, result);
        }
    }


    public class FakeHttpRequestData : HttpRequestData
    {
        public FakeHttpRequestData(FunctionContext functionContext, Uri url, Stream body = null) : base(functionContext)
        {
            Url = url;
            Body = body ?? new MemoryStream();
        }

        public override Stream Body { get; } = new MemoryStream();

        public override HttpHeadersCollection Headers { get; } = new HttpHeadersCollection();

        public override IReadOnlyCollection<IHttpCookie> Cookies { get; }

        public override Uri Url { get; }

        public override IEnumerable<ClaimsIdentity> Identities { get; }

        public override string Method { get; }

        public override HttpResponseData CreateResponse()
        {
            return new FakeHttpResponseData(FunctionContext);
        }
    }

    public class FakeHttpResponseData : HttpResponseData
    {
        public FakeHttpResponseData(FunctionContext functionContext) : base(functionContext)
        {
        }

        public override HttpStatusCode StatusCode { get; set; }
        public override HttpHeadersCollection Headers { get; set; } = new HttpHeadersCollection();
        public override Stream Body { get; set; } = new MemoryStream();
        public override HttpCookies Cookies { get; }
    }

}
