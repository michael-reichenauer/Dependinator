using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;


namespace Dependinator.Utils.Net
{
    public class HttpClientDownloadWithProgress : IDisposable
    {
        public delegate void ProgressChangedHandler(
            long? totalFileSize, long totalBytesDownloaded, double? progressPercentage);


        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly Timer dispatcherTimer;


        public HttpClientDownloadWithProgress()
        {
            dispatcherTimer = new Timer(state => cts.Cancel());
        }


        public HttpClient HttpClient { get; } = new HttpClient();

        public TimeSpan NetworkActivityTimeout { get; set; } = TimeSpan.FromSeconds(30);


        public void Dispose()
        {
            cts.Dispose();
            HttpClient?.Dispose();
            dispatcherTimer.Dispose();
        }


        public event ProgressChangedHandler ProgressChanged;


        public Task StartDownloadAsync(string uri, string filePath)
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            cts.Token.Register(() => tcs.TrySetCanceled());


            StartAsync(uri, filePath).ContinueWith(_ => tcs.TrySetResult(true));
            return tcs.Task;
        }


        private async Task StartAsync(string uri, string filePath)
        {
            dispatcherTimer.Change(NetworkActivityTimeout, NetworkActivityTimeout);
            using (var response = await HttpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cts.Token))
            {
                await DownloadFileAsync(response, filePath, cts.Token);
            }
        }


        private async Task DownloadFileAsync(HttpResponseMessage response, string filePath, CancellationToken ct)
        {
            response.EnsureSuccessStatusCode();

            long? totalBytes = response.Content.Headers.ContentLength;

            using (Stream contentStream = await response.Content.ReadAsStreamAsync())
            {
                await ProcessContentStreamAsync(totalBytes, contentStream, filePath, ct);
            }
        }


        private async Task ProcessContentStreamAsync(
            long? totalDownloadSize, Stream contentStream, string filePath, CancellationToken ct)
        {
            long totalBytesRead = 0L;
            long readCount = 0L;
            byte[] buffer = new byte[8192];
            bool isMoreToRead = true;

            using (var fileStream = new FileStream(
                filePath, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, true))
            {
                do
                {
                    dispatcherTimer.Change(NetworkActivityTimeout, NetworkActivityTimeout);
                    var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct);
                    if (bytesRead == 0)
                    {
                        isMoreToRead = false;
                        TriggerProgressChanged(totalDownloadSize, totalBytesRead);
                        continue;
                    }

                    await fileStream.WriteAsync(buffer, 0, bytesRead, ct);

                    totalBytesRead += bytesRead;
                    readCount += 1;

                    if (readCount % 100 == 0)
                    {
                        TriggerProgressChanged(totalDownloadSize, totalBytesRead);
                    }
                } while (isMoreToRead);
            }
        }


        private void TriggerProgressChanged(long? totalDownloadSize, long totalBytesRead)
        {
            if (ProgressChanged == null)
            {
                return;
            }

            double? progressPercentage = null;
            if (totalDownloadSize.HasValue)
            {
                progressPercentage = Math.Round((double)totalBytesRead / totalDownloadSize.Value * 100, 2);
            }

            ProgressChanged(totalDownloadSize, totalBytesRead, progressPercentage);
        }
    }
}
