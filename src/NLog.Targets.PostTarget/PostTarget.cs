﻿using System;
using System.Collections.Generic;
using NLog.Common;
using System.Net.Http;
using System.Text;
using System;
using System.Threading.Tasks;

namespace NLog.Targets.PostTarget
{
    [Target("PostTarget")]
    public class PostTarget : TargetWithLayout
    {
        private HttpClient _client;

        public string Uri { get; set; } = "http://localhost:9200";

        public bool RequireAuth { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }

        public string AcceptEncoding { get; set; } = "none";

        public string Encoding { get; set; } = "UTF-8";

        public string ContentType { get; set; } = "application/json";

        public bool KeepAlive { get; set; } = false;

        public int Timeout { get; set; } = 3;


        public PostTarget()
        {
            Name = "PostTarget";
        }

        protected override void InitializeTarget()
        {
            base.InitializeTarget();
            _client = new HttpClient();
            _client.Timeout = TimeSpan.FromSeconds(Timeout);
        }

        protected override void Write(AsyncLogEventInfo logEvent)
        {
            SendBatch(new[] { logEvent });
        }

        protected override void Write(IList<AsyncLogEventInfo> logEvents)
        {
            SendBatch(logEvents);
        }

        private async void SendBatch(ICollection<AsyncLogEventInfo> logEvents)
        {
            try
            {
                var payload = FormPayload(logEvents);

                Task<HttpResponseMessage> result;

                var httpContent = new StringContent(payload, System.Text.Encoding.GetEncoding(Encoding), ContentType);
                if (KeepAlive)
                {
                    httpContent.Headers.Add("Keep-Alive", "true");
                }

                if (AcceptEncoding != "none")
                { 
                    result = _client.PostAsync(Uri, new CompressedContent(httpContent, AcceptEncoding));
                }
                else
                {
                    result = _client.PostAsync(Uri, httpContent);
                }

                await result.ContinueWith(x => { });

                if (result.Result != null && result.Result.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    var errorMessage = "Error sending post";
                    InternalLogger.Error($"Failed to send log messages to PostTarget: status={result.Result.StatusCode}, message=\"{errorMessage}\"");
                    InternalLogger.Trace($"Failed to send log messages to PostTarget: result={result}");
                }

                foreach (var ev in logEvents)
                {
                    ev.Continuation(null);
                }
            }
            catch (Exception ex)
            {
                InternalLogger.Error($"Error while sending log messages to PostTarget: message=\"{ex.Message}\"");

                foreach (var ev in logEvents)
                {
                    ev.Continuation(ex);
                }
            }
        }

        private string FormPayload(ICollection<AsyncLogEventInfo> logEvents)
        {
            var sb = new StringBuilder();

            foreach (var ev in logEvents)
            {
                sb.AppendLine(Layout.Render(ev.LogEvent));
            }

            return sb.ToString();
        }
    }
}
